using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using StealthPro.Core;
using StealthPro.Core.Audio;
using StealthPro.Core.Connection;
using StealthPro.Core.Hid;
using StealthPro.Core.Settings;

namespace StealthPro.App.Services;

/// <summary>Work asked of the headset while nothing is answering for it.</summary>
public sealed class HeadsetUnavailableException(string message) : Exception(message);

/// <summary>
/// The one thing in the app that talks to the headset.
///
/// Everything here exists because of how the device actually behaves rather
/// than because of how a service is usually built:
///
/// <b>One thread owns the HID handle.</b> The protocol is request/response
/// over a single report pair with no request ids to match on, so two callers
/// interleaving would read each other's replies. Every read, write and
/// inventory call is queued onto the owner thread.
///
/// <b>A standing reader, not polling.</b> The headset pushes notifications
/// when anything changes on the hardware: the chat wheel, the mode button,
/// the boom arm, battery. Reading continuously keeps the whole UI current
/// for free. A full read costs about 1.2 seconds because each of the twelve
/// categories waits on the device, so it is done once at connect and on an
/// explicit refresh, never casually.
///
/// <b>Values the person is touching are theirs.</b> Moving a slider writes
/// immediately, but the headset's own notification for that value arrives a
/// moment later carrying whatever it had before. Without an ownership window
/// the control jumps backwards under the finger. Learned in the web UI;
/// carried over deliberately.
/// </summary>
public sealed class HeadsetService : IDisposable
{
    /// <summary>How long a value the person just set is theirs, not the headset's.</summary>
    private static readonly TimeSpan OwnershipWindow = TimeSpan.FromMilliseconds(1500);

    /// <summary>
    /// Slowest we will tell the UI something changed. Notifications arrive
    /// in bursts and redrawing once per value is wasted work.
    /// </summary>
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(60);

    /// <summary>Closest together two writes to the same key are allowed to be.</summary>
    private const int WriteGapMs = 30;

    /// <summary>
    /// How often to ask the headset whether it is still there, and how long
    /// to wait for the answer.
    ///
    /// Cheap when it is: a category read returns the moment that category
    /// replies, so a live headset costs a few milliseconds. Only a dead one
    /// costs the whole window, and by then there is nothing else to do.
    /// </summary>
    private static readonly TimeSpan Beat = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan BeatWindow = TimeSpan.FromMilliseconds(700);

    /// <summary>How long to leave a silent transmitter alone before asking again.</summary>
    private static readonly TimeSpan SilentRetry = TimeSpan.FromSeconds(3);

    private static long BeatTicks => (long)(Beat.TotalSeconds * Stopwatch.Frequency);

    private readonly DispatcherQueue _ui;
    private readonly BlockingCollection<Job> _jobs = new(new ConcurrentQueue<Job>());
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<string, JsonElement> _values = new();
    private readonly ConcurrentDictionary<string, long> _owned = new();
    private readonly ConcurrentDictionary<int, int> _pending = new();
    private readonly ConcurrentDictionary<int, bool> _inFlight = new();
    private readonly ConcurrentDictionary<int, long> _lastWrite = new();
    private readonly Thread _worker;

    private const string Looking = "Looking for the headset…";

    private static readonly LinkWords Words = new(
        Looking, QuietDetail, UnreachableDetail, OffDetail, AdapterName);

    /// <summary>Decides the state. Only touched on the headset thread.</summary>
    private readonly LinkTracker _link;

    // Written on the headset thread and read on the UI thread.
    private volatile HeadsetStatus _status;
    private long _lastRaise;

    /// <summary>Which slot's block the lighting writes belong in. See OnTheWire.</summary>
    private int _lightingBlock = 0x400;

    /// <summary>Last seen 0x150, for spotting that it moved. See LinkFlagMoved.</summary>
    private int? _lastLinkFlag;
    private int _pendingRaise;

    private sealed record Job(Func<HeadsetClient, object?> Work, TaskCompletionSource<object?> Done);

    /// <param name="cabled">
    /// Whether the headset's own sound device is in Windows, which it is only
    /// while connected by its USB-C cable. See <see cref="LinkTracker"/>.
    /// </param>
    public HeadsetService(Func<bool>? cabled = null)
    {
        var running = Stopwatch.StartNew();
        _link = new LinkTracker(Words, () => running.Elapsed, cabled ?? (() => false));
        _status = _link.Status;
        _ui = DispatcherQueue.GetForCurrentThread();
        _worker = new Thread(Run) { IsBackground = true, Name = "headset" };
        _worker.Start();
    }

