using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Neap.Core.Audio;
using Neap.Core.Hid;
using Neap.Core.Protocol;
using Neap.Core.Settings;

namespace Neap.Core;

public class WritesDisabledException : Exception
{
    public WritesDisabledException(string message) : base(message) { }
}

/// <summary>A change the app asked the headset for, and whether it was sent.</summary>
/// <param name="Key">The setting's key.</param>
/// <param name="Value">The value as sent, or as asked for when it was not.</param>
/// <param name="Refused">Why it was not sent, or null when it was.</param>
/// <param name="Setting">The setting it was checked as, where the headset keeps that setting at <paramref name="Key"/>; see <see cref="HeadsetModels.CheckedAs"/>.</param>
public sealed record WriteAttempt(int Key, string Value, string? Refused, int? Setting = null);

/// <summary>
/// Reads and writes headset settings. Reads are always allowed; a write needs
/// a client made with writes allowed and a key the <see cref="Registry"/>
/// confirms as writable.
/// </summary>
/// <remarks>
/// Swarm II polls the same channel thousands of times a second and drains the
/// notification queue, so while it runs the replies land in its process
/// rather than ours. Only one of the two can usefully talk to the headset at
/// a time.
/// </remarks>
public sealed class HeadsetClient : IDisposable
{
    private readonly IHidTransport _transport;
    private readonly bool _ownsTransport;
    private readonly List<byte> _buffer = new();
    private readonly List<DeviceEvent> _setAside = new();
    private int _counter;
    private long _lastSet;
    private byte[]? _heard;
    private int _reads;
    private byte[] _lastReport = [];

    /// <summary>How long a read waits after a write before it is sent.</summary>
    /// <remarks>
    /// The headset drops a request sent straight after another, and a write
    /// has no reply to wait for, so a read sent right behind one could be
    /// lost. A lost read of a preset slot looks like an empty slot. About one
    /// read's round trip.
    /// </remarks>
    internal static readonly TimeSpan AfterWrite = TimeSpan.FromMilliseconds(100);

    /// <summary>The shortest time between two reads while waiting for a reply.</summary>
    /// <remarks>
    /// The pace Windows sets on its own: a read there takes about 12 ms, so
    /// with a short rest it comes to one every 15 ms or so, 190 in three
    /// seconds on the Atlas Air's Windows tester's PC. A read on Linux takes
    /// about 1 ms, which would otherwise ask four times as often, faster than
    /// any headset has been seen to answer reliably.
    /// </remarks>
    internal static readonly TimeSpan ReadInterval = TimeSpan.FromMilliseconds(15);

    public bool AllowWrites { get; }

    /// <summary>
    /// The first report that arrived during the last <see cref="ReadCategory"/>
    /// that went unanswered, in hex; null when nothing arrived at all.
    /// </summary>
    /// <remarks>
    /// A device that sends something other than the reply asked for is not
    /// silent, and the two need telling apart when the headset reads as off.
    /// </remarks>
    public string? Unanswered { get; private set; }

    /// <param name="allowWrites">Whether this client may write to the headset at all.</param>
    /// <param name="transport">The channel to the device, or the first plugged in to ask when none is given.</param>
    /// <param name="ownsTransport">
    /// Close <paramref name="transport"/> when this client is disposed. A
    /// transport handed in is otherwise the caller's to close.
    /// </param>
    public HeadsetClient(bool allowWrites = false, IHidTransport? transport = null,
        bool ownsTransport = false)
    {
        AllowWrites = allowWrites;
        _ownsTransport = transport is null || ownsTransport;
        _transport = transport ?? SystemDevices.Instance.Open(HidControl.Pick(SystemDevices.Instance.Candidates()));
    }

    /// <summary>How long to give a device to prove it has the headset.</summary>
    private static readonly TimeSpan AskWindow = TimeSpan.FromMilliseconds(900);

