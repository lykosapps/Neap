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
/// The one thing in the app that talks to the headset: it owns the connection,
/// reads the headset's notifications, runs queued requests and tracks the link.
/// </summary>
/// <remarks>
/// <para>
/// One thread owns the HID handle. The protocol is request/response over a
/// single report pair with no request ids to match on, so two callers
/// interleaving would read each other's replies. Every read, write and
/// inventory call is queued onto the owner thread.
/// </para>
/// <para>
/// A standing reader replaces polling. The headset pushes notifications when
/// anything changes on the hardware (the chat wheel, the mode button, the boom
/// arm, battery), so reading continuously keeps the whole UI current. A full
/// read costs about 1.2 seconds because each of the twelve categories waits on
/// the device, so it is done once at connect and on an explicit refresh only.
/// </para>
/// <para>
/// Values the person is touching are theirs. Moving a slider writes
/// immediately, but the headset's own notification for that value arrives a
/// moment later carrying whatever it had before. Without an ownership window
/// the control jumps backwards under the finger.
/// </para>
/// </remarks>
public sealed class HeadsetService : IDisposable
{
    /// <summary>How long a value the person just set is theirs, not the headset's.</summary>
    private static readonly TimeSpan OwnershipWindow = TimeSpan.FromMilliseconds(1500);

    /// <summary>
    /// Shortest gap between two change notifications to the UI. Notifications
    /// arrive in bursts, and redrawing once per value is wasted work.
    /// </summary>
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(60);

    /// <summary>Closest together two writes to the same key are allowed to be.</summary>
    private const int WriteGapMs = 30;

    /// <summary>
    /// How often to ask the headset whether it is still there.
    /// <see cref="BeatWindow"/> is how long to wait for the answer.
    /// </summary>
    /// <remarks>
    /// A category read returns the moment that category replies, so a live
    /// headset costs a few milliseconds. Only a silent one costs the whole
    /// window, and then there is nothing else to do.
    /// </remarks>
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

    private static string Looking => Strings.Get("Headset_Looking");

    private static readonly LinkWords Words = new(
        Looking, QuietDetail, UnreachableDetail, OffDetail, AdapterName);

    /// <summary>Decides the state. Only touched on the headset thread.</summary>
    private readonly LinkTracker _link;

    // Written on the headset thread and read on the UI thread.
    private volatile HeadsetStatus _status;
    private long _lastRaise;

    /// <summary>Which slot's block the lighting writes belong in. See <see cref="OnTheWire"/>.</summary>
    private int _lightingBlock = 0x400;

    /// <summary>Last seen 0x150, for spotting that it moved. See <see cref="LinkFlagMoved"/>.</summary>
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

    /// <summary>The link came up, went away, or changed its detail. Raised on the UI thread.</summary>
    public event Action<HeadsetStatus>? StatusChanged;

    /// <summary>
    /// The transmitter slots were read and at least one was paired, so
    /// <see cref="KnownTransmitters"/> was replaced. Raised on the UI thread.
    /// </summary>
    public event Action? TransmittersChanged;

    /// <summary>
    /// The transmitters the headset last reported, kept after the device that
    /// reported them has gone. What is plugged in now is a separate question;
    /// see <see cref="HidTransport.Candidates"/>.
    /// </summary>
    public IReadOnlyList<Transmitter> KnownTransmitters => _known;

    private volatile IReadOnlyList<Transmitter> _known = Array.Empty<Transmitter>();

    /// <summary>
    /// The chat wheel reported a position. Raised on the UI thread with the
    /// raw counter, which is deliberately not a mix value: see
    /// <see cref="WheelKey"/>.
    /// </summary>
    public event Action<int>? WheelMoved;

    /// <summary>The chat wheel's absolute counter.</summary>
    /// <remarks>
    /// Kept out of the value store. It is a free-spinning encoder whose counter
    /// cannot be written, so it drifts away from the mix the app is applying;
    /// stored as a value, it would snap the mix slider to the wheel's physical
    /// position on the first notch. The mix handles it as movement instead.
    /// </remarks>
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

    private static string OffDetail => StateCopy.WhatOffOnCable + " " + StateCopy.FixOff;

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

