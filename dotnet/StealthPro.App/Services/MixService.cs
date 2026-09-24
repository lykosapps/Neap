using System.Diagnostics;
using System.Globalization;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using StealthPro.Core.Connection;
using StealthPro.Core.Mix;
using StealthPro.Core.Pretend;

namespace StealthPro.App.Services;

/// <summary>An application that could be the chat one, as the picker shows it.</summary>
public sealed record ChatCandidate(string Process, string Display, bool Playing);

/// <summary>
/// The game/chat crossfade, and the chat wheel that drives it.
/// </summary>
/// <remarks>
/// <para>
/// The mix itself is <see cref="SessionMix"/>: no audio engine, no virtual
/// cable, just the chat application's session volume against everything
/// else's. This class is everything between the hardware and that.
/// </para>
/// <para>
/// The wheel is relative, not a position. It is a free-spinning encoder and
/// the headset reports an absolute 0-100 counter we cannot write. With the mix
/// set to 70 on screen the counter is still wherever the wheel physically
/// sits, so treating its reading as the mix would snap the dial to the
/// wheel's position (usually near zero) on the first notch. Only the movement
/// between readings is applied.
/// </para>
/// <para>
/// The ends are anchored. The counter clamps at 0 and 100, so once it is
/// pinned there it reports no further movement. Without anchoring, a mix that
/// had drifted a few points short of an extreme could never reach it.
/// </para>
/// <para>
/// The centre has a detent. The wheel steps in fives and rarely lands on
/// exactly 50. A plain snap window is not enough: a fast turn straight through
/// centre skips it, and a value snapped to 50 slides off again on the next
/// notch, so the beep and the number disagree. The detent catches a crossing,
/// and it holds.
/// </para>
/// <para>
/// Everything here runs on the UI thread: the dial and keyboard directly,
/// the wheel and the link through the headset service's events.
/// </para>
/// </remarks>
public sealed class MixService : IDisposable
{
    /// <summary>Centre detent half-width, in mix points.</summary>
    private const int Detent = 4;

    /// <summary>
    /// Biggest step still treated as a wheel notch. The dial dragged across
    /// the whole range passes through centre rather than sticking to it.
    /// </summary>
    private const int NotchLimit = 15;

    private readonly HeadsetService _headset;

    private IMixEngine? _mix;
    private int? _wheelLast;
    private bool _wheelDark;
    private bool _wheelResync;
    private int? _lastApplied;
    private bool _detentHeld;
    private long _lastWheelApply;

    public MixService(HeadsetService headset)
    {
        _headset = headset;
        _headset.WheelMoved += OnWheel;
        _headset.StatusChanged += OnLink;
    }

    /// <summary>The mix moved, or the engine started or stopped.</summary>
    public event Action? Changed;

    public bool Running => _mix is not null;

    public int Mix => _lastApplied ?? 50;

    public SessionMixStatus? Status => _mix?.Status;

    public IReadOnlyList<string> ChatApps
    {
        get => AppSettings.Current.ChatApps;
        set
        {
            AppSettings.Update(s => s.ChatApps = value.ToList());
            if (_mix is not null) _mix.ChatApps = value;
            Changed?.Invoke();
        }
    }

    // -- lifecycle ---------------------------------------------------------

    /// <summary>
    /// Put back anything a previous run left turned down, before anything
    /// else. An unclean exit is the one case where somebody is left with a
    /// quiet application and no idea why.
    /// </summary>
    /// <remarks>A pretend run holds no real volume down, so it has nothing to put back.</remarks>
    public static Task Recover() => Pretend.Active ? Task.CompletedTask : Task.Run(SessionMix.Recover);

    public void Start()
    {
        if (_mix is not null) return;
        _mix = Pretend.Windows?.CreateMix(ChatApps) ?? new SessionMix(ChatApps);
        _mix.Start();
        _lastApplied = _mix.SetMix(_lastApplied ?? 50);
        Changed?.Invoke();
    }