    /// <summary>Something in the value store moved. Raised on the UI thread.</summary>
    public event Action? Changed;

    /// <summary>The link came up, went away, or changed its story.</summary>
    public event Action<HeadsetStatus>? StatusChanged;

    /// <summary>The transmitter slots were read and said something new.</summary>
    public event Action? TransmittersChanged;

    /// <summary>
    /// The transmitters the headset last reported, kept after the device that
    /// reported them has gone. What is plugged in <i>now</i> is a separate
    /// question with its own answer; see <see cref="HidTransport.Candidates"/>.
    /// </summary>
    public IReadOnlyList<Transmitter> KnownTransmitters => _known;

    private volatile IReadOnlyList<Transmitter> _known = Array.Empty<Transmitter>();

    /// <summary>
    /// The chat wheel reported a position. Raised on the UI thread with the
    /// raw counter, which is deliberately not a mix value: see
    /// <see cref="WheelKey"/>.
    /// </summary>
    public event Action<int>? WheelMoved;

    /// <summary>
    /// The chat wheel's absolute counter. Kept out of the value store on
    /// purpose. It is a free-spinning encoder whose counter we cannot write,
    /// so it drifts away from the mix the app is applying; treating it as a
    /// value would make the mix slider snap to the wheel's physical position
    /// on the first notch. It is handled as movement instead, by the mix.
    /// </summary>
    public const int WheelKey = 0x510;

    public HeadsetStatus Status => _status;

    /// <summary>Everything the headset has reported, keyed by lowercase hex.</summary>
    public IReadOnlyDictionary<string, JsonElement> Values => _values;

    // -- reading the store -------------------------------------------------

    public bool TryGetNumber(string name, out int value)
    {
        value = 0;
        return Registry.ByName.TryGetValue(name, out var key)
            && TryGetNumberByKey(key.Key, out value);
    }

    public bool TryGetNumberByKey(int key, out int value)
    {
        value = 0;
        return _values.TryGetValue(Hex(key), out var element) && TryRead(element, out value);
    }

    public int GetNumber(string name, int fallback = 0) =>
        TryGetNumber(name, out int value) ? value : fallback;

    public string? GetText(string name)
    {
        if (!Registry.ByName.TryGetValue(name, out var key)) return null;
        if (!_values.TryGetValue(Hex(key.Key), out var element)) return null;
        return element.ValueKind == JsonValueKind.String
            ? element.GetString() : element.ToString();
    }

    private static bool TryRead(JsonElement element, out int value)
    {
        value = 0;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number: return element.TryGetInt32(out value);
            case JsonValueKind.String:
                return int.TryParse(element.GetString(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out value);
            case JsonValueKind.True: value = 1; return true;
            case JsonValueKind.False: value = 0; return true;
            default: return false;
        }
    }

    private static string Hex(int key) => key.ToString("x", CultureInfo.InvariantCulture);

    private static readonly string SoundLinkHex = Hex(LinkTracker.SoundLinkKey);

    private const string OffDetail = StateCopy.WhatOffOnCable + " " + StateCopy.FixOff;

    private void NoteSoundLink(JsonElement? was, JsonElement now)
    {
        string before = was is JsonElement e ? StealthPro.Core.Protocol.DeviceEvent.Render(e) : "-";
        string after = StealthPro.Core.Protocol.DeviceEvent.Render(now);
        if (before == after) return;
        _link.SoundLink(int.TryParse(after, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out int value) ? value : null);
        AppLog.Write($"headset sound link (0x230): {before} -> {after}");
    }

    // -- writing -----------------------------------------------------------

    /// <summary>
    /// Set a headset setting. The store moves at once and the write is
    /// queued, so the control the person is holding answers immediately
    /// rather than after a round trip.
    /// </summary>
    public void Set(string name, int value)
    {
        if (Registry.ByName.TryGetValue(name, out var key)) SetKey(key.Key, value);
    }

