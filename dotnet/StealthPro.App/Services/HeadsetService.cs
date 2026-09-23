using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using StealthPro.Core;
using StealthPro.Core.Audio;
using StealthPro.Core.Hid;
using StealthPro.Core.Settings;

namespace StealthPro.App.Services;

/// <summary>
/// Where the link has got to.
///
/// <b>These are four different things and they used to be two.</b> What
/// Windows shows us is a transmitter, never the headset, so "the device
/// opened" and "the headset is talking" are separate facts — and the app
/// reported the first as though it were the second. A charging hub plugged
/// in with the headset switched off answered every read with nothing, every
/// control on every page sat blank or at zero, and the header said Connected
/// in green. Everything looked like our bug.
/// </summary>
public enum Link
{
    /// <summary>Nothing of Turtle Beach's is plugged in at all.</summary>
    Absent,
    /// <summary>Something is plugged in and we are asking it.</summary>
    Connecting,
    /// <summary>
    /// A transmitter is plugged in, and the headset's settings cannot be
    /// reached through it. See <see cref="HeadsetStatus.SettingsUnreachable"/>.
    /// </summary>
    Silent,
    /// <summary>
    /// A transmitter is plugged in and the headset is not on it — switched
    /// off or out of range, as far as anyone can tell. Or, over its cable,
    /// known to be switched off. See <see cref="HeadsetStatus.NotConnected"/>
    /// and <see cref="HeadsetStatus.SwitchedOff"/>.
    /// </summary>
    Quiet,
    /// <summary>The headset is answering.</summary>
    Connected,
}

/// <summary>Work asked of the headset while nothing is answering for it.</summary>
public sealed class HeadsetUnavailableException(string message) : Exception(message);

/// <summary>How the headset is being reached.</summary>
public enum Route
{
    /// <summary>Nothing open, or a product id we do not recognise.</summary>
    Unknown,
    /// <summary>Over 2.4GHz, through the dock that also charges a battery.</summary>
    ChargingHub,
    /// <summary>Over 2.4GHz, through the small USB-A dongle.</summary>
    UsbTransmitter,
    /// <summary>Straight to the headset chip over USB-C.</summary>
    DirectUsb,
}

