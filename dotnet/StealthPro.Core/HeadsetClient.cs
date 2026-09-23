using System.Diagnostics;
using System.Text.Json;
using StealthPro.Core.Hid;
using StealthPro.Core.Protocol;

namespace StealthPro.Core;

public class WritesDisabledException : Exception
{
    public WritesDisabledException(string message) : base(message) { }
}

/// <summary>
/// Reads and writes headset settings. Reads are free; writes are gated.
///
/// Ported from stealthpro/client.py.
///
/// A note that matters in practice: Swarm II polls this same channel
/// thousands of times a second and drains the notification queue, so with
/// Swarm running the replies land in its process rather than ours. Only one
/// of the two can usefully talk to the headset at a time.
/// </summary>
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
    /// Open the device the headset is actually behind, by asking each one.
    ///
    /// <b>What is plugged in does not tell you where the headset is.</b> It
    /// pairs with one transmitter at a time and the others sit there opening
    /// cleanly and answering nothing. Measured with the dongle and the
    /// charging hub both connected: the dongle returned no values at all
    /// while the hub returned everything.
    ///
    /// This lives here rather than in the app because the probe needs it just
    /// as much. It did not have it for one session, and read all four
    /// transmitter slots as empty off a headset that was sitting there
    /// connected — a harness quietly addressing the wrong device is the exact
    /// failure this project keeps paying for.
    ///
    /// Returns null when devices are present but none has the headset;
    /// throws <see cref="DeviceNotFoundException"/> when there are none.
    /// </summary>
    public static HeadsetClient? Behind(bool allowWrites, out int present)
    {
        var candidates = HidTransport.Candidates();
        present = candidates.Count;
        if (present == 0)
            throw new DeviceNotFoundException("no Turtle Beach control collection is present");

        foreach (var device in candidates)
        {
            HeadsetClient? client = null;
            try
            {
                // <b>The client owns the handle it is given here.</b> It
                // used not to, and nothing else held it either: every device
                // asked and turned down was left open, and so was the one
                // kept, once the app let it go. Harmless once at startup;
                // not once the app re-asks every device each time the
                // headset goes quiet, which it does every few seconds for as
                // long as the headset is off.
                client = new HeadsetClient(allowWrites, new HidTransport(device.Path),
                    ownsTransport: true);
                client.Drain();
                if (client.ReadCategory("GSI", AskWindow).Count > 0) return client;
            }
            catch (Exception)
            {
                // Will not open, or will not talk: not the one. Try the next
                // rather than failing the whole connect.
            }
            client?.Dispose();
        }
        return null;
    }

    public string Device => _transport.Describe();

    /// <summary>
    /// Which Turtle Beach device this handle is on. The thing Windows shows
    /// is a transmitter, not the headset, and which one it is changes what
    /// the link means — so anything reporting a connection needs to know.
    /// </summary>
    public ushort ProductId => _transport.ProductId;

    private int NextCounter() => _counter = (_counter + 1) & 0xFFFF;

    // -- reading -----------------------------------------------------------

    /// <summary>
    /// One read from the headset, returning any complete events found.
    ///
    /// Long replies span several reports and an unrelated notification can
    /// land in the middle of one, so only the consumed prefix is dropped.
    /// </summary>
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
    /// Read one category. Returns as soon as that category's response
    /// arrives rather than burning the whole window.
    /// </summary>
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
    /// Read the settings categories into one dictionary.
    ///
    /// One category at a time, and that is deliberate. Asking for all twelve
    /// up front and listening once was tried, to collapse twelve round trips
    /// into one window: the headset drops requests sent back to back and the
    /// read came home with half the values missing, so it was both wrong and
    /// no faster. Measured, then reverted.
    ///
    /// Each category costs roughly 100ms of the device's own reply latency,
    /// so a full read is about 1.2 seconds and there is no protocol trick to
    /// shorten it. The answer is not to do full reads casually — keep a
    /// standing reader for notifications and use this for startup and for an
    /// explicit refresh.
    ///
    /// Not every readable category: the ten preset slots and four transmitter
    /// slots are inventory rather than settings, and an empty slot answers
    /// nothing at all so it costs the full window.
    /// </summary>
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
    /// Throw away anything already buffered or waiting on the wire.
    ///
    /// <see cref="ReadCategory"/> returns on the first matching response it
    /// sees, which can be a leftover from a previous exchange. Harmless when
    /// reads are seconds apart, actively wrong when they are not: writing a
    /// setting and immediately reading it back would show the value from a
    /// moment ago. Not safe to call from a background reader — anything
    /// discarded is a notification nobody was waiting for.
    /// </summary>
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

    public void Set(int key, object value)
    {
        if (!AllowWrites)
            throw new WritesDisabledException(
                $"refusing to set 0x{key:x}: this client is read-only");
        _transport.SendOutput(Frames.SetKey(key, value, NextCounter()));
    }

    public void Dispose()
    {
        if (_ownsTransport) _transport.Dispose();
    }
}
