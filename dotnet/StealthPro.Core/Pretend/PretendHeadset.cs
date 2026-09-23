using System.Globalization;
using System.Text;
using System.Text.Json;
using StealthPro.Core.Hid;
using StealthPro.Core.Presets;
using StealthPro.Core.Protocol;
using StealthPro.Core.Settings;

namespace StealthPro.Core.Pretend;

/// <summary>A write the pretend headset accepted and applied, in wire form.</summary>
public sealed record PretendWrite(int Key, string Value);

/// <summary>A frame the pretend headset would not act on, and why.</summary>
/// <param name="Frame">The frame as sent, in hex.</param>
/// <param name="Reason">What was wrong with it.</param>
public sealed record PretendRefusal(string Frame, string Reason);

/// <summary>
/// A Stealth Pro II with no hardware behind it: a headset on a Charging Dock
/// that answers the protocol from state it keeps, and records every frame
/// sent to it.
/// </summary>
/// <remarks>
/// <para>
/// It answers each read verb with the reply FINDINGS.md documents for it,
/// applies a write only when <see cref="Registry.WireValue"/> confirms it,
/// and records anything else as a refusal rather than acting on it: an
/// unconfirmed key, a value out of range, a transmitter slot's base address,
/// a verb the headset does not know, and any frame that is not a Turtle Beach
/// command, which covers Airoha's own firmware commands. The app's client
/// refuses all of these before they are sent, so a refusal here means a check
/// on the app's side has been bypassed.
/// </para>
/// <para>
/// The effects of writes follow what was measured on the hardware: writing a
/// band clears the selected preset, writing a name saves a preset into the
/// lowest free slot whatever id was asked for, writing a name to a delete
/// register frees that preset's slot, and a lighting write lands in the slot
/// it addresses. Each accepted write is pushed back as a notification.
/// </para>
/// <para>
/// Everything it reports is made up: the name, the serial, the firmware
/// versions and the transmitters' addresses, which are locally administered.
/// </para>
/// </remarks>
public sealed class PretendHeadset : IDeviceSource
{
    public const ushort DockProduct = 0x229B;
    public const ushort TransmitterProduct = 0x229D;
    public const ushort HeadsetProduct = 0x229E;
    public const string HeadsetName = "Pretend Headset";
    public const string Serial = "PRETEND-0000001";

    /// <summary>Bytes of payload one inbound report carries after its id and length.</summary>
    private const int PayloadPerReport = Frames.ReportLength - 3;

    /// <summary>Keys the headset reports that the registry does not list.</summary>
    /// <remarks>
    /// FINDINGS.md shows a general-state reply opening with these three;
    /// only the name, 0x220, is identified.
    /// </remarks>
    private static readonly IReadOnlyDictionary<int, string> Unlisted = new Dictionary<int, string>
    {
        [0x200] = "GSI",
        [0x210] = "GSI",
        [0x220] = "GSI",
    };

    private const int NameKey = 0x220;
    private const int SlotBlock = 0x400;
    private const int SlotStride = 0x20;

    private readonly object _gate = new();
    private readonly Dictionary<int, string> _values;
    private readonly Preset?[] _game = new Preset?[PresetStore.CustomSlots];
    private readonly Preset?[] _mic = new Preset?[PresetStore.CustomSlots];
    private readonly string[][] _info;
    private readonly string[][] _control;
    private readonly Queue<byte[]> _incoming = new();
    private readonly List<byte[]> _sent = new();
    private readonly List<PretendWrite> _writes = new();
    private readonly List<PretendRefusal> _refusals = new();
    private readonly List<PretendRefusal> _unraised = new();
    private bool _on = true;

