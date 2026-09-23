using System.Diagnostics;
using System.Text.Json;
using StealthPro.Core.Hid;
using StealthPro.Core.Protocol;
using StealthPro.Core.Settings;

namespace StealthPro.Core;

public class WritesDisabledException : Exception
{
    public WritesDisabledException(string message) : base(message) { }
}

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
    private int _counter;

    public bool AllowWrites { get; }

    /// <param name="ownsTransport">
    /// Close <paramref name="transport"/> when this client is disposed. A
    /// transport handed in is otherwise the caller's to close.
    /// </param>
    public HeadsetClient(bool allowWrites = false, IHidTransport? transport = null,
        bool ownsTransport = false)
    {
        AllowWrites = allowWrites;
        _ownsTransport = transport is null || ownsTransport;
        _transport = transport ?? new HidTransport();
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
    /// <param name="askLast">A product id to ask after the others, when its answer is in doubt.</param>
    /// <returns>
    /// The client for the device that answered, or null when devices are
    /// present but none has the headset.
    /// </returns>
    /// <exception cref="DeviceNotFoundException">No candidate device is present.</exception>
    public static HeadsetClient? Behind(bool allowWrites, out int present, ushort? askLast = null)
    {
        var candidates = HidTransport.Candidates().OrderBy(d => d.ProductId == askLast).ToList();
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
                client = new HeadsetClient(allowWrites, new HidTransport(device.Path),
                    ownsTransport: true);
                client.Drain();
                if (client.ReadCategory("GSI", AskWindow).Count > 0) return client;
            }
            catch (Exception)
            {
                // Cannot be opened or does not answer: try the next rather
                // than failing the whole connect.
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

    private int NextCounter() => _counter = (_counter + 1) & 0xFFFF;

    // -- reading -----------------------------------------------------------

    /// <summary>
    /// Performs one read from the headset and returns any complete events found.
    /// </summary>
    /// <remarks>
    /// Long replies span several reports and an unrelated notification can
    /// land in the middle of one, so only the consumed prefix is dropped.
    /// </remarks>
    public IReadOnlyList<DeviceEvent> ReadOnce()
    {
        var payload = Frames.PayloadOf(_transport.GetInput());
        if (payload.IsEmpty) return Array.Empty<DeviceEvent>();

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
    /// <exception cref="KeyNotFoundException">The category has no read verb.</exception>
    public Dictionary<string, JsonElement> ReadCategory(string category, TimeSpan wait)
    {
        if (!Verbs.Readers.TryGetValue(category, out var verb))
            throw new KeyNotFoundException(
                $"no read verb for '{category}'; readable categories are "
                + string.Join(", ", Verbs.Readers.Keys.Order()));

        _transport.SendOutput(Frames.Build(verb, counter: NextCounter()));

        var values = new Dictionary<string, JsonElement>();
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < wait)
        {
            foreach (var evt in ReadOnce())
            {
                if (evt.Category != category) continue;
                foreach (var pair in evt.Values) values[pair.Key] = pair.Value;
                if (evt.Kind == "OR") return values;
            }
            Thread.Sleep(3);
        }
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
    public Dictionary<string, JsonElement> ReadAll(
        TimeSpan? wait = null, IEnumerable<string>? categories = null)
    {
        var window = wait ?? TimeSpan.FromMilliseconds(1200);
        var values = new Dictionary<string, JsonElement>();
        foreach (var category in categories ?? Verbs.SettingCategories)
            foreach (var pair in ReadCategory(category, window))
                values[pair.Key] = pair.Value;
        return values;
    }

    /// <summary>
    /// Discards anything already buffered or waiting on the wire.
    /// </summary>
    /// <remarks>
    /// <see cref="ReadCategory"/> returns on the first matching response it
    /// sees, which can be a leftover from a previous exchange; without a
    /// drain, writing a setting and reading it straight back shows the old
    /// value. Do not call this from a background reader: anything discarded
    /// is a notification nobody else will see.
    /// </remarks>
    /// <returns>The number of reports discarded.</returns>
    public int Drain(int limit = 200)
    {
        _buffer.Clear();
        int seen = 0;
        for (int i = 0; i < limit; i++)
        {
            try
            {
                var payload = Frames.PayloadOf(_transport.GetInput());
                if (payload.IsEmpty) break;
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
    public void Set(int key, object value)
    {
        if (!AllowWrites)
            throw new WritesDisabledException(
                $"refusing to set 0x{key:x}: this client is read-only");
        _transport.SendOutput(Frames.SetKey(key, Registry.WireValue(key, value), NextCounter()));
    }

    public void Dispose()
    {
        if (_ownsTransport) _transport.Dispose();
    }
}