    public void Stop()
    {
        var going = _mix;
        _mix = null;
        going?.Dispose();
        Changed?.Invoke();
    }

    // -- setting the mix ---------------------------------------------------

    /// <summary>
    /// Set the mix. The one path both the dial and the wheel take, so the
    /// detent behaves identically whichever moved it.
    /// </summary>
    /// <param name="value">The mix wanted, 0 to 100.</param>
    /// <param name="why">
    /// What moved it, for the log, so a mix that moves with nobody touching
    /// anything can be traced to its cause.
    /// </param>
    /// <returns>The mix applied, or null when the mix is not running.</returns>
    public int? Apply(int value, string why = "dial")
    {
        int want = Math.Clamp(value, 0, 100);
        int? previous = _lastApplied;

        bool crossed = previous is int was
            && (was - 50) * (want - 50) < 0
            && Math.Abs(want - was) <= NotchLimit;
        bool near = Math.Abs(want - 50) <= Detent;

        int target;
        bool cue = false;
        if ((near || crossed) && !_detentHeld) { target = 50; _detentHeld = true; cue = true; }
        else if (near) target = 50;
        else { target = want; _detentHeld = false; }

        var engine = _mix;
        if (engine is null) return null;

        int applied = engine.SetMix(target);
        if (cue && applied == 50) CentreCue();
        Note(why, previous, applied);
        _lastApplied = applied;
        Changed?.Invoke();
        return applied;
    }

    private readonly object _noteGate = new();
    private System.Threading.Timer? _noteTimer;
    private string? _noteKind;
    private string _noteWhy = "";
    private int? _noteFrom;
    private int _noteTo;
    private int _noteSteps;
    private DateTime _noteStarted;
    private long _noteAt;