    /// <summary>
    /// Set a value, coalescing rapid changes to the same key.
    ///
    /// A slider dragged across its range raises a change per pixel. Sending
    /// every one builds a backlog the headset answers long after the finger
    /// has stopped, so only the latest value for a key is ever in flight and
    /// writes to one key are spaced out. The last value always lands, which
    /// is the part that matters.
    /// </summary>
    public void SetKey(int key, int value)
    {
        SetKeyLocally(key, value);
        _pending[key] = value;
        if (!_inFlight.TryAdd(key, true)) return;

        var write = Post(client =>
        {
            _inFlight.TryRemove(key, out _);
            if (!_pending.TryRemove(key, out int latest)) return true;

            if (_lastWrite.TryGetValue(key, out long previous))
            {
                int since = (int)((Stopwatch.GetTimestamp() - previous) * 1000 / Stopwatch.Frequency);
                if (since < WriteGapMs) Thread.Sleep(WriteGapMs - since);
            }
            client.Set(OnTheWire(key), latest);
            _lastWrite[key] = Stopwatch.GetTimestamp();
            return true;
        });

        // A write failed before it ran never clears its own mark, and with the
        // mark left behind that key could not be written again until restart.
        // Clear it here; the latest value stays pending for the next write.
        write.ContinueWith(failed =>
        {
            _ = failed.Exception;
            _inFlight.TryRemove(key, out _);
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    /// <summary>
    /// Move a value in the store without writing it, for changes the headset
    /// makes on its own behalf. Selecting an equaliser preset is the case
    /// that needs it: the headset reloads all ten bands itself, so writing
    /// them would be ten pointless round trips, but the UI should not sit on
    /// the old curve for the 1.2 seconds a full read would take to confirm
    /// it.
    /// </summary>
    public void SetKeyLocally(int key, int value)
    {
        string hex = Hex(key);
        _values[hex] = JsonSerializer.SerializeToElement(value);
        _owned[hex] = Stopwatch.GetTimestamp()
            + (long)(OwnershipWindow.TotalSeconds * Stopwatch.Frequency);
        Raise();
    }

    // -- work on the owner thread ------------------------------------------

    /// <summary>
    /// Run something against the headset on the thread that owns it. Presets,
    /// transmitters and anything else needing a round trip goes through here
    /// rather than opening a second handle.
    /// </summary>
    public Task<T> Post<T>(Func<HeadsetClient, T> work)
    {
        var done = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try { _jobs.Add(new Job(client => work(client), done)); }
        catch (Exception ex) { done.TrySetException(ex); }
        return Unwrap<T>(done.Task);
    }

    private static async Task<T> Unwrap<T>(Task<object?> task) => (T)(await task)!;

    /// <summary>A full re-read of every settings category. Around 1.2 seconds.</summary>
    public Task Refresh() => Post(client =>
    {
        Merge(client.ReadAll(), authoritative: true);
        return true;
    });

    // -- the owner thread --------------------------------------------------

    private void Run()
    {
        HeadsetClient? client = null;
        bool primed = false;
        long nextBeat = 0;
        int unanswered = 0;
        int beats = 0;
        string carrying = "";
        string product = "";
        bool elsewhere = false;

        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                if (client is null)
                {
                    // Only on the way down from a working link. Announcing
                    // it on every retry made the header flicker between
                    // "Connecting" and whatever was actually wrong, three
                    // seconds apart, so the real state was never on screen
                    // long enough to read.
                    Publish(_link.Reconnecting());
                    client = Open(out int present);
                    if (client is null)
                    {
                        Forget();
                        Unreachable();
                        _stopping.Token.WaitHandle.WaitOne(SilentRetry);
                        continue;
                    }
                    primed = false;
                }

                var route = LinkTracker.RouteOf(client.ProductId);
                string adapter = AdapterName(route);

                if (!primed)
                {
                    // Clear anything left on the wire before the first read,
                    // or a stale reply answers the first question asked.
                    client.Drain();
                    var everything = client.ReadAll();

                    // Opening the transmitter proves only that the
                    // transmitter is there. If nothing came back, the headset
                    // is off or out of range, and saying so is the whole
                    // point of this state.
                    if (everything.Count == 0)
                    {
                        Forget();
                        Unreachable();
                        _stopping.Token.WaitHandle.WaitOne(SilentRetry);

                        // Let go, so the next try asks every device again.
                        // See the note on silence in the heartbeat below.
                        LetGo(ref client);
                        continue;
                    }

                    Merge(everything, authoritative: true);
                    primed = true;
                    unanswered = 0;
                    beats = 0;
                    nextBeat = Stopwatch.GetTimestamp() + BeatTicks;
                    var held = Carrying(client);
                    carrying = held?.Name ?? adapter;
                    product = held?.Product ?? client.ProductId.ToString("X4", CultureInfo.InvariantCulture);
                    elsewhere = held is not null && !held.Value.Here;

                    // Ask on the way in, not only when it changes. The state
                    // this catches is one the app can start up already in.
                    _lastLinkFlag = null;
                    LinkFlagMoved();
                    Publish(_link.Answering(elsewhere, route, adapter, carrying, client.Device, product));
                }

                // Queued work first: a slider write waiting behind a poll
                // feels like lag, and there is never much of it.
                while (_jobs.TryTake(out var job)) Serve(job, client);
                Publish(_link.Refresh());
                Publish(_link.Cable(elsewhere, route, adapter, carrying, client.Device, product));

                var events = client.ReadOnce();
                if (events.Count > 0)
                {
                    foreach (var evt in events) Merge(evt.Values, authoritative: false);
                    unanswered = 0;
                    nextBeat = Stopwatch.GetTimestamp() + BeatTicks;
                }
                else
                {
                    Thread.Sleep(4);
                }

                // A headset switched off sends nothing, and so does one
                // sitting on the desk untouched: silence on its own proves
                // nothing either way, so ask something cheap now and then.
                // One missed answer is not enough — a reply can be lost
                // behind a long read — but two in a row is.
                if (primed && Stopwatch.GetTimestamp() >= nextBeat)
                {
                    // <b>One request per beat, alternating.</b> This used to
                    // ask GSI and then Inf back to back, and
                    // <see cref="HeadsetClient.ReadAll"/> has documented since
                    // the port that the headset drops requests sent back to
                    // back. Measured with the headset connected and working:
                    // the header cycled Connected, Settings unavailable, Not
                    // connected, roughly every two beats, for as long as you
                    // cared to watch.
                    //
                    // Inf carries 0x150 and only has to be timely, not
                    // immediate — it moves when the headset changes
                    // transmitter, which is a thing a person does with their
                    // hands. Every other beat is soon enough.
                    var beat = client.ReadCategory(
                        ++beats % 2 == 0 ? "Inf" : "GSI", BeatWindow);
                    nextBeat = Stopwatch.GetTimestamp() + BeatTicks;

                    // Whatever it said on the way past is worth keeping —
                    // battery and volume live in the GSI block, 0x150 in Inf.
                    if (beat.Count > 0) Merge(beat, authoritative: false);

                    // <b>Silence is counted on its own.</b> This used to sit in
                    // an else-branch of the announce test, so a beat that
                    // answered but had nothing new to say still incremented
                    // the counter — one dropped read followed by a good one
                    // was enough to report the headset as gone.
                    unanswered = beat.Count > 0 ? 0 : unanswered + 1;

                    if (unanswered >= 2)
                    {
                        primed = false;
                        Forget();
                        Unreachable();

                        // <b>Silence is a reason to look again, not to wait
                        // by the same door.</b> This kept the device that had
                        // gone quiet and went on asking it, every three
                        // seconds, for ever. The headset switched off on the
                        // Charging Dock and came back on the USB Transmitter —
                        // which answered, measured the same minute — and the
                        // app sat on "Settings unavailable", never asking
                        // anything but the dock. Letting go makes the next
                        // try ask every device, the way a first connect does.
                        LetGo(ref client);
                    }
                    // Reading the slots costs four round trips, so it is done
                    // when 0x150 says something moved, and on a slow timer in
                    // case a change is ever missed — not every beat.
                    else if (beat.Count > 0 && (LinkFlagMoved() || beats % 16 == 0))
                    {
                        var held = Carrying(client);
                        carrying = held?.Name ?? adapter;
                        product = held?.Product ?? client.ProductId.ToString("X4", CultureInfo.InvariantCulture);
                        elsewhere = held is not null && !held.Value.Here;
                        // <b>No following.</b> This used to close the device
                        // and ask every transmitter again whenever the headset
                        // selected a different one, on the theory that the
                        // controls would have moved with it. They do not: the
                        // device answering went on answering, every time it was
                        // measured, and the reopen only ever found it again
                        // after four seconds of "Connecting" on screen.
                        Publish(_link.Answering(elsewhere, route, adapter, carrying, client.Device, product));
                    }
                }
            }
            catch (DeviceNotFoundException)
            {
                Drop(ref client, ref primed,
                    "Nothing of the headset's is plugged in.");
            }
            catch (TransportException)
            {
                Drop(ref client, ref primed, "Lost contact with the headset.");
            }
            catch (Exception ex)
            {
                // Not a device going away but a fault of ours, which would
                // otherwise retry every second and a half with nothing to
                // say why.
                NoteFault("headset loop", ex);
                Drop(ref client, ref primed, ex.Message);
            }
        }

        client?.Dispose();
    }

    private string _lastFault = "";

    /// <summary>One line in the log per fault, not one per retry.</summary>
    private void NoteFault(string where, Exception ex)
    {
        string frame = ex.StackTrace?.Split('\n', 2)[0].Trim() ?? "";
        string line = $"{where} failed: {ex.GetType().Name}: {ex.Message} {frame}";
        if (line == _lastFault) return;
        _lastFault = line;
        AppLog.Write(line);
    }

    /// <summary>
    /// Close the device without reporting anything, so the next pass through
    /// the loop opens whichever device answers.
    /// </summary>
    private static void LetGo(ref HeadsetClient? client)
    {
        client?.Dispose();
        client = null;
    }

    private static void Serve(Job job, HeadsetClient client)
    {
        try { job.Done.TrySetResult(job.Work(client)); }
        catch (Exception ex) { job.Done.TrySetException(ex); }
    }

    /// <summary>
    /// Let the device go, and fail anything queued rather than leaving it
    /// waiting on a reply that will never come.
    /// </summary>
    private void Drop(ref HeadsetClient? client, ref bool primed, string detail)
    {
        ushort? was = client?.ProductId;
        client?.Dispose();
        client = null;
        primed = false;
        Forget();

        // What is actually plugged in is a question with an answer: ask it,
        // rather than taking one device going for all of them going.
        Publish(_link.Lost(was, Plugged(), detail));
        FailWaiting(detail);
        _stopping.Token.WaitHandle.WaitOne(1500);
    }

    /// <summary>
    /// Fail everything queued. Done whenever nothing answers for the headset,
    /// so work asked for then fails at once, as its callers expect, rather
    /// than waiting for the headset to come back and then all running
    /// together ahead of the chat wheel.
    /// </summary>
    private void FailWaiting(string why)
    {
        while (_jobs.TryTake(out var waiting))
            waiting.Done.TrySetException(new HeadsetUnavailableException(why));
    }

    /// <summary>
    /// Take values in, except ones the person is currently holding. A full
    /// read is authoritative and overrides that.
    /// </summary>
    private void Merge(IReadOnlyDictionary<string, JsonElement> incoming, bool authoritative)
    {
        bool moved = false;
        long now = Stopwatch.GetTimestamp();
        string wheel = Hex(WheelKey);
        foreach (var pair in incoming)
        {
            if (pair.Key == wheel)
            {
                if (TryRead(pair.Value, out int position))
                    _ui.TryEnqueue(() => WheelMoved?.Invoke(position));
                continue;
            }
            if (!authoritative && _owned.TryGetValue(pair.Key, out long until) && until > now)
                continue;
            _owned.TryRemove(pair.Key, out _);
            bool had = _values.TryGetValue(pair.Key, out var existing);
            if (had
                && existing.ValueKind == pair.Value.ValueKind
                && existing.ToString() == pair.Value.ToString()) continue;
            if (pair.Key == SoundLinkHex) NoteSoundLink(had ? existing : null, pair.Value);
            _values[pair.Key] = pair.Value.Clone();
            moved = true;
        }
        if (moved) Raise();
    }

    /// <summary>
    /// Where a lighting brightness actually has to be written.
    ///
    /// <b>The two LED keys are not addresses, they are roles.</b> Each
    /// transmitter slot is a block — 0x400, 0x420, 0x440, 0x460 — and a
    /// slot's two brightnesses sit at +1 and +2 inside its own block. So
    /// 0x401 and 0x402 are slot one's, not "the LEDs".
    ///
    /// The app reads the <i>active</i> transmitter's brightnesses and showed
    /// them under 0x401/0x402, while writing 0x401/0x402 — which went to
    /// whichever transmitter happened to be listed first. With the dock in
    /// slot two and the dongle in slot one, the sliders showed the dock and
    /// dimmed the dongle. Measured: writing 0x421 moved the dock's control
    /// values and 0x401 moved the dongle's.
    ///
    /// Reads and writes now agree by translating at the wire, so the keys can
    /// go on meaning "the LEDs of the transmitter you are using".
    /// </summary>
    private int OnTheWire(int key) =>
        key is 0x401 or 0x402 ? _lightingBlock + (key - 0x400) : key;

    /// <summary>
    /// Throw away everything the headset told us.
    ///
    /// Values outlive the headset they came from, and a stale reading shown
    /// as a live one is worse than no reading: the controls look right and
    /// are wrong. When the link goes, so do they, and every control that
    /// depends on one has nothing to draw rather than something untrue.
    /// </summary>
    private void Forget()
    {
        _link.Forget();
        if (_values.IsEmpty && _owned.IsEmpty) return;
        _values.Clear();
        _owned.Clear();
        Raise();
    }

    /// <summary>
    /// Open the device the headset is actually behind, by asking each one.
    ///
    /// <b>What is plugged in does not tell you where the headset is.</b> The
    /// headset pairs with one transmitter at a time, and the others sit there
    /// opening cleanly and answering nothing — measured with the dongle and
    /// the charging hub both connected: the dongle returned no values while
    /// the hub returned everything. Picking by product id would have opened
    /// the dongle and reported the headset as off while it was sitting there
    /// connected.
    ///
    /// So each candidate is asked one cheap question and the first that
    /// replies is the one we keep. Returns null when devices are present but
    /// none of them has the headset; throws when there is nothing at all.
    /// </summary>
    private static HeadsetClient? Open(out int present) =>
        HeadsetClient.Behind(allowWrites: true, out present);

    private static string AdapterName(Route route) => route switch
    {
        Route.ChargingHub => "Charging Dock",
        Route.UsbTransmitter => "USB Transmitter",
        Route.DirectUsb => "Headset, over USB-C",
        _ => "Transmitter",
    };

    /// <summary>
    /// Which transmitter the headset has selected, and whether that is the one
    /// we are talking through. See <see cref="Transmitters.Active"/>.
    /// </summary>
    private (string Name, string Product, bool Here)? Carrying(HeadsetClient client)
    {
        var all = Transmitters.ReadAll(client, TimeSpan.FromMilliseconds(500));

        // A dropped read comes back as four empty slots. Keeping that
        // would wipe the list the moment a reply went missing, so only a
        // read that found something replaces what we knew.
        if (all.Any(t => t.Paired))
        {
            _known = all.Where(t => t.Paired).ToList();
            _ui.TryEnqueue(() => TransmittersChanged?.Invoke());
        }

        // The dock's two lighting brightnesses come back inside its slot,
        // and nothing was taking them out — so both LED rows sat greyed
        // with a dash, on a dock that had been reporting them all along.
        // The reply had them; we were the ones not listening.
        var active = all.FirstOrDefault(t => t.Active);
        if (active is not null) _lightingBlock = 0x400 + 0x20 * (active.Slot - 1);

        var lighting = Transmitters.Lighting(all);
        if (lighting.Count > 0)
        {
            var values = new Dictionary<string, JsonElement>();
            foreach (var (key, value) in lighting)
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                    values[key] = JsonSerializer.SerializeToElement(number);
            if (values.Count > 0) Merge(values, authoritative: false);
        }

        if (string.IsNullOrEmpty(active?.Kind)) return null;

        // <b>Compared, not inferred.</b> The slot says which transmitter
        // the headset selected and the transport says which one we opened;
        // if they are the same piece of hardware, the headset is on this
        // one. Both halves are things the device stated outright.
        bool here = ushort.TryParse(active!.ProductId,
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out ushort selected)
                    && selected == client.ProductId;

        return (active.Kind, active.ProductId, here);
    }

    /// <summary>
    /// Has 0x150 moved since we last looked?
    ///
    /// <b>That it changed is usable; what it equals is not.</b> It was read as
    /// "2 means the headset is on this transmitter", and on that reading the
    /// app sat on "Not connected" for as long as you cared to watch, over a
    /// headset playing music with its wheels working. Measured at that moment,
    /// with the Charging Dock the only transmitter plugged in, the only one
    /// paired, and the one the headset had selected: 0x150 read 1.
    ///
    /// The best available reading is that it is the slot number the headset is
    /// using — the dock sat in slot 1 and it said 1, and the earlier 2-to-1
    /// measurement fits a dock in slot 2 just as well. That is a theory, and
    /// nothing here depends on it. What it is used for is the one thing it
    /// certainly supports: when it moves, something happened, so go and read
    /// the slots, which say outright which transmitter is selected.
    /// </summary>
    private bool LinkFlagMoved()
    {
        if (!TryGetNumberByKey(LinkState.OnThisTransmitterKey, out int now)) return false;
        if (now == _lastLinkFlag) return false;
        bool first = _lastLinkFlag is null;
        _lastLinkFlag = now;
        return !first;
    }

    /// <summary>
    /// Say that the headset's settings cannot be reached, and fail whatever
    /// was waiting for them.
    /// </summary>
    private void Unreachable()
    {
        Publish(_link.Unreachable(Plugged() ?? []));
        FailWaiting(_link.Status.Detail);
    }

    /// <summary>The product ids of everything plugged in, or null if they could not be listed.</summary>
    private List<ushort>? Plugged()
    {
        try
        {
            return HidTransport.Candidates().Select(device => device.ProductId).ToList();
        }
        catch (Exception ex)
        {
            NoteFault("listing devices", ex);
            return null;
        }
    }

    private const string QuietDetail =
        StateCopy.WhatOff + " " + StateCopy.FixOff + " " + StateCopy.FallbackOff;

    /// <summary>
    /// <b>Only what is true in every way into this state.</b> The first
    /// version said "if you cannot hear it", to somebody listening to it: the
    /// headset had lost only its settings, with its sound playing through the
    /// transmitter that was left. The other ways — no sound at all, or the
    /// headset switched off — are covered by the last sentence, not led with.
    ///
    /// Switching off and on comes before CrossPlay because CrossPlay moves
    /// only the sound. The settings stay on the transmitter the headset was
    /// switched on with, and when that one has gone, only switching it off and
    /// on brings them to one that is here.
    /// </summary>
    private const string UnreachableDetail =
        StateCopy.WhatUnreachable + " " + StateCopy.FixUnreachable + " "
        + StateCopy.FallbackUnreachable;

    /// <summary>Show a new status, when the tracker decided one.</summary>
    private void Publish(HeadsetStatus? next)
    {
        if (next is null) return;
        var was = _status;

        // The detail is wording and changes without the state changing, so
        // only a change of state is recorded.
        if (was.Link != next.Link || was.Route != next.Route || was.Adapter != next.Adapter
            || was.Product != next.Product || was.ControlVia != next.ControlVia
            || was.NoSound != next.NoSound)
            AppLog.Write("headset: " + next.Link
                + (next.Adapter.Length > 0 ? $" via {next.Adapter}" : "")
                + (next.ControlVia.Length > 0 ? $", settings via {next.ControlVia}" : "")
                + (next.NoSound ? ", no sound" : ""));

        _status = next;
        _ui.TryEnqueue(() => StatusChanged?.Invoke(next));
    }

    /// <summary>Tell the UI, at most once per coalesce window.</summary>
    private void Raise()
    {
        if (Interlocked.Exchange(ref _pendingRaise, 1) == 1) return;

        long due = _lastRaise + (long)(CoalesceWindow.TotalSeconds * Stopwatch.Frequency);
        long now = Stopwatch.GetTimestamp();
        int delay = now >= due ? 0 : (int)((due - now) * 1000 / Stopwatch.Frequency);

        void Fire()
        {
            _lastRaise = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref _pendingRaise, 0);
            Changed?.Invoke();
        }

        if (delay <= 0) _ui.TryEnqueue(Fire);
        else _ = Task.Delay(delay).ContinueWith(_ => _ui.TryEnqueue(Fire));
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _jobs.CompleteAdding();

        // Only once the thread has stopped using them. It can be in the
        // middle of a full read, which takes over a second; disposing under
        // it made its next step throw, and its error handling throw again,
        // which ends the process with a crash on the way out. If it is still
        // busy, the process exit takes it and them together.
        if (!_worker.Join(TimeSpan.FromSeconds(2))) return;
        _stopping.Dispose();
        _jobs.Dispose();
    }
}

