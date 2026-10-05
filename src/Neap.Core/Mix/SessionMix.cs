using System.Diagnostics;

namespace Neap.Core.Mix;

public sealed record SessionMixStatus(
    bool Running, int Mix, int ChatSessions, int GameSessions, int Held,
    bool HeadsetIsOutput, string ChatApps, ChatElsewhere? Elsewhere);

/// <summary>
/// The chat application has audio open on a device that is not the headset.
/// </summary>
public sealed record ChatElsewhere(string App, string Device);

/// <summary>
/// The game/chat crossfade, applied through per-application volumes with
/// nothing in the audio path.
/// </summary>
/// <remarks>
/// <para>
/// The chat application and the game both play natively to the headset, as
/// they would with no software running at all. The mix sets the chat
/// application's volumes to the chat half of the crossfade and every other
/// application on that output to the game half. Per-application volume is
/// already a working gain, so no virtual cable, driver or real-time capture
/// and render path is needed, and there is nothing to configure, because the
/// chat application is already on its default output. Measured on hardware
/// with a call and music playing at once: endpoint peak 1.0000 with both,
/// 0.8529 with chat alone, 0.6876 with game alone.
/// </para>
/// <para>
/// Routing by application means the chat application has to be its own
/// process. Discord, Teams and Steam are; a call taken in a browser is not,
/// because the browser also carries game and media audio. That case still
/// needs a virtual cable.
/// </para>
/// <para>
/// This holds other applications' volumes down while it runs and is the only
/// thing that knows what they were. If it dies without putting them back,
/// somebody is left with a quiet Spotify and no idea why. So every change is
/// journalled first and replayed on the next launch by <see cref="Recover"/>.
/// Without the journal, three commands in a row are enough to ratchet an
/// application to silence.
/// </para>
/// </remarks>
public sealed class SessionMix : IMixEngine
{
    /// <summary>
    /// How often to look for applications that started playing since the mix
    /// was set.
    /// </summary>
    /// <remarks>They are invisible until the session list is asked for again.</remarks>
    private const int SweepMs = 2000;

    private readonly object _gate = new();
    /// <summary>How often to go looking; walking every output is not free.</summary>
    private static readonly TimeSpan LookGap = TimeSpan.FromSeconds(4);

    private readonly IPlayback _playback;
    private readonly VolumeJournal _journal;

    /// <summary>Set to have Apply report what it actually managed to change.</summary>
    public static bool Diagnostics { get; set; }

    /// <summary>Where the mix and its journal report a failure. Unset, nothing is reported.</summary>
    /// <remarks>
    /// The mix works on pool threads and on the way out, where there is nobody
    /// to throw to, so a failure is handed here instead.
    /// </remarks>
    public static Action<string>? Trouble { get; set; }

    /// <summary>Where the mix says it looked up another program's name. Unset, nothing is said.</summary>
    public static Action<string>? LookedUp { get; set; }

    private List<string> _chatApps;
    private int _mix = 50;
    private bool _running;
    private Timer? _sweep;

    /// <summary>Held for the whole of a pass, so no two passes ever overlap.</summary>
    private readonly object _pass = new();
    private int _passQueued;

    /// <summary>The device the last pass held volumes down on. Only touched inside a pass.</summary>
    private string? _lastDevice;
    private int _chatCount, _gameCount;
    private bool _headsetIsOutput = true;
    private ChatElsewhere? _elsewhere;
    private long _lastLook;

    /// <summary>A mix over this operating system's per-application volumes.</summary>
    /// <exception cref="PlatformNotSupportedException">The operating system has none the mix can reach.</exception>
    public SessionMix(IEnumerable<string> chatApps)
        : this(Playback.ForThisSystem(), chatApps, VolumeJournal.Shared) { }

    internal SessionMix(IPlayback playback, IEnumerable<string> chatApps, VolumeJournal journal)
    {
        _playback = playback;
        _journal = journal;
        _chatApps = chatApps.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
    }

    public IReadOnlyList<string> ChatApps
    {
        get { lock (_gate) return _chatApps.ToList(); }
        set { lock (_gate) _chatApps = value.ToList(); Poke(); }
    }