    /// <summary>
    /// Log one line per burst, not per step: a drag or a spin of the wheel is
    /// dozens of changes, and what matters is where it went and why.
    /// </summary>
    /// <remarks>
    /// The line is written when the burst ends and stamped with when it began.
    /// Written at the start, a drag out to 62 and back to centre would read as
    /// a move to 62 that never finished. The cause kept is the first step's;
    /// for the wheel that includes the count's first jump, which is what gives
    /// a lurch away.
    /// </remarks>
    private void Note(string why, int? from, int to)
    {
        string kind = why.Split(' ')[0];
        long now = Stopwatch.GetTimestamp();
        lock (_noteGate)
        {
            bool sameBurst = _noteKind == kind && now - _noteAt < Stopwatch.Frequency * 2;
            if (!sameBurst)
            {
                WriteNote();
                _noteKind = kind;
                _noteWhy = why;
                _noteFrom = from;
                _noteSteps = 0;
                _noteStarted = DateTime.Now;
            }
            _noteTo = to;
            _noteSteps++;
            _noteAt = now;
            _noteTimer ??= new System.Threading.Timer(_ => { lock (_noteGate) WriteNote(); });
            _noteTimer.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Write the burst in hand, if there is one. Under the note gate.</summary>
    private void WriteNote()
    {
        if (_noteKind is null) return;
        _noteKind = null;
        string from = _noteFrom is int f ? f.ToString(CultureInfo.InvariantCulture) : "-";
        AppLog.Write($"mix: {from} -> {_noteTo} ({_noteWhy}"
            + (_noteSteps > 1 ? $", {_noteSteps} steps)" : ")"), _noteStarted);
    }

    private void OnWheel(int position)
    {
        int? from = _wheelLast;

        // A reconnect is a starting point, never a movement. The wheel is a
        // free-spinning encoder, not a control with a real position, and the
        // headset's count behind it resets when the headset is switched on.
        // Measured: 45 before an idle shut-off, 0 after switching back on,
        // the wheel untouched throughout. Adopting that reading would move
        // the mix to "Game only" and silence chat with nobody touching
        // anything. Only the wheel moving may move the mix.
        if (from is null || _mix is null)
        {
            _wheelLast = position;
            return;
        }

        // Nor is the first count to differ after no sound. See OnLink.
        if (_wheelResync && position != from)
        {
            _wheelLast = position;
            _wheelResync = false;
            return;
        }
        if (position == from) return;

        // Bound how often the wheel is allowed to drive the mix. It is turned
        // far faster than there is any point applying. The starting point is
        // left where it was, so a reading skipped here is carried into the
        // next step rather than lost.
        long now = Stopwatch.GetTimestamp();
        if (now - _lastWheelApply < Stopwatch.Frequency / 33) return;
        _lastWheelApply = now;

        _wheelLast = position;
        Apply(Follow(Mix, from.Value, position), $"wheel {from.Value}->{position}");
    }

    /// <summary>
    /// Where the mix goes when the wheel's count moves from one value to
    /// another.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The count and the mix are two scales that drift apart: the dial and
    /// the keyboard move the mix without the wheel, and the count resets at
    /// power-on. Adding the difference keeps them apart for good, and then the
    /// wheel cannot reach an end: its count stops at 0 while the mix is still
    /// at 20. Snapping to the end when the count gets near it is no answer; it
    /// lurches from Balanced to Game only in one notch.
    /// </para>
    /// <para>
    /// So each step covers the same share of what is left. Turning toward game
    /// moves the mix by the fraction of the remaining count just travelled;
    /// the same toward chat. Both arrive at the end together, with no jump, and
    /// once the two agree this is exactly the difference.
    /// </para>
    /// </remarks>
    private static int Follow(int mix, int from, int to)
    {
        double next = to < from
            ? (from <= 0 ? mix : mix * (double)to / from)
            : (from >= 100 ? mix : 100 - (100 - mix) * (100.0 - to) / (100 - from));
        return (int)Math.Round(Math.Clamp(next, 0, 100));
    }

    /// <summary>
    /// Forget where the wheel was when the link goes, or when the wheel's
    /// clicks stop reaching the app.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Otherwise the next reading is treated as a movement from a position
    /// the wheel may have left long ago: the headset can be switched to
    /// another transmitter, or turned off and moved, entirely out of sight.
    /// Forgetting makes the first reading after a reconnect a starting point
    /// again, which is the only honest thing it can be.
    /// </para>
    /// <para>
    /// No sound is treated the same, although the link stays up. While no
    /// transmitter is sending the headset sound, the wheel's clicks are lost
    /// with it, and when the sound comes back the first click can jump the mix
    /// (measured: 50 to 70 at once, most likely every turn made in the
    /// meantime arriving together). The app cannot tell a count left over from
    /// before from a fresh one, so after no sound the first count that differs
    /// is taken as the new starting point, and only turns after it move the
    /// mix.
    /// </para>
    /// </remarks>
    private void OnLink(HeadsetStatus status)
    {
        if (status.Link != Link.Connected)
        {
            _wheelLast = null;
            _wheelDark = _wheelResync = false;
        }
        else if (status.NoSound)
        {
            _wheelDark = true;
        }
        else if (_wheelDark)
        {
            _wheelDark = false;
            _wheelResync = true;
        }
    }

    // -- the picker --------------------------------------------------------

    /// <summary>
    /// Applications that could be the chat one. Anything with an audio
    /// session on the headset, whether or not it is making a sound right
    /// now, plus whatever is already chosen so a chat app that is not
    /// running does not vanish from its own setting.
    /// </summary>
    public Task<IReadOnlyList<ChatCandidate>> Candidates() => Task.Run<IReadOnlyList<ChatCandidate>>(() =>
    {
        var found = new Dictionary<string, ChatCandidate>(StringComparer.OrdinalIgnoreCase);
        if (Pretend.Active)
        {
            foreach (var session in PretendWindows.Sessions)
                found[session.Process] = new ChatCandidate(session.Process, session.Display, session.Playing);
        }
        else
        {
            AddSessions(found);
        }

        foreach (string chosen in ChatApps)
            if (!found.ContainsKey(chosen))
                found[chosen] = new ChatCandidate(chosen, chosen, false);

        return found.Values.OrderBy(c => c.Display, StringComparer.CurrentCultureIgnoreCase).ToList();
    });

    /// <summary>Every application with an audio session on the headset.</summary>
    private static void AddSessions(Dictionary<string, ChatCandidate> found)
    {
        try
        {
            using var devices = new MMDeviceEnumerator();
            using var headset = StealthPro.Core.Audio.Routing.Headset(devices, output: true);
            if (headset is not null)
            {
                headset.AudioSessionManager.RefreshSessions();
                var sessions = headset.AudioSessionManager.Sessions;
                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (session.State == AudioSessionState.AudioSessionStateExpired) continue;
                    uint pid = session.GetProcessID;
                    if (pid == 0 || pid == Environment.ProcessId) continue;
                    var (process, display) = Name(pid);
                    if (process.Length == 0 || found.ContainsKey(process)) continue;
                    found[process] = new ChatCandidate(process, display,
                        session.State == AudioSessionState.AudioSessionStateActive);
                }
            }
        }
        catch { }
    }

    /// <summary>The process name, and something a person would recognise.</summary>
    private static (string Process, string Display) Name(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            string name = process.ProcessName;
            string? described = null;
            try { described = process.MainModule?.FileVersionInfo.FileDescription; } catch { }
            return (name, string.IsNullOrWhiteSpace(described) ? name : described!);
        }
        catch { return ("", ""); }
    }