/// <summary>
/// What is plugged in, and whether the headset behind it is talking.
/// <paramref name="Adapter"/> is what to call the thing Windows has, which
/// is not the headset: "Charging Dock", "USB Transmitter", or the headset
/// itself when it is cabled up directly.
/// </summary>
public sealed record HeadsetStatus(
    Link Link, Route Route, string Adapter, string Detail, string Product = "",
    string ControlVia = "", bool NoSound = false)
{
    // NoSound: connected — the settings answer — but no transmitter is sending
    // the headset sound. Only ever set alongside Link.Connected. See
    // HeadsetService.SoundLinkDown for how it is read and why it waits.

    // ControlVia names the transmitter carrying the headset's settings and
    // chat wheel when it is not the one carrying its sound — the Charging
    // Dock, with the headset's sound on the USB Transmitter. Empty when one
    // transmitter carries everything. It is named from the device that
    // actually answered, never inferred: the USB Transmitter answered for
    // itself too, once, with the headset switched on straight onto it.

    /// <summary>
    /// A transmitter is plugged in, and nothing answers for the headset's
    /// settings.
    ///
    /// <b>Reached by unplugging the transmitter the headset's settings were
    /// on</b>, with another still plugged in: either the settings alone went,
    /// and the sound is still playing through the one that is left, or both
    /// went with it. The two look the same from here, so they are one state,
    /// and everything that treats it specially asks this.
    ///
    /// <b>Not the same as the headset being switched off</b>, although that
    /// is the same silence too. It was folded in here at first, and told
    /// somebody whose headset had just gone off that its buttons "work as
    /// normal" and its settings were merely out of reach. What tells the two
    /// apart is what happened just before — see <see cref="NotConnected"/>.
    ///
    /// It used to be two — a calm mode for the USB Transmitter on its own and
    /// an amber "settings unavailable" for the dock — on the belief that the
    /// USB Transmitter could never carry settings. It can: the headset keeps
    /// its controls on whichever transmitter it was switched on with. Which
    /// one is left over says nothing about which of these happened.
    /// </summary>
    public bool SettingsUnreachable => Link == Link.Silent;

    /// <summary>
    /// The headset stopped answering on a transmitter that is still plugged
    /// in, or never answered at all: switched off or out of range.
    ///
    /// Told apart from <see cref="SettingsUnreachable"/> by what came first.
    /// A transmitter that goes quiet while it stays plugged in has lost its
    /// headset; one that is unplugged took the settings with it. With no
    /// history — the app starting with the headset already off, which is
    /// what a login usually is — this is the likelier of the two, and the
    /// one assumed.
    /// </summary>
    public bool NotConnected => Link == Link.Quiet;

    /// <summary>
    /// Plugged in with its USB-C cable and switched off, which over the cable
    /// can be seen for certain. It still answers there — it keeps a
    /// connection for charging — so the battery is a real reading. See
    /// HeadsetService.OffOnCable.
    /// </summary>
    public bool SwitchedOff => Link == Link.Quiet && Route == Route.DirectUsb;
}

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

    private HeadsetStatus _status =
        new(Link.Connecting, Route.Unknown, "", "Looking for the headset…");
    private long _lastRaise;

    /// <summary>Which slot's block the lighting writes belong in. See OnTheWire.</summary>
    private int _lightingBlock = 0x400;

    /// <summary>Last seen 0x150, for spotting that it moved. See LinkFlagMoved.</summary>
    private int? _lastLinkFlag;
    private int _pendingRaise;

    private sealed record Job(Func<HeadsetClient, object?> Work, TaskCompletionSource<object?> Done);

    /// <param name="cabled">
    /// Whether the headset is connected by its USB-C cable, which decides
    /// whether its wireless sound link means anything; see SoundLinkDown.
    /// </param>
    public HeadsetService(Func<bool>? cabled = null)
    {
        _cabled = cabled ?? (() => false);
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

    private IReadOnlyList<Transmitter> _known = Array.Empty<Transmitter>();

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

    /// <summary>
    /// 0x230, the headset's sound link: 2 while a transmitter is sending it
    /// sound, 0 while none is.
    ///
    /// <b>It was read as "switching off", and it is not.</b> It was taken to
    /// be a flag that drops as the headset powers down, and drove "Headset
    /// off" in the header. Watched through a day of changes: it sits at 0 for
    /// about fifteen seconds after the headset is switched on, while the sound
    /// comes up; it drops to 0 when the transmitter carrying the sound is
    /// unplugged, with the headset on, its settings answering through the
    /// other transmitter, no sound, and that transmitter's light amber; and it
    /// never dropped ahead of a real switch-off. So it says whether sound is
    /// arriving over the wireless link.
    ///
    /// <b>Not every silent state.</b> Through the one earlier episode with
    /// both transmitter lights amber and no sound anywhere, it read 2; that
    /// state is still invisible to the app. And over the cable it says
    /// nothing about what is heard — see SoundLinkDown.
    /// </summary>
    private static readonly string SoundLinkKey = Hex(0x230);

    private bool _soundSeenUp;
    private long _soundDownAt;
    private long _connectedAt;
    private readonly Func<bool> _cabled;

    /// <summary>When the headset was first seen answering over its cable with no sound device.</summary>
    private long _offSince;

    /// <summary>
    /// How long that has to last before it counts as off: switching on brings
    /// the charging connection back a moment before the sound device.
    /// </summary>
    private static readonly TimeSpan OffGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The headset is answering over its cable, and its sound device has been
    /// gone for longer than <see cref="OffGrace"/>: it is switched off.
    ///
    /// <b>Switched off, it keeps a connection for charging.</b> With the cable
    /// in, switching the headset off took its speakers and microphone out of
    /// Windows and left a control device behind that went on answering, and
    /// the header said "Headset connected" over a headset that was off. The
    /// sound device going is what tells the two apart. Only asked when the
    /// device answering is the headset's own.
    /// </summary>
    private bool OffOnCable()
    {
        if (_cabled()) { _offSince = 0; return false; }
        long now = Stopwatch.GetTimestamp();
        if (_offSince == 0) _offSince = now;
        return now - _offSince >= (long)(OffGrace.TotalSeconds * Stopwatch.Frequency);
    }

    private const string OffDetail = StateCopy.WhatOffOnCable + " " + StateCopy.FixOff;

    /// <summary>
    /// How long it may take for sound to arrive after connecting — it took
    /// about fifteen seconds after a switch-on — before its absence counts.
    /// </summary>
    private static readonly TimeSpan SoundStartGrace = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a drop may last before it counts: long enough for CrossPlay
    /// to hand the sound between transmitters without a flash of "No sound".
    /// </summary>
    private static readonly TimeSpan SoundDropGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Connected, and no sound is arriving — after the grace periods above.
    /// Only on the 2.4GHz routes.
    ///
    /// <b>Never while the headset is connected by its cable.</b> The flag is
    /// the wireless link's: with the cable in, it dropped to 0 when the
    /// Charging Dock was unplugged, and the header said "No sound" for four
    /// seconds over music playing through the cable. Over the cable the sound
    /// does not depend on it, and anywhere else Windows sends it is the
    /// routing warning's to say.
    /// </summary>
    private bool SoundLinkDown(Route route)
    {
        if (route is not (Route.ChargingHub or Route.UsbTransmitter)) return false;
        if (_cabled()) return false;
        if (!TryGetNumberByKey(0x230, out int flag) || flag != 0) return false;
        long since = _soundSeenUp ? _soundDownAt : _connectedAt;
        var grace = _soundSeenUp ? SoundDropGrace : SoundStartGrace;
        return since != 0
               && Stopwatch.GetTimestamp() - since >= (long)(grace.TotalSeconds * Stopwatch.Frequency);
    }

    private void NoteSoundLink(JsonElement? was, JsonElement now)
    {
        string before = was is JsonElement e ? StealthPro.Core.Protocol.DeviceEvent.Render(e) : "-";
        string after = StealthPro.Core.Protocol.DeviceEvent.Render(now);
        if (before == after) return;
        if (int.TryParse(after, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            if (value > 0) _soundSeenUp = true;
            else _soundDownAt = Stopwatch.GetTimestamp();
        }
        AppLog.Write($"headset sound link (0x230): {before} -> {after}");
    }

    /// <summary>
    /// Look again at whether sound is arriving. Called round the loop, because
    /// the answer changes with time passing as well as with values arriving.
    /// </summary>
    private void RefreshSound()
    {
        var current = _status;
        if (current.Link != Link.Connected) return;
        if (SoundLinkDown(current.Route) == current.NoSound) return;
        SetStatus(current.Link, current.Route, current.Adapter, current.Detail,
            current.Product, current.ControlVia);
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
                    if (_status.Link == Link.Connected)
                        SetStatus(Link.Connecting, Route.Unknown, "",
                            "Looking for the headset…");
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

                var route = RouteOf(client.ProductId);
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
                    Announce(elsewhere, route, adapter, carrying, client.Device, product);
                }

                // Queued work first: a slider write waiting behind a poll
                // feels like lag, and there is never much of it.
                while (_jobs.TryTake(out var job)) Serve(job, client);
                RefreshSound();

                // Over its cable, looked at each time round: what decides it,
                // Windows' view of the headset's sound device, changes
                // without the headset saying anything. See OffOnCable.
                if (route == Route.DirectUsb)
                {
                    if (!_cabled())
                    {
                        if (OffOnCable() && !_status.SwitchedOff)
                            SetStatus(Link.Quiet, route, adapter, OffDetail, product);
                    }
                    else if (_status.Link != Link.Connected)
                    {
                        // The sound device came back, after Announce held off
                        // for it or the headset was off: now it is connected.
                        _offSince = 0;
                        Announce(elsewhere, route, adapter, carrying, client.Device, product);
                    }
                }

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
                        Announce(elsewhere, route, adapter, carrying, client.Device, product);
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

        // <b>Losing one device does not mean losing them all.</b> This used to
        // report "nothing plugged in" whenever the handle went away, so
        // unplugging the dock while the USB transmitter stayed in said the
        // machine was empty — measured, three seconds of it, before the retry
        // found the transmitter and corrected itself. What is actually
        // plugged in is a question with an answer; ask it.
        bool nothing = false;
        try
        {
            var plugged = HidTransport.Candidates();
            nothing = plugged.Count == 0;

            // The device we were talking to has gone, and something else is
            // still here: the headset's settings left with a transmitter,
            // rather than the headset going quiet. Remembered until it answers
            // again. See HeadsetStatus.SettingsUnreachable.
            if (!nothing && was is ushort gone && plugged.All(d => d.ProductId != gone))
                _lostWithTransmitter = true;
        }
        catch (Exception ex)
        {
            NoteFault("listing devices", ex);
            nothing = true;
        }

        // <b>Gone for a moment is not gone.</b> Switching the headset on or
        // off over its cable restarts its USB connection: the device goes,
        // and comes back about three seconds later. The header said "Nothing
        // plugged in", in red, for all of it. So for a few seconds after
        // losing a device it was talking to, the app is still looking.
        long now = Stopwatch.GetTimestamp();
        if (was is not null) _lostAt = now;
        bool settling = _lostAt != 0
            && now - _lostAt < (long)(AbsentGrace.TotalSeconds * Stopwatch.Frequency);
        bool absent = nothing && !settling;

        SetStatus(
            absent ? Link.Absent : Link.Connecting,
            Route.Unknown, "",
            absent ? detail : "Looking for the headset…");
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
            if (pair.Key == SoundLinkKey) NoteSoundLink(had ? existing : null, pair.Value);
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
        _soundSeenUp = false;
        _soundDownAt = 0;
        _offSince = 0;
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

    /// <summary>
    /// Which product id is which was recorded the wrong way round for most of
    /// this project; see <see cref="Transmitters.Hardware"/> for how it was
    /// settled. 0x229B is the charging hub, not the dongle.
    /// </summary>
    private static Route RouteOf(ushort productId) =>
        Transmitters.PieceOf(productId) switch
        {
            Transmitters.Piece.Dock => Route.ChargingHub,
            Transmitters.Piece.Transmitter => Route.UsbTransmitter,
            Transmitters.Piece.Headset => Route.DirectUsb,
            _ => Route.Unknown,
        };

    private static string AdapterName(Route route) => route switch
    {
        Route.ChargingHub => "Charging Dock",
        Route.UsbTransmitter => "USB Transmitter",
        Route.DirectUsb => "Headset, over USB-C",
        _ => "Transmitter",
    };

    /// <summary>
    /// What the headset says is carrying it, which is not always what is
    /// answering us. See <see cref="Transmitters.Active"/>.
    /// </summary>
    /// <summary>
    /// Which transmitter the headset has selected, and whether that is the one
    /// we are talking through.
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
    /// Connected, or connected to something that is not listening. Both
    /// answers come from the same read, so they are announced together rather
    /// than left to whichever caller remembers to check.
    /// </summary>
    private void Announce(bool elsewhere, Route route, string adapter,
        string carrying, string device, string product)
    {
        // Answering over its cable with no sound device: switched off, or
        // switching on and not finished. Neither is connected, so hold off
        // until one or the other is certain. See OffOnCable.
        if (route == Route.DirectUsb && !_cabled())
        {
            if (OffOnCable())
                SetStatus(Link.Quiet, route, adapter, OffDetail, product);
            else if (_status.Link != Link.Connecting && !_status.SwitchedOff)
                SetStatus(Link.Connecting, Route.Unknown, "", "Looking for the headset…");
            return;
        }
        _offSince = 0;

        if (!elsewhere)
        {
            SetStatus(Link.Connected, route, carrying, device, product);
            return;
        }

        // <b>Sound on one transmitter, controls through another — and all of
        // it working, in either direction.</b> The headset keeps its controls
        // on the transmitter it was switched on with, and CrossPlay moves only
        // its sound. Measured both ways round: switched on with the Charging
        // Dock and moved to the USB Transmitter, the dock carried the chat
        // wheel and noise cancellation; switched on with the USB Transmitter
        // and moved to the dock, the USB Transmitter did — asked one device at
        // a time, the dock answered nothing and the USB Transmitter answered
        // for everything.
        //
        // Each direction had been reported as a fault. The first as
        // sound-only, the second as "not connected, nothing changed here will
        // reach it", over a headset carrying a Teams meeting whose noise
        // cancellation switched audibly from this very screen.
        //
        // So the device answering is the headset, connected. Its sound is
        // wherever it selected, which is what Windows has to be pointed at.
        SetStatus(Link.Connected, RouteOfProduct(product), carrying, device, product,
            controlVia: adapter);
    }

    /// <summary>What sort of thing a slot's product id is, if it parses.</summary>
    private static Route RouteOfProduct(string product) =>
        ushort.TryParse(product, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out ushort id)
            ? RouteOf(id) : Route.Unknown;

    /// <summary>
    /// Say that the headset's settings cannot be reached, the same way every
    /// time, whichever way it happened.
    ///
    /// The transmitter plugged in is named when there is exactly one, so the
    /// wrong-output check still has something to compare Windows with.
    /// </summary>
    private void Unreachable()
    {
        var link = _lostWithTransmitter ? Link.Silent : Link.Quiet;
        string detail = _lostWithTransmitter ? UnreachableDetail : QuietDetail;

        var here = TransmittersPlugged();
        if (here.Count == 1)
        {
            var route = RouteOf(here[0]);
            SetStatus(link, route, AdapterName(route), detail, here[0].ToString("X4", CultureInfo.InvariantCulture));
        }
        else
        {
            SetStatus(link, Route.Unknown, "", detail);
        }
        FailWaiting(detail);
    }

    /// <summary>
    /// Set when the device the app was talking to is unplugged while another
    /// stays; cleared the moment the headset answers again.
    /// </summary>
    private bool _lostWithTransmitter;

    /// <summary>When the device the app was talking to last went away.</summary>
    private long _lostAt;

    /// <summary>How long "Nothing plugged in" waits after losing a device. See Drop.</summary>
    private static readonly TimeSpan AbsentGrace = TimeSpan.FromSeconds(6);

    private const string QuietDetail =
        StateCopy.WhatOff + " " + StateCopy.FixOff + " " + StateCopy.FallbackOff;

    private List<ushort> TransmittersPlugged()
    {
        try
        {
            return HidTransport.Candidates()
                .Select(device => device.ProductId)
                .Where(id => RouteOf(id) is Route.ChargingHub or Route.UsbTransmitter)
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            NoteFault("listing transmitters", ex);
            return new List<ushort>();
        }
    }

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

    private void SetStatus(Link link, Route route, string adapter, string detail,
        string product = "", string controlVia = "")
    {
        if (link == Link.Connected && _status.Link != Link.Connected)
            _connectedAt = Stopwatch.GetTimestamp();
        bool noSound = link == Link.Connected && SoundLinkDown(route);

        var status = new HeadsetStatus(link, route, adapter, detail, product, controlVia, noSound);
        if (link == Link.Connected) _lostWithTransmitter = false;
        if (_status == status) return;

        // The detail is wording and changes without the state changing, so
        // only a change of state is recorded.
        if (_status.Link != link || _status.Route != route || _status.Adapter != adapter
            || _status.Product != product || _status.ControlVia != controlVia
            || _status.NoSound != noSound)
            AppLog.Write("headset: " + link
                + (adapter.Length > 0 ? $" via {adapter}" : "")
                + (controlVia.Length > 0 ? $", settings via {controlVia}" : "")
                + (noSound ? ", no sound" : ""));

        _status = status;
        _ui.TryEnqueue(() => StatusChanged?.Invoke(status));
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
        _worker.Join(TimeSpan.FromSeconds(2));
        _stopping.Dispose();
        _jobs.Dispose();
    }
}