    /// <summary>
    /// Opens the device the headset is actually behind, by asking each
    /// candidate in turn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What is plugged in does not tell you where the headset is. It pairs
    /// with one transmitter at a time, and the others open cleanly and answer
    /// nothing. Measured with the USB Transmitter and the Charging Dock both
    /// connected: the USB Transmitter returned no values while the Charging
    /// Dock returned everything.
    /// </para>
    /// <para>
    /// This lives in Core rather than the app because the probe needs it too.
    /// Without it, a harness can read an empty result off the wrong device
    /// while the headset is connected elsewhere.
    /// </para>
    /// </remarks>
    /// <param name="allowWrites">Whether the returned client may write to the headset, by the product id of the device it opens.</param>
    /// <param name="present">How many candidate devices were found.</param>
    /// <param name="askLast">A product id to ask after the others, when its answer is in doubt.</param>
    /// <param name="devices">Where to look; what the operating system has when not given.</param>
    /// <param name="failed">Told why a device could not be opened or asked, with its description, so a headset that is there but not answering can be told from one that is not.</param>
    /// <param name="window">How long to give each device to answer; a short while when not given.</param>
    /// <returns>
    /// The client for the device that answered, or null when devices are
    /// present but none has the headset.
    /// </returns>
    /// <exception cref="DeviceNotFoundException">No candidate device is present.</exception>
    public static HeadsetClient? Behind(Func<ushort, bool> allowWrites, out int present, ushort? askLast = null,
        IDeviceSource? devices = null,
        Action<string, Exception>? failed = null, TimeSpan? window = null)
    {
        devices ??= SystemDevices.Instance;
        var candidates = devices.Candidates().OrderBy(d => d.ProductId == askLast).ToList();
        present = candidates.Count;
        if (present == 0)
            throw new DeviceNotFoundException("no Turtle Beach control collection is present");

        foreach (var device in candidates)
        {
            HeadsetClient? client = null;
            try
            {
                // The client owns this handle, so every device turned down is
                // closed, and so is the one kept once the app lets it go. The
                // app re-asks every device every few seconds while the
                // headset is off, so an unowned handle leaks on each pass.
                client = new HeadsetClient(allowWrites(device.ProductId), devices.Open(device),
                    ownsTransport: true);
                client.Drain();
                if (client.ReadCategory("GSI", window ?? AskWindow).Count > 0) return client;
                failed?.Invoke(device.Path, client.Unanswered is { } heard
                    ? new InvalidDataException($"it sent something other than the reply asked for: {heard}")
                    : new TimeoutException($"it opened and gave no answer: {client._reads} reads of "
                        + $"{device.InputLength} bytes with nothing in them, to a request of {device.OutputLength} bytes, "
                        + $"from {client.Device}"));
            }
            catch (Exception ex)
            {
                // Cannot be opened or does not answer: try the next rather
                // than failing the whole connect, but say why.
                failed?.Invoke(device.Path, ex);
            }
            client?.Dispose();
        }
        return null;
    }

    public string Device => _transport.Describe();

    /// <summary>The USB product ID of the Turtle Beach device this handle is on.</summary>
    /// <remarks>
    /// The device Windows sees is usually a transmitter, not the headset, and
    /// which one it is changes what the link means, so anything reporting a
    /// connection needs this.
    /// </remarks>
    public ushort ProductId => _transport.ProductId;

    /// <summary>The name the device's firmware gives itself, or empty when it gives none; see <see cref="IHidTransport.Name"/>.</summary>
    public string Name => _transport.Name;

    private int NextCounter() => _counter = (_counter + 1) & 0xFFFF;

    // -- reading -----------------------------------------------------------

    /// <summary>
    /// Performs one read from the headset and returns any complete events found.
    /// </summary>
    /// <remarks>
    /// Notifications that arrived while <see cref="ReadCategory"/> waited for
    /// its reply come first, so none is lost to a read.
    /// </remarks>
    public IReadOnlyList<DeviceEvent> ReadOnce()
    {
        var events = Receive();
        if (_setAside.Count == 0) return events;
        var all = new List<DeviceEvent>(_setAside);
        all.AddRange(events);
        _setAside.Clear();
        return all;
    }