    public PretendHeadset()
    {
        _values = Defaults();
        _game[0] = new Preset(PresetStore.FirstCustomId, "Night Raid",
            new[] { 30, 20, 0, -10, 0, 10, 30, 40, 30, 20 }, Bank.Game, true);
        _game[1] = new Preset(PresetStore.FirstCustomId + 1, "Footstep Focus",
            new[] { -20, -20, -10, 0, 20, 40, 50, 40, 20, 0 }, Bank.Game, true);
        _mic[0] = new Preset(PresetStore.FirstCustomId, "Broadcast",
            new[] { -30, -10, 10, 20, 20, 10, 20, 30, 20, 0 }, Bank.Mic, true);
        _values[PresetStore.Game.CountKey] = "2";
        _values[PresetStore.Microphone.CountKey] = "1";

        // Slot one is the Charging Dock the headset is using, slot two a USB
        // Transmitter it is paired with, and the others are empty.
        _info =
        [
            ["2", "17", "1", "100", "1", "10F5", "229B", "0.0.0.0", "02:00:00:00:00:01"],
            ["1", "17", "1", "100", "1", "10F5", "229D", "0.0.0.0", "02:00:00:00:00:02"],
            ["0", "0", "0", "0", "0", "0", "0", "0", "00:00:00:00:00:00"],
            ["0", "0", "0", "0", "0", "0", "0", "0", "00:00:00:00:00:00"],
        ];
        _control = [["2", "80", "60"], ["1", "100", "100"], ["0", "0", "0"], ["0", "0", "0"]];
    }

    /// <summary>A frame was refused. Raised on the thread that sent it.</summary>
    public event Action<PretendRefusal>? Refused;

    /// <summary>
    /// Whether the headset is switched on. Switched off, the dock stays
    /// plugged in and answers nothing.
    /// </summary>
    public bool On
    {
        get { lock (_gate) return _on; }
        set { lock (_gate) _on = value; }
    }

    /// <summary>Every frame sent, in order.</summary>
    public IReadOnlyList<byte[]> Sent
    {
        get { lock (_gate) return _sent.Select(frame => frame.ToArray()).ToList(); }
    }

    /// <summary>Every write accepted, in order.</summary>
    public IReadOnlyList<PretendWrite> Writes
    {
        get { lock (_gate) return _writes.ToList(); }
    }

    /// <summary>Every frame refused, in order.</summary>
    public IReadOnlyList<PretendRefusal> Refusals
    {
        get { lock (_gate) return _refusals.ToList(); }
    }

    /// <summary>Forgets the frames, writes and refusals recorded so far.</summary>
    public void ClearRecord()
    {
        lock (_gate)
        {
            _sent.Clear();
            _writes.Clear();
            _refusals.Clear();
        }
    }

    /// <summary>A value as the headset holds it, or null when it holds none.</summary>
    /// <remarks>A transmitter's lighting is held in its slot, at +1 and +2.</remarks>
    public string? Value(int key)
    {
        int inSlot = key - SlotBlock;
        lock (_gate)
            return inSlot is >= 0 and < Transmitters.Slots * SlotStride && inSlot % SlotStride is 1 or 2
                ? _control[inSlot / SlotStride][inSlot % SlotStride]
                : _values.GetValueOrDefault(key);
    }

    /// <summary>The custom presets stored in one bank, by slot, with nulls for empty slots.</summary>
    public IReadOnlyList<Preset?> Customs(Bank bank)
    {
        lock (_gate) return Slots(bank).ToArray();
    }

    /// <summary>
    /// Changes a value the way the headset does itself, from a button, a
    /// wheel or the battery, and notifies it.
    /// </summary>
    /// <remarks>
    /// Any key the headset reports can change this way, including ones the
    /// app may not write, but the value is checked against the registry so a
    /// change the real headset could not make is not reported.
    /// </remarks>
    /// <exception cref="ArgumentException">The key is not one the headset reports, or the value is not valid for it.</exception>
    public void Report(int key, string value)
    {
        if (!Registry.ByKey.TryGetValue(key, out var setting) || !IsPlainValue(key))
            throw new ArgumentException($"0x{key:x} is not a value the headset reports on its own");
        string wire = setting.Validate(value);
        lock (_gate)
        {
            _values[key] = wire;
            if (_on) Notify(new Dictionary<int, string> { [key] = wire });
        }
    }

