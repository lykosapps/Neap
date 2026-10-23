using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Neap.Core.Audio;
using Neap.Core.Connection;
using Neap.Core.Hid;
using Neap.Core.Presets;
using Neap.Core.Protocol;
using Neap.Core.Settings;

namespace Neap.Core.Pretend;

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
    public const ushort AtlasTransmitterProduct = 0x225E;
    public const ushort AtlasHeadsetProduct = 0x2260;

    /// <summary>With the pretend flag, pretends to be an Atlas Air; see the constructor.</summary>
    public const string AtlasFlag = "--atlas";
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

    /// <summary>The keys one owner's Atlas Air did not report, of those the Stealth Pro II does.</summary>
    private static readonly int[] AtlasLacks = [0x2E0, 0x510, 0x640, 0x650, 0x700, 0x710, 0x730, 0x740, 0x750, 0x760];

    /// <summary>The category one owner's Atlas Air gave no answer for, asked twice.</summary>
    private const string AtlasSilent = "3DT";

    private const int NameKey = 0x220;
    private const int SlotBlock = 0x400;
    private const int SlotStride = 0x20;

    // The Charging Dock's slot, and where in it the spare battery's charge sits.
    private const int DockSlot = 0;
    private const int SpareEntry = 3;

    private readonly object _gate = new();
    private readonly Dictionary<int, string> _values;
    private readonly Preset?[] _game = new Preset?[PresetStore.CustomSlots];
    private readonly Preset?[] _mic = new Preset?[PresetStore.CustomSlots];
    private readonly string[][] _info;
    private readonly string[][] _control;
    private readonly Queue<byte[]> _incoming = new();
    private readonly List<byte[]> _sent = new();
    private readonly Dictionary<string, int> _reads = new(StringComparer.Ordinal);
    private readonly List<PretendWrite> _writes = new();
    private readonly List<PretendRefusal> _refusals = new();
    private readonly List<PretendRefusal> _unraised = new();
    private bool _on = true;
    private bool _atlas;

    /// <summary>The playback loudness level the headset holds, in decibels; the Atlas Air does not answer for it.</summary>
    private float _loudnessDb = LoudnessBoost.StockDb;

    /// <summary>The product id of the device it is opened through: the Charging Dock, or the Atlas Air's transmitter.</summary>
    private readonly ushort _product;

    /// <param name="atlas">
    /// Be an Atlas Air, as one owner's recording showed it: through its own
    /// transmitter, with no transmitter slots in use, no answer for the game
    /// and chat mix, its volume mirrored at 0x260 with 0x2a0 at 100, 0x290 at
    /// 1 on its transmitter, and 0x620 turning on and off with the mute.
    /// </param>
    public PretendHeadset(bool atlas = false)
    {
        _product = atlas ? AtlasTransmitterProduct : DockProduct;
        _values = Defaults();
        _game[0] = new Preset(PresetStore.FirstCustomId, "Night Raid",
            new[] { 30, 20, 0, -10, 0, 10, 30, 40, 30, 20 }, Bank.Game, true);
        _game[1] = new Preset(PresetStore.FirstCustomId + 1, "Footstep Focus",
            new[] { -20, -20, -10, 0, 20, 40, 50, 40, 20, 0 }, Bank.Game, true);
        _mic[0] = new Preset(PresetStore.FirstCustomId, "Broadcast",
            new[] { -30, -10, 10, 20, 20, 10, 20, 30, 20, 0 }, Bank.Mic, true);
        _values[PresetStore.Game.CountKey] = "2";
        _values[PresetStore.Microphone.CountKey] = "1";

        // Slot one is the Charging Dock the headset is using, with a spare
        // battery charging in it, slot two a USB Transmitter it is paired
        // with, and the others are empty.
        _info =
        [
            ["2", "17", "1", "64", "1", "10F5", "229B", "0.0.0.0", "02:00:00:00:00:01"],
            ["1", "33", "0", "0", "1", "10F5", "229D", "0.0.0.0", "02:00:00:00:00:02"],
            ["0", "0", "0", "0", "0", "0", "0", "0", "00:00:00:00:00:00"],
            ["0", "0", "0", "0", "0", "0", "0", "0", "00:00:00:00:00:00"],
        ];
        _control = [["2", "80", "60"], ["1", "100", "100"], ["0", "0", "0"], ["0", "0", "0"]];
        if (atlas) BeAtlasAir();
    }

    private void BeAtlasAir()
    {
        _atlas = true;
        foreach (int key in AtlasLacks) _values.Remove(key);
        _values[0x110] = AtlasHeadsetProduct.ToString("X4", CultureInfo.InvariantCulture);
        _values[0x130] = "6.163.1646.0";
        _values[0x260] = _values[0x2A0];
        _values[0x2A0] = "100";
        _values[0x290] = "1";
        _values[0x2D0] = "0";
        _values[0x800] = "0";
        _values[0x810] = "1";
        _values[0x820] = "50";
        _values[0xC00] = "12";
        _values[0x430] = "50";
        _values[0x620] = _values[0x600];
        _values[0x630] = "20";
        for (int slot = 0; slot < _info.Length; slot++)
        {
            _info[slot] = ["0", "0", "0", "0", "0", "0", "0", "0", "00:00:00:00:00:00"];
            _control[slot] = ["0", "0", "0"];
        }
    }

    /// <summary>
    /// The system's output or microphone level moved, and the headset's
    /// mirror of it follows and is reported, as the headset's does.
    /// </summary>
    public void Follow(Flow flow, int percent)
    {
        int key = flow == Flow.Input ? Registry.ByName["mic_volume"].Key
            : (_atlas ? HeadsetModels.AtlasAir : HeadsetModels.StealthProII).VolumeKey;
        string value = Math.Clamp(percent, 0, 100).ToString(CultureInfo.InvariantCulture);
        lock (_gate)
        {
            _values[key] = value;
            if (_on) Notify(new Dictionary<int, string> { [key] = value });
        }
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

    /// <summary>The playback loudness level the headset currently holds, in decibels.</summary>
    public float LoudnessDb { get { lock (_gate) return _loudnessDb; } }

    /// <summary>Every frame sent, in order.</summary>
    public IReadOnlyList<byte[]> Sent
    {
        get { lock (_gate) return _sent.Select(frame => frame.ToArray()).ToList(); }
    }

    /// <summary>How many times each group of settings has been read.</summary>
    public IReadOnlyDictionary<string, int> Reads
    {
        get { lock (_gate) return new Dictionary<string, int>(_reads, StringComparer.Ordinal); }
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

    /// <summary>Forgets the frames, reads, writes and refusals recorded so far.</summary>
    public void ClearRecord()
    {
        lock (_gate)
        {
            _sent.Clear();
            _reads.Clear();
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
            var changed = new Dictionary<int, string> { [key] = wire };
            if (_atlas && key == 0x600) changed[0x620] = wire;
            foreach (var (at, to) in changed) _values[at] = to;
            if (_on) Notify(changed);
        }
    }

    /// <summary>
    /// Puts a spare battery in the Charging Dock's slot, or takes it out, and
    /// sends the dock's slot the way the headset does.
    /// </summary>
    /// <param name="percent">The spare's charge, or null for an empty slot.</param>
    /// <exception cref="ArgumentOutOfRangeException">The charge is outside 0 to 100.</exception>
    public void Spare(int? percent)
    {
        if (percent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "a spare battery's charge is 0 to 100");
        lock (_gate)
        {
            _info[DockSlot][SpareEntry] = percent?.ToString(CultureInfo.InvariantCulture) ?? SpareBattery.EmptySlot;
            if (!_on) return;
            var kvp = new StringBuilder();
            Append(kvp, Hex(SlotBlock), SlotRecord(DockSlot));
            Enqueue($"{{\"UP\":\"TX1\",\"KVP\":{{{kvp}}}}}", reply: false);
        }
    }

    /// <summary>Queues a raw inbound message, split into reports the way the headset splits them.</summary>
    public void Push(string json)
    {
        lock (_gate) Enqueue(json, reply: false);
    }

    // -- the device source -------------------------------------------------

    public IReadOnlyList<HidDeviceInfo> Candidates() =>
        [new HidDeviceInfo($"pretend:{_product:X4}", HidControl.VendorId, _product,
            HidControl.UsagePage, Frames.ReportLength, 0, Frames.ReportLength)];

    /// <summary>Whether opening the dock is refused, as Linux does until its udev rule is in place.</summary>
    public bool Refusing { get; init; }

    public IHidTransport Open(HidDeviceInfo device) =>
        Refusing ? throw new AccessDeniedException("pretend: no permission to open the dock")
        : device.ProductId == _product
            ? new Link(this)
            : throw new TransportException($"no pretend device 0x{device.ProductId:x4}");

    /// <summary>Opens the Charging Dock.</summary>
    public IHidTransport Open() => new Link(this);

    /// <summary>One open handle on the dock. Closing it leaves the headset as it was.</summary>
    private sealed class Link(PretendHeadset headset) : IHidTransport
    {
        public ushort ProductId => headset._product;

        public string Name => HeadsetName;

        public string Describe() => $"pretend 0x{HidControl.VendorId:x4}:0x{headset._product:x4}";

        public void SendOutput(ReadOnlySpan<byte> report) => headset.Receive(report);

        public byte[] GetInput(byte reportId = HidControl.InReportId) => headset.NextReport(reportId);

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
            if (IsLoudnessCommand(frame))
                Loudness(frame);
            else if (_atlas && IsLightRead(frame))
                ReplyLight();
            else if (_atlas && IsInputsRead(frame))
                ReplyInputs();
            else if (Unwrap(frame, out string? verb, out string? argument) is string wrong)
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

    // -- the loudness command (a RACE command, not a set_kvp write) ----------

    private const ushort SetOp = 0;

    /// <summary>Whether a frame is the DSP loudness command (id 0x000A).</summary>
    private static bool IsLoudnessCommand(byte[] frame) =>
        frame.Length >= 9 && frame[0] == Frames.OutReportId
        && frame[3] == Frames.RaceStart && frame[4] == Frames.RaceCommand
        && (frame[7] | (frame[8] << 8)) == LoudnessBoost.CommandId;

    /// <summary>Answers or applies RACE command 0x000A: the loudness level is read and set, as SP2Tools found on both headsets (F114), and the Atlas Air's monitoring gain is read (F120).</summary>
    private void Loudness(byte[] frame)
    {
        if (frame.Length < 19) return;              // the id, then op(2), parameter(4) and value(4)
        ushort op = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(9, 2));
        uint param = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(11, 4));
        float value = BinaryPrimitives.ReadSingleBigEndian(frame.AsSpan(15, 4));
        if (!_on) return;

        // The Atlas Air's stored monitoring gain is only ever read, as its firmware has it (F120).
        if (param == Sidetone.GainParam && _atlas && op != SetOp)
            ReplyLevel(param, Sidetone.GainFor(int.Parse(_values[0x630], CultureInfo.InvariantCulture)));
        if (param != LoudnessBoost.InputLevelParam) return;

        if (op == SetOp) _loudnessDb = LoudnessBoost.ClampLevel(value);
        else ReplyLevel(param, _loudnessDb);
    }

    /// <summary>Queues the reply to a read of the command: the parameter, then its level.</summary>
    private void ReplyLevel(uint param, float db)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(payload, param);
        BinaryPrimitives.WriteSingleBigEndian(payload.AsSpan(4), db);

        int inner = 2 + payload.Length;
        var message = new byte[4 + inner];
        message[0] = Frames.RaceStart;
        message[1] = Frames.RaceReply;
        message[2] = (byte)(inner & 0xFF);
        message[3] = (byte)(inner >> 8);
        message[4] = (byte)(LoudnessBoost.CommandId & 0xFF);
        message[5] = (byte)(LoudnessBoost.CommandId >> 8);
        payload.CopyTo(message, 6);
        Fragment(message);
    }

    // -- the transmitter's light (a RACE command the transmitter answers) -----

    /// <summary>Whether a frame is the read of the transmitter's light level; see <see cref="TransmitterLight"/>.</summary>
    private static bool IsLightRead(byte[] frame) => frame.AsSpan().StartsWith(TransmitterLight.ReadFrame);

    /// <summary>Queues the transmitter's answer to a light read: the level it shows, as a little-endian number.</summary>
    /// <remarks>The transmitter answers itself, so it does whether or not the headset is on.</remarks>
    private void ReplyLight()
    {
        int level = int.Parse(_values[0x430], CultureInfo.InvariantCulture);
        var message = new byte[] { Frames.RaceStart, Frames.RaceReply, 6, 0, 0x06, 0x00, (byte)level, 0, 0, 0 };
        Fragment(message);
    }

    private static bool IsInputsRead(byte[] frame) =>
        frame.AsSpan().StartsWith(HeadsetInputs.ReadFrame(true)) || frame.AsSpan().StartsWith(HeadsetInputs.ReadFrame(false));

    /// <summary>Queues the headset's answer to a read of its inputs: no presses yet and every wheel at rest.</summary>
    private void ReplyInputs()
    {
        if (!_on) return;
        var message = new byte[4 + 18];
        new byte[] { Frames.RaceStart, Frames.RaceReply, 18, 0, 0x00, 0x00 }.CopyTo(message, 0);
        Fragment(message);
    }

    // -- reads --------------------------------------------------------------

    private void Answer(string verb)
    {
        string category = Verbs.Readers.First(pair => pair.Value == verb).Key;
        _reads[category] = _reads.GetValueOrDefault(category) + 1;
        if (!_on || (_atlas && category == AtlasSilent)) return;
        var kvp = new StringBuilder();

        if (Verbs.TransmitterCategories.Contains(category))
        {
            Append(kvp, Hex(Verbs.TransmitterKey(category)), SlotRecord(category[2] - '1'));
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

    private string SlotRecord(int slot) =>
        $"{{\"info\":{ArrayOf(_info[slot])},\"control\":{ArrayOf(_control[slot])}}}";

    /// <summary>The category a plain value is read in, or null for one that is part of a slot.</summary>
    private static string? CategoryOf(int key) =>
        Unlisted.TryGetValue(key, out var unlisted) ? unlisted
        : IsPlainValue(key) && Registry.ByKey.TryGetValue(key, out var setting) ? setting.Category
        : null;

    /// <summary>
    /// Whether a key is read as a value of its own, rather than inside a
    /// transmitter or preset slot's reply.
    /// </summary>
    /// <remarks>The Atlas Air's transmitter group sits among the slots' keys and is read as plain values.</remarks>
    private static bool IsPlainValue(int key) =>
        (key is not (>= SlotBlock and < SlotBlock + Transmitters.Slots * SlotStride)
         || Registry.ByKey.TryGetValue(key, out var setting) && setting.Category == "TX")
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
                // The Atlas Air keeps its mic monitoring level where the
                // Stealth Pro II keeps its AI noise reduction switch.
                wire = Registry.WireValue(key, value, _atlas && key == 0x630 ? 0x620 : null);
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
        if (!IsPlainValue(key) && inSlot is >= 0 and < Transmitters.Slots * SlotStride)
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
        Fragment(message);
    }

    /// <summary>Cuts one RACE message into inbound reports, each holding as much as fits.</summary>
    private void Fragment(byte[] message)
    {
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
            // Identity. The serial is made up; the firmware is the build firmware edits are made for.
            [0x100] = "10F5",
            [0x110] = "229E",
            [0x120] = Serial,
            [0x130] = "4.107.703.0",
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
            // At 100 throughout ten days of Swarm II's logs.
            [0x260] = "100",
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