    /// <summary>Performs one read from the transport and returns any complete events found.</summary>
    /// <remarks>
    /// Long replies span several reports and an unrelated notification can
    /// land in the middle of one, so only the consumed prefix is dropped.
    /// </remarks>
    private List<DeviceEvent> Receive() => Ingest(_transport.GetInput());

    /// <summary>Takes in one already-read report, returning any complete events found.</summary>
    /// <remarks>
    /// <para>
    /// Split from <see cref="Receive"/> so a command exchange can hand on a
    /// report that was not its reply without losing a notification in it.
    /// </para>
    /// <para>
    /// A report the same as the one read just before it is taken in once. The
    /// Atlas Air's transmitter hands back the report it last gave until it has
    /// a new one: one tester's recording holds a single signal report 4,998
    /// times in under a second. Taken in again, a copy of a long reply's first
    /// part leaves a reply that never closes, and every reply after it is
    /// read as part of that one.
    /// </para>
    /// </remarks>
    private List<DeviceEvent> Ingest(byte[] report)
    {
        _reads++;
        var payload = Frames.PayloadOf(report);
        if (!payload.IsEmpty || report is not [HidControl.InReportId, ..])
            _heard ??= report[..(report.AsSpan().LastIndexOfAnyExcept((byte)0) + 1)];
        if (report.AsSpan().SequenceEqual(_lastReport)) return [];
        _lastReport = report;
        if (payload.IsEmpty) return [];

        _buffer.AddRange(payload.ToArray());
        var (events, remainder) = EventParser.Consume(_buffer.ToArray());
        _buffer.Clear();
        // Keep the tail bounded: a reply we never complete must not grow
        // without limit.
        _buffer.AddRange(remainder.Length > 8192 ? remainder[^8192..] : remainder);
        return events;
    }

    /// <summary>
    /// Reads one category, returning as soon as that category's response
    /// completes rather than waiting out the whole window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A notification from another category that arrives meanwhile is kept
    /// for <see cref="ReadOnce"/>. It is the headset's only word on that
    /// change: a chat wheel notch dropped here leaves the app's count behind
    /// the wheel's, and the next notch then reads as a turn of several.
    /// </para>
    /// <para>
    /// Whatever is left unfinished from before is dropped as the request goes
    /// out. A reply that never finished will not finish now, and kept, it
    /// would swallow this one: the parser reads on from where it starts.
    /// </para>
    /// </remarks>
    /// <exception cref="KeyNotFoundException">The category has no read verb.</exception>
    public Dictionary<string, JsonElement> ReadCategory(string category, TimeSpan wait)
    {
        if (!Verbs.Readers.TryGetValue(category, out var verb))
            throw new KeyNotFoundException(
                $"no read verb for '{category}'; readable categories are "
                + string.Join(", ", Verbs.Readers.Keys.Order()));

        var sinceWrite = Stopwatch.GetElapsedTime(_lastSet);
        if (_lastSet != 0 && sinceWrite < AfterWrite) Thread.Sleep(AfterWrite - sinceWrite);
        _buffer.Clear();
        _transport.SendOutput(Frames.Build(verb, counter: NextCounter()));

        _heard = null;
        _reads = 0;
        Unanswered = null;
        var values = new Dictionary<string, JsonElement>();
        var clock = Stopwatch.StartNew();
        var sinceRead = new Stopwatch();
        while (clock.Elapsed < wait)
        {
            sinceRead.Restart();
            foreach (var evt in Receive())
            {
                if (evt.Category != category)
                {
                    if (evt.Kind == "UP") _setAside.Add(evt);
                    continue;
                }
                foreach (var pair in evt.Values) values[pair.Key] = pair.Value;
                if (evt.Kind == "OR") return values;
            }
            // Rounded up: a sleep is whole milliseconds, and rounding down
            // asks faster than the interval allows.
            double rest = Math.Ceiling((ReadInterval - sinceRead.Elapsed).TotalMilliseconds);
            if (rest > 0) Thread.Sleep((int)rest);
        }
        if (values.Count == 0 && _heard is not null) Unanswered = Convert.ToHexString(_heard);
        return values;
    }