    /// <summary>Queues a raw inbound message, split into reports the way the headset splits them.</summary>
    public void Push(string json)
    {
        lock (_gate) Enqueue(json, reply: false);
    }

    // -- the device source -------------------------------------------------

    public IReadOnlyList<HidDeviceInfo> Candidates() =>
        [new HidDeviceInfo("pretend:229B", HidTransport.DefaultVendorId, DockProduct,
            HidTransport.VendorUsagePage, Frames.ReportLength, 0, Frames.ReportLength)];

    public IHidTransport Open(HidDeviceInfo device) =>
        device.ProductId == DockProduct
            ? new Link(this)
            : throw new TransportException($"no pretend device 0x{device.ProductId:x4}");

    /// <summary>Opens the Charging Dock.</summary>
    public IHidTransport Open() => new Link(this);

    /// <summary>One open handle on the dock. Closing it leaves the headset as it was.</summary>
    private sealed class Link(PretendHeadset headset) : IHidTransport
    {
        public ushort ProductId => DockProduct;

        public string Describe() => $"pretend 0x{HidTransport.DefaultVendorId:x4}:0x{DockProduct:x4}";

        public void SendOutput(ReadOnlySpan<byte> report) => headset.Receive(report);

        public byte[] GetInput(byte reportId = HidTransport.InReportId) => headset.NextReport(reportId);

        public void Dispose() { }
    }

    // -- frames in ----------------------------------------------------------

    /// <exception cref="TransportException">The frame is longer than the output report.</exception>
    private void Receive(ReadOnlySpan<byte> report)
    {
        if (report.Length > Frames.ReportLength)
            throw new TransportException(
                $"frame is {report.Length} bytes and the output report holds {Frames.ReportLength}");

        byte[] frame = report.ToArray();
        PretendRefusal[] raised;
        lock (_gate)
        {
            _sent.Add(frame);
            if (Unwrap(frame, out string? verb, out string? argument) is string wrong)
                Refuse(frame, wrong);
            else if (argument is null)
                Answer(verb!);
            else
                Write(frame, argument);
            raised = _unraised.ToArray();
            _unraised.Clear();
        }
        foreach (var refusal in raised) Refused?.Invoke(refusal);
    }

    /// <summary>Takes a frame apart, the reverse of <see cref="Frames.Build"/>.</summary>
    /// <returns>Null when it is a Turtle Beach command, or what is wrong with it.</returns>
    private static string? Unwrap(byte[] frame, out string? verb, out string? argument)
    {
        verb = argument = null;
        if (frame.Length < 7 || frame[0] != Frames.OutReportId
            || frame[3] != Frames.RaceStart || frame[4] != Frames.RaceCommand)
            return "not a RACE command";

        int total = frame[1] | (frame[2] << 8);
        int inner = frame[5] | (frame[6] << 8);
        if (total != inner + 4 || 7 + inner > frame.Length) return "the lengths do not match the frame";

        var body = frame.AsSpan(7, inner);
        if (body.Length < 2 || body[0] != 0x02 || body[1] != 0x99)
            return "not a Turtle Beach command; Airoha's own commands, the firmware layer "
                + "and the update path are never sent";
        if (body.Length < 16 || body[2] != 0x48 || body[4] != 0xFB) return "the command header is malformed";

        if (body[3] == 0x01 && body[14] == 0x61 && body[15] == 0x00)
        {
            verb = Encoding.ASCII.GetString(body[16..]);
            return Verbs.Readers.Values.Contains(verb)
                ? null : $"unknown verb '{verb}'; the headset does not answer it";
        }

        const string SetVerb = "set_kvp";
        int json = 15 + SetVerb.Length + 1;
        if (body[3] == 0x03 && body[14] == 0xB7 && body.Length > json
            && Encoding.ASCII.GetString(body.Slice(15, SetVerb.Length)) == SetVerb
            && body[json - 1] == 0xFF)
        {
            verb = SetVerb;
            argument = Encoding.ASCII.GetString(body[json..]);
            return null;
        }
        return "the verb and its argument are malformed";
    }