    /// <summary>Set a value, coalescing rapid changes to the same key.</summary>
    /// <remarks>
    /// A slider dragged across its range raises a change per pixel. Sending
    /// every one builds a backlog the headset answers long after the finger
    /// has stopped, so only the latest value for a key is ever in flight and
    /// writes to one key are spaced by <see cref="WriteGapMs"/>. The last value
    /// always lands.
    /// </remarks>
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

        // A write that fails before it runs never clears its own in-flight
        // mark, which would block that key until restart. Clear it here; the
        // latest value stays pending for the next write.
        write.ContinueWith(failed =>
        {
            _ = failed.Exception;
            _inFlight.TryRemove(key, out _);
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    /// <summary>
    /// Move a value in the store without writing it, for changes the headset
    /// makes on its own behalf.
    /// </summary>
    /// <remarks>
    /// Selecting an equaliser preset needs it: the headset reloads all ten
    /// bands itself, so writing them would be ten pointless round trips, but
    /// the UI should not sit on the old curve for the 1.2 seconds a full read
    /// takes to confirm it.
    /// </remarks>
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
                    // Announced only on the way down from a working link.
                    // Announcing it on every retry flickers the header between
                    // "Connecting" and whatever is actually wrong, three
                    // seconds apart, so the real state is never on screen
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
                    // is off or out of range, which is what this state reports.
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
                    // One request per beat, alternating GSI and Inf. The
                    // headset drops requests sent back to back (see
                    // HeadsetClient.ReadAll); asking both in one beat makes a
                    // connected, working headset cycle through Connected,
                    // Settings unavailable and Not connected roughly every
                    // two beats.
                    //
                    // Inf carries 0x150 and only has to be timely, not
                    // immediate — it moves when the headset changes
                    // transmitter, which a person does by hand. Every other
                    // beat is soon enough.
                    var beat = client.ReadCategory(
                        ++beats % 2 == 0 ? "Inf" : "GSI", BeatWindow);
                    nextBeat = Stopwatch.GetTimestamp() + BeatTicks;

                    // Whatever it said on the way past is worth keeping —
                    // battery and volume live in the GSI block, 0x150 in Inf.
                    if (beat.Count > 0) Merge(beat, authoritative: false);

                    // Silence is counted on its own. Any answer resets the
                    // counter, even one with nothing new in it; otherwise one
                    // dropped read followed by a good one reports the headset
                    // as gone.
                    unanswered = beat.Count > 0 ? 0 : unanswered + 1;

                    if (unanswered >= 2)
                    {
                        primed = false;
                        Forget();
                        Unreachable();

                        // Silence is a reason to look again, not to keep asking
                        // the same device. A headset switched off on the
                        // Charging Dock can come back on the USB Transmitter,
                        // and asking only the dock leaves the app on "Settings
                        // unavailable" for ever. Letting go makes the next try
                        // ask every device, the way a first connect does.
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
                        // Keep the open device when the headset selects a
                        // different transmitter. The controls do not move with
                        // the selection: the device answering goes on answering
                        // (measured every time), so reopening only finds it
                        // again after four seconds of "Connecting" on screen.
                        Publish(_link.Answering(elsewhere, route, adapter, carrying, client.Device, product));
                    }
                }
            }
            catch (DeviceNotFoundException)
            {
                Drop(ref client, ref primed, Strings.Get("Headset_NothingPluggedIn"));
            }
            catch (TransportException)
            {
                Drop(ref client, ref primed, Strings.Get("Headset_LostContact"));
            }
            catch (Exception ex)
            {
                // A fault of ours, not a device going away. Log it, or it
                // retries every second and a half with nothing to say why.
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

    /// <summary>Where a lighting brightness actually has to be written.</summary>
    /// <remarks>
    /// <para>
    /// The two LED keys are roles, not addresses. Each transmitter slot is a
    /// block (0x400, 0x420, 0x440, 0x460), and a slot's two brightnesses sit at
    /// +1 and +2 inside its own block, so 0x401 and 0x402 on the wire are slot
    /// one's. Measured with the Charging Dock in slot two and the USB
    /// Transmitter in slot one: writing 0x421 moved the dock's values and 0x401
    /// moved the transmitter's.
    /// </para>
    /// <para>
    /// The app shows the active transmitter's brightnesses under 0x401/0x402,
    /// so writes are translated here to the active slot's block. The keys then
    /// mean "the LEDs of the transmitter you are using" for reads and writes
    /// alike.
    /// </para>
    /// </remarks>
    private int OnTheWire(int key) =>
        key is 0x401 or 0x402 ? _lightingBlock + (key - 0x400) : key;

    /// <summary>Throw away everything the headset told us.</summary>
    /// <remarks>
    /// A stale reading shown as a live one is worse than no reading: the
    /// controls look right and are wrong. When the link goes, so do the
    /// values, and every control that depends on one has nothing to draw
    /// rather than something untrue.
    /// </remarks>
    private void Forget()
    {
        _link.Forget();
        if (_values.IsEmpty && _owned.IsEmpty) return;
        _values.Clear();
        _owned.Clear();
        Raise();
    }

    /// <summary>Open the device the headset is actually behind, by asking each one.</summary>
    /// <remarks>
    /// <para>
    /// What is plugged in does not say where the headset is. The headset pairs
    /// with one transmitter at a time, and the others open cleanly and answer
    /// nothing. Measured with the USB Transmitter and the Charging Dock both
    /// connected: the transmitter returned no values while the dock returned
    /// everything, so picking by product id would report a connected headset
    /// as off.
    /// </para>
    /// <para>
    /// Each candidate is asked one cheap question and the first that replies
    /// is kept.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The client, or null when devices are present but none of them has the
    /// headset.
    /// </returns>
    /// <exception cref="DeviceNotFoundException">Nothing is plugged in at all.</exception>
    private static HeadsetClient? Open(out int present) =>
        HeadsetClient.Behind(allowWrites: true, out present);

    private static string AdapterName(Route route) => route switch
    {
        Route.ChargingDock => Strings.Get("Adapter_ChargingDock"),
        Route.UsbTransmitter => Strings.Get("Adapter_UsbTransmitter"),
        Route.DirectUsb => Strings.Get("Adapter_Cable"),
        _ => Strings.Get("Adapter_Transmitter"),
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

        // The dock's two lighting brightnesses come back inside its slot and
        // nowhere else, so take them out here or both LED rows have no value.
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

        // Compared, not inferred. The slot says which transmitter the headset
        // selected and the transport says which one we opened; if they are
        // the same piece of hardware, the headset is on this one. Both halves
        // are things the device states outright.
        bool here = ushort.TryParse(active!.ProductId,
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out ushort selected)
                    && selected == client.ProductId;

        return (active.Kind, active.ProductId, here);
    }

    /// <summary>
    /// Whether 0x150 has moved since the last look. The first reading only
    /// records it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// That it changed is usable; what it equals is not. It does not mean "2
    /// is on this transmitter": with the Charging Dock the only transmitter
    /// plugged in, the only one paired and the one the headset had selected,
    /// over a headset playing music with its wheels working, 0x150 read 1.
    /// </para>
    /// <para>
    /// It may be the slot number the headset is using (the dock sat in slot 1
    /// and it said 1, and an earlier 2-to-1 change fits a dock in slot 2), but
    /// nothing here depends on that. When it moves, something happened, so the
    /// slots are read, and they say outright which transmitter is selected.
    /// </para>
    /// </remarks>
    private bool LinkFlagMoved()
    {
        if (!TryGetNumberByKey(LinkState.TransmitterFlagKey, out int now)) return false;
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

    private static string QuietDetail =>
        StateCopy.WhatOff + " " + StateCopy.FixOff + " " + StateCopy.FallbackOff;

    /// <summary>The detail shown when the headset's settings cannot be reached.</summary>
    /// <remarks>
    /// <para>
    /// It leads only with what is true in every case of this state. The
    /// headset may have lost only its settings, with its sound still playing
    /// through the transmitter that is left, so it does not say "if you cannot
    /// hear it". No sound at all, or the headset switched off, is covered by
    /// the last sentence, not led with.
    /// </para>
    /// <para>
    /// Switching off and on comes before CrossPlay because CrossPlay moves
    /// only the sound. The settings stay on the transmitter the headset was
    /// switched on with, and when that one has gone, only switching it off and
    /// on brings them to one that is here.
    /// </para>
    /// </remarks>
    private static string UnreachableDetail =>
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
        // it makes its next step throw, and its error handling throw again,
        // which crashes the process on the way out. If it is still busy, the
        // process exit takes it and them together.
        if (!_worker.Join(TimeSpan.FromSeconds(2))) return;
        _stopping.Dispose();
        _jobs.Dispose();
    }
}