    /// <summary>
    /// Reads the settings categories, one at a time, into one dictionary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Categories are requested one at a time. The headset drops requests
    /// sent back to back, so asking for all twelve up front loses about half
    /// the values and is no faster.
    /// </para>
    /// <para>
    /// Each category costs roughly 100 ms of the device's own reply latency,
    /// so a full read takes about 1.2 seconds and the protocol offers no way
    /// to shorten it. Use this for startup and explicit refresh, and keep a
    /// standing reader for notifications.
    /// </para>
    /// <para>
    /// The ten preset slots and four transmitter slots are not included by
    /// default: they are inventory rather than settings, and an empty slot
    /// answers nothing, so it costs the full window.
    /// </para>
    /// </remarks>
    /// <param name="wait">How long each category is given to answer.</param>
    /// <param name="categories">The categories to read: every headset's, unless given.</param>
    /// <param name="unanswered">
    /// Given, each category that gave no answer is added to it, with what
    /// arrived instead in hex, or null when nothing did; see <see cref="Unanswered"/>.
    /// </param>
    public Dictionary<string, JsonElement> ReadAll(
        TimeSpan? wait = null, IEnumerable<string>? categories = null,
        ICollection<(string Category, string? Heard)>? unanswered = null)
    {
        var window = wait ?? TimeSpan.FromMilliseconds(1200);
        var values = new Dictionary<string, JsonElement>();
        foreach (var category in categories ?? Verbs.SettingCategories)
        {
            var answer = ReadCategory(category, window);
            if (answer.Count == 0) unanswered?.Add((category, Unanswered));
            foreach (var pair in answer)
                values[pair.Key] = pair.Value;
        }
        return values;
    }

    /// <summary>
    /// Discards anything already buffered or waiting on the wire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ReadCategory"/> returns on the first matching response it
    /// sees, which can be a leftover from a previous exchange; without a
    /// drain, writing a setting and reading it straight back shows the old
    /// value. Do not call this from a background reader: anything discarded
    /// is a notification nobody else will see.
    /// </para>
    /// <para>
    /// A report the same as the one before it is the transmitter handing back
    /// its last report (see <see cref="Ingest"/>), so nothing new is waiting.
    /// The last one read is kept, so it is not taken in when handed back again.
    /// </para>
    /// </remarks>
    /// <returns>The number of reports discarded.</returns>
    public int Drain(int limit = 200)
    {
        _buffer.Clear();
        _setAside.Clear();
        int seen = 0;
        for (int i = 0; i < limit; i++)
        {
            try
            {
                var report = _transport.GetInput();
                if (Frames.PayloadOf(report).IsEmpty || report.AsSpan().SequenceEqual(_lastReport)) break;
                _lastReport = report;
                seen++;
            }
            catch (TransportException)
            {
                break;
            }
        }
        _buffer.Clear();
        return seen;
    }

    // -- writing -----------------------------------------------------------

