using System.Diagnostics;
using System.Globalization;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using Neap.Core.Connection;
using Neap.Core.Mix;
using Neap.Core.Pretend;

namespace Neap.App.Services;

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
/// Which of the wheel's readings move the mix, and how far, is decided in
/// <see cref="ChatWheel"/>.
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

    private readonly ChatWheel _wheel;

    private IMixEngine? _mix;
    private int? _lastApplied;
    private bool _detentHeld;

    public MixService(HeadsetService headset)
    {
        var running = Stopwatch.StartNew();
        _wheel = new ChatWheel(() => running.Elapsed);
        _headset = headset;
        _headset.WheelMoved += OnWheel;
        _headset.StatusChanged += OnLink;
    }

    /// <summary>The mix moved, or the engine started or stopped.</summary>
    public event Action? Changed;

    public bool Running => _mix is not null;

    public int Mix => _lastApplied ?? 50;

    public SessionMixStatus? Status => _mix?.Status;

    /// <summary>The applications that carry chat.</summary>
    /// <remarks>
    /// Choosing the first starts the mix, and clearing the last stops it and
    /// puts every volume back: with no chat to balance against, holding the
    /// game half down would only make everything quieter.
    /// </remarks>
    public IReadOnlyList<string> ChatApps
    {
        get => AppSettings.Current.ChatApps;
        set
        {
            AppSettings.Update(s => s.ChatApps = value.ToList());
            if (value.Count == 0) Stop();
            else if (_mix is null) Start();
            else
            {
                _mix.ChatApps = value;
                Changed?.Invoke();
            }
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

    private void OnWheel(int count)
    {
        var step = _wheel.Read(count);
        if (_wheel.Doubted is int doubted)
            AppLog.Write(FormattableString.Invariant(
                $"mix: the wheel read {doubted} straight after rest; held until the next reading confirms it"));
        if (step is WheelStep moved && _mix is not null)
            Apply(ChatWheel.Follow(Mix, moved), FormattableString.Invariant($"wheel {moved.From}->{moved.To}"));
    }

    private void OnLink(HeadsetStatus status) => _wheel.Link(status);

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
            using var headset = Neap.Core.Audio.Routing.Headset(devices, output: true);
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
            using var headset = Neap.Core.Audio.Routing.Headset(devices, output: true);
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