    // -- the centre cue ----------------------------------------------------

    /// <summary>A short, soft beep on reaching centre, played straight to the headset.</summary>
    /// <remarks>
    /// Not the default device: the mix is about the headset, and the person may
    /// well be listening on it while Windows plays elsewhere. Generated rather
    /// than shipped, so there is no audio asset to carry.
    /// </remarks>
    private static void CentreCue() => Task.Run(() =>
    {
        // A pretend run plays nothing: the real headset may be on
        // somebody's head.
        if (Pretend.Active)
        {
            AppLog.Write("mix: centre cue, not played on a pretend run");
            return;
        }
        try
        {
            using var devices = new MMDeviceEnumerator();
            using var headset = StealthPro.Core.Audio.Routing.Headset(devices, output: true);
            if (headset is null) return;

            using var player = new WasapiPlayerBuilder()
                .WithDevice(headset).WithSharedMode().WithPollingSync().WithLatency(60)
                .Build();

            // Made at the device's own rate, so nothing has to convert it.
            int rate = player.DeviceMixFormat.SampleRate;
            const double seconds = 0.13, frequency = 620.0;
            int total = (int)(rate * seconds), fade = (int)(rate * 0.012);
            var pcm = new byte[total * 2];
            for (int i = 0; i < total; i++)
            {
                double amplitude = 0.25;
                if (i < fade) amplitude *= (double)i / fade;
                else if (i > total - fade) amplitude *= (double)(total - i) / fade;
                short sample = (short)(amplitude * short.MaxValue
                    * Math.Sin(2 * Math.PI * frequency * i / rate));
                pcm[i * 2] = (byte)(sample & 0xFF);
                pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            using var source = new RawSourceWaveStream(
                new MemoryStream(pcm), new WaveFormat(rate, 16, 1));
            using var finished = new ManualResetEventSlim();
            player.PlaybackStopped += (_, _) => finished.Set();
            player.Init(source);
            player.Play();
            finished.Wait(TimeSpan.FromSeconds(2));
        }
        catch { /* a cue that will not play is not worth an error */ }
    });

    public void Dispose()
    {
        _headset.WheelMoved -= OnWheel;
        _headset.StatusChanged -= OnLink;
        Stop();
        lock (_noteGate) WriteNote();
        _noteTimer?.Dispose();
    }
}