    private void Refuse(byte[] frame, string reason)
    {
        var refusal = new PretendRefusal(Convert.ToHexString(frame), reason);
        _refusals.Add(refusal);
        _unraised.Add(refusal);
    }

    // -- reads --------------------------------------------------------------

    private void Answer(string verb)
    {
        if (!_on) return;
        string category = Verbs.Readers.First(pair => pair.Value == verb).Key;
        var kvp = new StringBuilder();

        if (Verbs.TransmitterCategories.Contains(category))
        {
            int slot = category[2] - '1';
            Append(kvp, Hex(Verbs.TransmitterKey(category)),
                $"{{\"info\":{ArrayOf(_info[slot])},\"control\":{ArrayOf(_control[slot])}}}");
        }
        else if (Verbs.PresetSlotCategories.Contains(category))
        {
            var bank = category[1] == 'G' ? Bank.Game : Bank.Mic;
            // An empty slot does not answer at all.
            if (Slots(bank)[category[2] - '1'] is not { } preset) return;
            Append(kvp, Hex(Verbs.PresetSlotKey(category)),
                $"{{\"name\":{Quote(preset.Name)},\"bands\":{ArrayOf(preset.Bands)}}}");
        }
        else
        {
            // The protocol version's reply has not been captured, so Ver
            // answers with no values rather than with guessed ones.
            foreach (var (key, value) in _values.Where(v => CategoryOf(v.Key) == category).OrderBy(v => v.Key))
                Append(kvp, Hex(key), Quote(value));
        }

        Enqueue($"{{\"OR\":\"{category}\",\"KVP\":{{{kvp}}}}}", reply: true);
    }

    /// <summary>The category a plain value is read in, or null for one that is part of a slot.</summary>
    private static string? CategoryOf(int key) =>
        Unlisted.TryGetValue(key, out var unlisted) ? unlisted
        : IsPlainValue(key) && Registry.ByKey.TryGetValue(key, out var setting) ? setting.Category
        : null;

    /// <summary>
    /// Whether a key is read as a value of its own, rather than inside a
    /// transmitter or preset slot's reply.
    /// </summary>
    private static bool IsPlainValue(int key) =>
        key is not (>= SlotBlock and < SlotBlock + Transmitters.Slots * SlotStride)
        && !(key >= 0x1700 && key < 0x1900);

    // -- writes -------------------------------------------------------------

    private void Write(byte[] frame, string argument)
    {
        Dictionary<string, string>? pairs;
        try
        {
            pairs = JsonSerializer.Deserialize<Dictionary<string, string>>(argument);
        }
        catch (JsonException)
        {
            pairs = null;
        }
        if (pairs is null || pairs.Count == 0)
        {
            Refuse(frame, $"the argument is not an object of strings: {argument}");
            return;
        }

        var changed = new Dictionary<int, string>();
        foreach (var (name, value) in pairs)
        {
            if (!name.StartsWith("0x", StringComparison.Ordinal)
                || !int.TryParse(name.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int key))
            {
                Refuse(frame, $"'{name}' is not a key");
                continue;
            }

            string wire;
            try
            {
                wire = Registry.WireValue(key, value);
            }
            catch (ArgumentException ex)
            {
                Refuse(frame, ex.Message);
                continue;
            }

            // Switched off, nothing reaches the headset; the frame is still
            // checked, because what the app sends is what is being tested.
            if (!_on) continue;
            if (Apply(key, wire, changed) is string trouble)
            {
                Refuse(frame, trouble);
                continue;
            }
            _writes.Add(new PretendWrite(key, wire));
        }
        if (changed.Count > 0) Notify(changed);
    }