    /// <exception cref="WritesDisabledException">The client is read-only.</exception>
    /// <exception cref="ArgumentException">
    /// The key is not confirmed as writable, or the value is not valid for it.
    /// </exception>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to set it to.</param>
    /// <param name="checkedAs">The key whose range the value is checked against, where it is not <paramref name="key"/>'s own; see <see cref="Registry.WireValue"/>.</param>
    public void Set(int key, object value, int? checkedAs = null)
    {
        string wire;
        try
        {
            if (!AllowWrites)
                throw new WritesDisabledException(
                    $"refusing to set 0x{key:x}: this client is read-only");
            wire = Registry.WireValue(key, value, checkedAs);
            _transport.SendOutput(Frames.SetKey(key, wire, NextCounter()));
            _lastSet = Stopwatch.GetTimestamp();
        }
        catch (Exception ex)
        {
            Wrote?.Invoke(new WriteAttempt(key, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", ex.Message, checkedAs));
            throw;
        }
        Wrote?.Invoke(new WriteAttempt(key, wire, null, checkedAs));
    }

    // -- the loudness level (a RACE command, not a set_kvp key) -------------

    /// <summary>
    /// Whether a command must be relayed to the headset over the air: true for
    /// a dock or transmitter, false only on the wired headset itself.
    /// </summary>
    private bool RelayThroughBase => Transmitters.PieceOf(ProductId) != Transmitters.Piece.Headset;

    /// <summary>How long to wait for a loudness reply before giving up.</summary>
    /// <remarks>A base only replies while it is being polled from when it enumerated, which the standing reader does; a missing reply is not proof the set did not land.</remarks>
    private static readonly TimeSpan LoudnessWait = TimeSpan.FromMilliseconds(700);

    /// <summary>
    /// Reads the headset's playback loudness level in decibels, or null when it
    /// gives no valid reply — which also means the feature is not usable here.
    /// </summary>
    public float? ReadLoudness(TimeSpan? wait = null) =>
        Exchange(LoudnessBoost.ReadFrame(RelayThroughBase),
            report => LoudnessBoost.LevelInReply(report), wait ?? LoudnessWait);

    /// <summary>
    /// Sets the headset's playback loudness level, held to the firmware's
    /// range. The headset sends no reply; confirm it with <see cref="ReadLoudness"/>.
    /// </summary>
    /// <exception cref="WritesDisabledException">The client is read-only.</exception>
    public void SetLoudness(float db)
    {
        if (!AllowWrites)
            throw new WritesDisabledException("refusing to set the loudness level: this client is read-only");
        _transport.SendOutput(LoudnessBoost.SetFrame(db, RelayThroughBase));
        _lastSet = Stopwatch.GetTimestamp();
    }

    /// <summary>Reads the gain the headset stores for mic monitoring, or null when it gives none; see <see cref="Sidetone"/>.</summary>
    public float? ReadSidetone(TimeSpan wait) =>
        Exchange(Sidetone.ReadFrame(RelayThroughBase), report => Sidetone.GainInReply(report), wait);

    /// <summary>Reads what the headset's buttons and switches have done, or null when it gives no reading; see <see cref="HeadsetInputs"/>.</summary>
    public InputReading? ReadInputs(TimeSpan wait) =>
        Exchange(HeadsetInputs.ReadFrame(RelayThroughBase), report => HeadsetInputs.InReply(report), wait);

    /// <summary>Reads the transmitter's light level as the transmitter gives it, or null when it gives none; see <see cref="TransmitterLight"/>.</summary>
    public int? ReadTransmitterLight(TimeSpan wait) =>
        Exchange(TransmitterLight.ReadFrame, report => TransmitterLight.LevelInReply(report), wait);

    /// <summary>
    /// Sends one request and watches the input report for a reply the matcher
    /// accepts, keeping any notification that arrives meanwhile.
    /// </summary>
    /// <remarks>
    /// The reply is a RACE command reply, not the text event protocol, so it is
    /// matched on the raw report before the report reaches the event parser. A
    /// report that is not the reply is taken in as usual, so a notification in
    /// it is not lost to the exchange.
    /// </remarks>
    private T? Exchange<T>(byte[] request, Func<byte[], T?> match, TimeSpan wait) where T : struct
    {
        var sinceWrite = Stopwatch.GetElapsedTime(_lastSet);
        if (_lastSet != 0 && sinceWrite < AfterWrite) Thread.Sleep(AfterWrite - sinceWrite);
        _transport.SendOutput(request);

        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < wait)
        {
            var report = _transport.GetInput();
            if (match(report) is { } hit) return hit;
            foreach (var evt in Ingest(report))
                if (evt.Kind == "UP") _setAside.Add(evt);
            Thread.Sleep(3);
        }
        return null;
    }

    /// <summary>A write was sent, or refused or failed before it could be. Raised on the thread that wrote.</summary>
    /// <remarks>
    /// The headset sends no reply to a write. Whether it took the value shows
    /// only in what it reports next, so a recording keeps the two side by side.
    /// </remarks>
    public event Action<WriteAttempt>? Wrote;

    public void Dispose()
    {
        if (_ownsTransport) _transport.Dispose();
    }
}