    public SessionMixStatus Status
    {
        get
        {
            lock (_gate)
                return new SessionMixStatus(_running, _mix, _chatCount, _gameCount,
                    _journal.Count, _headsetIsOutput, string.Join(", ", _chatApps),
                    _elsewhere);
        }
    }

    /// <summary>Begins applying the mix, and keeps applying it as applications start.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_running) return;
            _running = true;
        }
        Poke();
        _sweep = new Timer(_ => Pass(), null, SweepMs, SweepMs);
    }

    /// <summary>
    /// Sets the mix: 0 = all game, 50 = both, 100 = all chat. Returns the
    /// value clamped to that range.
    /// </summary>
    /// <remarks>
    /// Applied a moment later, off the caller's thread: a pass walks every
    /// session on the device, which is too slow to do on every step of a
    /// slider drag.
    /// </remarks>
    public int SetMix(int percent)
    {
        int mix = Math.Clamp(percent, 0, 100);
        lock (_gate) _mix = mix;
        Poke();
        return mix;
    }

    /// <summary>Stops, and puts every volume back.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (!_running) return;
            _running = false;
        }
        _sweep?.Dispose();
        _sweep = null;

        // Wait out a pass already under way. Otherwise it could hold volumes
        // down again just after they were put back.
        lock (_pass) RestoreDevices(_playback, _journal);
    }

    /// <summary>Applies the mix now, on the caller's thread.</summary>
    internal void ApplyNow() => Pass();

    /// <summary>Applies the mix soon. Asking again before it runs costs nothing.</summary>
    private void Poke()
    {
        if (Interlocked.Exchange(ref _passQueued, 1) == 1) return;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Interlocked.Exchange(ref _passQueued, 0);
            Pass();
        });
    }

    private void Pass()
    {
        lock (_pass)
        {
            try { Apply(); }
            catch (Exception ex)
            {
                // Raised on a pool thread, where anything thrown ends the process.
                Trouble?.Invoke($"a pass failed: {ex.Message}");
            }
        }
    }

    private void Apply()
    {
        bool running;
        int mix;
        List<string> chatApps;
        lock (_gate) { running = _running; mix = _mix; chatApps = _chatApps.ToList(); }
        if (!running) return;

        // Hold nothing down on a device nobody is listening to. If the person
        // has switched to speakers — or to the other transmitter — the mix
        // applies to nothing they can hear, and levels left pinned on an
        // unused device are found later with no explanation.
        using var headset = _playback.Headset();
        if (headset is null)
        {
            lock (_gate) _headsetIsOutput = false;
            RestoreDevices(_playback, _journal);
            _lastDevice = null;
            return;
        }
        lock (_gate) _headsetIsOutput = true;

        // Moved from one of the headset's devices to another, the Charging
        // Dock to the USB Transmitter say. What was held down on the one left
        // behind goes back now, not whenever the mix next runs there.
        if (_lastDevice is { } previous && previous != headset.Id)
            RestoreDevices(_playback, _journal, previous);
        _lastDevice = headset.Id;

        float chatScale = MixLevels.ChatScale(mix), gameScale = MixLevels.GameScale(mix);
        int chatSeen = 0, gameSeen = 0;
        bool chatPlaying = false;

        var pending = new List<(IPlaybackSession Session, string Id, float Wanted, float Scale)>();
        foreach (var session in headset.Sessions())
        {
            if (session.Ours) continue;
            // No identifier means nothing we could put back later.
            string? id = session.Id;
            if (string.IsNullOrEmpty(id)) continue;

            bool isChat = IsChat(session, chatApps);
            if (isChat) chatSeen++; else gameSeen++;
            if (isChat && session.Playing) chatPlaying = true;

            float scale = isChat ? chatScale : gameScale;
            if (_journal.Wanted(headset.Id, id, session.Volume, scale) is float wanted)
                pending.Add((session, id, wanted, scale));
        }

        lock (_gate) { _chatCount = chatSeen; _gameCount = gameSeen; }

        // The mix can only reach sessions on the output being listened to.
        // A chat application pointed at a different device is invisible to it
        // and silent to the person, and nothing about that says so: the wheel
        // simply stops doing anything to chat. Discord keeps its own output
        // setting, so switching the headset does not move it.
        //
        // Only looked for when chat is not playing here, which is the only
        // time it can be true, and at most every few seconds because it means
        // walking every output's sessions.
        if (!chatPlaying && chatApps.Count > 0) LookElsewhere(chatApps, headset);
        else lock (_gate) _elsewhere = null;
        if (pending.Count == 0) return;

        // Write down what we are about to do before doing it. A kill between
        // the two costs nothing; a kill the other way round loses the
        // originals and the person's other applications stay quiet for good.
        _journal.Commit(headset.Id, pending.Select(p => (p.Id, p.Wanted, p.Scale)));
        int set = 0; string? trouble = null;
        foreach (var (session, _, wanted, _) in pending)
        {
            try { session.Volume = wanted; set++; }
            catch (Exception ex) { trouble ??= ex.Message; }
        }
        if (Diagnostics)
            Console.Error.WriteLine($"[mix] pending {pending.Count}, set {set}"
                + (trouble is null ? "" : $", first failure: {trouble}"));
    }

    /// <summary>Finds where the chat application is playing instead.</summary>
    /// <remarks>
    /// Only a playing session counts, here and elsewhere. An application keeps
    /// idle sessions on every device it has played to, so counting those put
    /// Discord "here" on the Charging Dock while it was playing to the USB
    /// Transmitter, and the warning never appeared. Between sounds, when chat
    /// is idle everywhere, nothing is reported: there is nothing for the mix
    /// to miss.
    /// </remarks>
    private void LookElsewhere(List<string> chatApps, IPlaybackDevice headset)
    {
        long now = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            if (_lastLook != 0
                && (now - _lastLook) < LookGap.TotalSeconds * Stopwatch.Frequency) return;
            _lastLook = now;
        }

        var seen = new List<ChatSession>();
        try
        {
            foreach (var device in _playback.Others(headset))
                using (device)
                    foreach (var session in device.Sessions())
                        if (IsChat(session, chatApps))
                            seen.Add(new ChatSession(session.Program, device.Name, session.Playing));
        }
        catch { /* a device that will not answer is not where chat is */ }

        lock (_gate) _elsewhere = Elsewhere(seen);
    }

    /// <summary>A chat application's session on a device other than the one being mixed.</summary>
    internal sealed record ChatSession(string App, string Device, bool Playing);

    /// <summary>The first device chat is playing on, of those not being mixed.</summary>
    internal static ChatElsewhere? Elsewhere(IEnumerable<ChatSession> sessions) =>
        sessions.FirstOrDefault(s => s.Playing) is { } playing
            ? new ChatElsewhere(playing.App, playing.Device)
            : null;

    /// <summary>
    /// Puts back what was turned down: on every device the journal holds
    /// anything for, or only on <paramref name="only"/>.
    /// </summary>
    /// <remarks>A device that is not plugged in keeps its record for when it is.</remarks>
    private static void RestoreDevices(IPlayback playback, VolumeJournal journal, string? only = null)
    {
        foreach (string id in only is null ? journal.Devices : [only])
        {
            try
            {
                using var device = playback.Find(id);
                if (device is null) continue;
                journal.RestoreInto(id, device.Sessions());
            }
            catch (Exception ex)
            {
                // Devices come and go; never fail on the way out. The journal
                // keeps the record for the next try.
                Trouble?.Invoke($"could not put volumes back yet: {ex.Message}");
            }
        }
    }

    /// <remarks>The program is named only once there is a chat application to compare it with.</remarks>
    private static bool IsChat(IPlaybackSession session, List<string> chatApps)
    {
        if (chatApps.Count == 0) return false;
        string program = session.Program;
        if (program.Length == 0) return false;
        foreach (string app in chatApps)
            if (program.Contains(app, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Puts back anything a previous run left turned down. Call this at
    /// launch, before anything else.
    /// </summary>
    public static void Recover()
    {
        try
        {
            using var playback = Playback.ForThisSystem();
            RestoreDevices(playback, VolumeJournal.Shared);
        }
        catch { /* no audio system to ask; the journal keeps it for next time */ }
    }

    public void Dispose()
    {
        Stop();
        _playback.Dispose();
    }
}