    /// <summary>Applies one confirmed write, noting every value it moved.</summary>
    /// <returns>Null when applied, or why the headset would not take it.</returns>
    private string? Apply(int key, string value, Dictionary<int, string> changed)
    {
        void Set(int at, string to)
        {
            _values[at] = to;
            changed[at] = to;
        }

        int inSlot = key - SlotBlock;
        if (inSlot is >= 0 and < Transmitters.Slots * SlotStride)
        {
            // Only +1 and +2 get this far: WireValue refuses the rest.
            _control[inSlot / SlotStride][inSlot % SlotStride] = value;
            return null;
        }

        foreach (var spec in new[] { PresetStore.Game, PresetStore.Microphone })
        {
            var slots = Slots(spec.Bank);
            if (key == spec.Select)
            {
                Set(key, value);
                int id = int.Parse(value, CultureInfo.InvariantCulture);
                int slot = id - PresetStore.FirstCustomId;
                var chosen = PresetStore.Factory(spec.Bank).FirstOrDefault(p => p.Id == id)
                    ?? (slot is >= 0 and < PresetStore.CustomSlots ? slots[slot] : null);
                if (chosen is not null)
                {
                    for (int band = 0; band < spec.Bands.Count; band++)
                        Set(spec.Bands[band], chosen.Bands[band].ToString(CultureInfo.InvariantCulture));
                }
                Set(spec.NameKey, chosen?.Name ?? "");
                return null;
            }
            if (spec.Bands.Contains(key))
            {
                // Measured: the curve is no longer the preset it was.
                Set(key, value);
                Set(spec.Select, "0");
                Set(spec.NameKey, "");
                return null;
            }
            if (key == spec.NameKey)
            {
                if (value.Trim().Length == 0)
                {
                    Set(key, "");
                    return null;
                }
                // Writing the name commits a save, into the lowest free slot.
                int free = Array.IndexOf(slots, null);
                if (free < 0) return "all five custom slots are in use";
                var bands = spec.Bands
                    .Select(band => int.Parse(_values[band], CultureInfo.InvariantCulture)).ToArray();
                slots[free] = new Preset(PresetStore.FirstCustomId + free, value, bands, spec.Bank, true);
                Set(spec.Select, (PresetStore.FirstCustomId + free).ToString(CultureInfo.InvariantCulture));
                Set(key, value);
                Set(spec.CountKey, slots.Count(p => p is not null).ToString(CultureInfo.InvariantCulture));
                return null;
            }
            if (key == spec.DeleteKey)
            {
                int found = Array.FindIndex(slots, p => p?.Name == value);
                if (found >= 0) slots[found] = null;
                Set(key, value);
                Set(spec.CountKey, slots.Count(p => p is not null).ToString(CultureInfo.InvariantCulture));
                return null;
            }
        }

        Set(key, value);
        return null;
    }

    private Preset?[] Slots(Bank bank) => bank == Bank.Game ? _game : _mic;

    // -- messages out -------------------------------------------------------

    /// <summary>Pushes changed values, one notification per category.</summary>
    private void Notify(Dictionary<int, string> changed)
    {
        foreach (var group in changed.Where(c => CategoryOf(c.Key) is not null)
                     .GroupBy(c => CategoryOf(c.Key)!))
        {
            var kvp = new StringBuilder();
            foreach (var (key, value) in group.OrderBy(c => c.Key)) Append(kvp, Hex(key), Quote(value));
            Enqueue($"{{\"UP\":\"{group.Key}\",\"KVP\":{{{kvp}}}}}", reply: false);
        }
    }

    /// <summary>
    /// Queues one message as the headset sends it: a RACE header, then the
    /// text, cut into reports that each hold as much as fits.
    /// </summary>
    /// <remarks>
    /// Only the first report carries the header; the rest are raw
    /// continuation bytes, split wherever the report fills, mid-string
    /// included.
    /// </remarks>
    private void Enqueue(string json, bool reply)
    {
        byte[] text = Encoding.ASCII.GetBytes(json);
        int inner = 2 + text.Length;
        var message = new byte[4 + inner];
        message[0] = Frames.RaceStart;
        message[1] = reply ? (byte)0x5B : (byte)0x5D;
        message[2] = (byte)(inner & 0xFF);
        message[3] = (byte)(inner >> 8);
        message[4] = 0x02;
        message[5] = 0x99;
        text.CopyTo(message, 6);

        for (int at = 0; at < message.Length; at += PayloadPerReport)
        {
            int length = Math.Min(PayloadPerReport, message.Length - at);
            var report = new byte[Frames.ReportLength];
            report[0] = Frames.InReportId;
            report[1] = (byte)length;
            message.AsSpan(at, length).CopyTo(report.AsSpan(3));
            _incoming.Enqueue(report);
        }
    }

    private byte[] NextReport(byte reportId)
    {
        lock (_gate)
        {
            if (_incoming.Count > 0) return _incoming.Dequeue();
        }
        var empty = new byte[Frames.ReportLength];
        empty[0] = reportId;
        return empty;
    }

    private static void Append(StringBuilder kvp, string key, string json)
    {
        if (kvp.Length > 0) kvp.Append(',');
        kvp.Append('"').Append(key).Append("\":").Append(json);
    }

    private static string ArrayOf<T>(IEnumerable<T> values) =>
        "[" + string.Join(",", values.Select(v => Quote(Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""))) + "]";

    /// <summary>A JSON string escaped the way the app escapes what it sends.</summary>
    private static string Quote(string value)
    {
        var json = new StringBuilder();
        Frames.EncodeString(json, value);
        return json.ToString();
    }

    private static string Hex(int key) => key.ToString("x", CultureInfo.InvariantCulture);

    // -- the headset as it starts -------------------------------------------

    /// <summary>Every value the headset reads back, as a headset in use might hold them.</summary>
    private static Dictionary<int, string> Defaults()
    {
        var values = new Dictionary<int, string>
        {
            // Identity. The serial and firmware are made up.
            [0x100] = "10F5",
            [0x110] = "229E",
            [0x120] = Serial,
            [0x130] = "0.0.0.0",
            [0x140] = "0",
            [0x150] = "1",
            [0x160] = "0",

            // General state: on 2.4 GHz with Bluetooth connected, sound
            // arriving, not charging.
            [0x200] = "1",
            [0x210] = "0",
            [NameKey] = HeadsetName,
            [0x230] = "2",
            [0x240] = "76",
            [0x250] = "0",
            [0x280] = "2",
            [0x290] = "3",
            [0x2A0] = "45",
            [0x2D0] = "60",
            [0x2E0] = "1",

            // Bluetooth, and the signal strength, unsigned as a read reports it.
            [0x300] = "0",
            [0x310] = "0",
            [0x320] = "204",
            [0x330] = "0",
            [0x340] = "0",
            [0x350] = "0",

            [0x510] = "50",

            [0x600] = "0",
            [0x610] = "70",
            [0x620] = "30",
            [0x630] = "1",
            [0x640] = "1",
            [0x650] = "0",

            [0x700] = "0",
            [0x710] = "40",
            [0x720] = "0",
            [0x730] = "1",
            [0x740] = "50",
            [0x750] = "1",
            [0x760] = "80",

            [0xA00] = "0",
            [0xA10] = "1",
            [0xA20] = "1",
            [0xA30] = "30",
            [0xB00] = "0",
            [0xB10] = "0",
            [0xB20] = "0",

            [0x1200] = "0",

            [0x1600] = "0",
            [0x1610] = "",
            [0x1620] = "0",
            [0x1630] = "",
            [0x1640] = "1",
            [0x1650] = "",
        };

        // Each bank on a factory preset.
        foreach (var (spec, preset) in new[]
                 {
                     (PresetStore.Game, PresetStore.Factory(Bank.Game)[1]),
                     (PresetStore.Microphone, PresetStore.Factory(Bank.Mic)[0]),
                 })
        {
            values[spec.Select] = preset.Id.ToString(CultureInfo.InvariantCulture);
            values[spec.NameKey] = preset.Name;
            for (int band = 0; band < spec.Bands.Count; band++)
                values[spec.Bands[band]] = preset.Bands[band].ToString(CultureInfo.InvariantCulture);
        }
        return values;
    }
}
