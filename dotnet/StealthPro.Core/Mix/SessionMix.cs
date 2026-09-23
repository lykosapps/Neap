using System.Diagnostics;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace StealthPro.Core.Mix;

public sealed record SessionMixStatus(
    bool Running, int Mix, int ChatSessions, int GameSessions, int Held,
    bool HeadsetIsOutput, string ChatApps, ChatElsewhere? Elsewhere);

/// <summary>
/// The chat application has audio open on a device that is not the headset.
/// </summary>
public sealed record ChatElsewhere(string App, string Device);

/// <summary>
/// The game/chat crossfade, done with nothing in the audio path.
///
/// The chat application and the game both play natively to the headset, as
/// they would with no software running at all. The mix is applied by setting
/// the chat application's session volumes to the chat half of the crossfade
/// and everything else on that endpoint to the game half.
///
/// <b>Why this replaced an audio engine.</b> The old design gave chat a
/// device of its own — a virtual cable the person had to install and point
/// their chat application at — captured that device by loopback, and
/// rendered it into the headset as our own stream. It worked, and it cost:
/// a driver install and a reboot, a licence that restricts redistribution,
/// a real-time capture and render path, drift correction between two clocks,
/// and a supervisor to restart the whole thing when it died.
///
/// None of that is needed. Session volume is already a working per-app gain
/// — it is how the game side always worked — so naming the chat application
/// does the whole job. Measured on hardware with a call and music playing at
/// once: endpoint peak 1.0000 with both, 0.8529 with chat alone, 0.6876 with
/// game alone. And it needs no configuration, because the chat application is
/// already on its default output.
///
/// <b>The limit, stated plainly.</b> Routing by application means the chat
/// application has to be its own process. Discord, Teams and Steam are; a
/// call taken in a browser is not, because the browser also carries game and
/// media audio. That case still wants a virtual cable.
///
/// <b>What did not go away.</b> This holds other applications' volumes down
/// while it runs and it is the only thing that knows what they were. If it
/// dies without putting them back, somebody is left with a quiet Spotify and
/// no idea why. So every change is journalled first and replayed on the next
/// launch. A prototype without that ratcheted an application to silence in
/// three commands.
/// </summary>
public sealed class SessionMix : IDisposable
{
    /// <summary>
    /// How often to look for applications that started playing since the mix
    /// was set. They are invisible until the session list is asked for again.
    /// </summary>
    private const int SweepMs = 2000;

    private readonly object _gate = new();
    /// <summary>How often to go looking; walking every endpoint is not free.</summary>
    private static readonly TimeSpan LookGap = TimeSpan.FromSeconds(4);

    private readonly string _headsetMatch;
    private readonly MMDeviceEnumerator _devices = new();

    /// <summary>Set to have Apply report what it actually managed to change.</summary>
    public static bool Diagnostics { get; set; }

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

    public SessionMix(IEnumerable<string> chatApps, string headsetMatch = "Stealth Pro")
    {
        _chatApps = chatApps.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
        _headsetMatch = headsetMatch;
    }

    public SessionMix(string chatApp, string headsetMatch = "Stealth Pro")
        : this(new[] { chatApp }, headsetMatch) { }

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
                    VolumeJournal.Shared.Count, _headsetIsOutput, string.Join(", ", _chatApps),
                    _elsewhere);
        }
    }

    /// <summary>Begin applying the mix, and keep applying it as things start.</summary>
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
    /// 0 = all game, 50 = both, 100 = all chat. Applied a moment later, off
    /// the caller's thread: a pass walks every session on the device, which
    /// is too slow to do on every step of a slider drag.
    /// </summary>
    public int SetMix(int percent)
    {
        int mix = Math.Clamp(percent, 0, 100);
        lock (_gate) _mix = mix;
        Poke();
        return mix;
    }

    /// <summary>Stop, and put every volume back.</summary>
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
        lock (_pass) RestoreDevices(_devices);
    }

    /// <summary>Apply the mix soon. Asking again before it runs costs nothing.</summary>
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
                if (Diagnostics) Console.Error.WriteLine($"[mix] pass failed: {ex}");
            }
        }
    }

    // Equal-ish balance: centre leaves both untouched, moving off centre
    // attenuates the side you are moving away from. Same curve the engine
    // used, so the slider feels identical.
    internal static float ChatScale(int mix) => MathF.Min(1f, 2f * (mix / 100f));
    internal static float GameScale(int mix) => MathF.Min(1f, 2f * (1f - mix / 100f));

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
        using var headset = FindHeadset();
        if (headset is null)
        {
            lock (_gate) _headsetIsOutput = false;
            RestoreDevices(_devices);
            _lastDevice = null;
            return;
        }
        // FindHeadset only returns the default output, so reaching here
        // means the person is listening on the headset.
        lock (_gate) _headsetIsOutput = true;

        // Moved from one of the headset's devices to another, the Charging
        // Dock to the USB Transmitter say. What was held down on the one left
        // behind goes back now, not whenever the mix next runs there.
        if (_lastDevice is { } previous && previous != headset.ID)
            RestoreDevices(_devices, previous);
        _lastDevice = headset.ID;

        float chatScale = ChatScale(mix), gameScale = GameScale(mix);
        int ours = Environment.ProcessId;
        int chatSeen = 0, gameSeen = 0;

        // Refreshed every time, never cached. NAudio hands back the same
        // collection until asked again, so an application that started
        // playing after the mix was set stays invisible — which is exactly
        // the bug that let one keep playing at full volume through a full
        // chat mix.
        headset.AudioSessionManager.RefreshSessions();
        var sessions = headset.AudioSessionManager.Sessions;

        var pending = new List<(AudioSessionControl Session, string Id, float Wanted, float Scale)>();
        for (int i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            if (session.GetProcessID == ours) continue;
            if (session.State == AudioSessionState.AudioSessionStateExpired) continue;
            // No identifier means nothing we could put back later.
            string? id = session.GetSessionIdentifier;
            if (string.IsNullOrEmpty(id)) continue;

            bool isChat = IsChat(session.GetProcessID, chatApps);
            if (isChat) chatSeen++; else gameSeen++;

            float scale = isChat ? chatScale : gameScale;
            if (VolumeJournal.Shared.Wanted(headset.ID, id, session.SimpleAudioVolume.Volume, scale)
                is float wanted)
                pending.Add((session, id, wanted, scale));
        }

        lock (_gate) { _chatCount = chatSeen; _gameCount = gameSeen; }

        // The mix can only reach sessions on the endpoint being listened to.
        // A chat application pointed at a different device is invisible to it
        // and silent to the person, and nothing about that says so: the wheel
        // simply stops doing anything to chat. Discord keeps its own output
        // setting, so switching the headset does not move it.
        //
        // Only looked for when chat was not found here, which is the only
        // time it can be true, and at most every few seconds because it means
        // walking every endpoint's sessions.
        if (chatSeen == 0 && chatApps.Count > 0) LookElsewhere(chatApps, headset.ID);
        else lock (_gate) _elsewhere = null;
        if (pending.Count == 0) return;

        // Write down what we are about to do before doing it. A kill between
        // the two costs nothing; a kill the other way round loses the
        // originals and the person's other applications stay quiet for good.
        VolumeJournal.Shared.Commit(headset.ID, pending.Select(p => (p.Id, p.Wanted, p.Scale)));
        int set = 0; string? trouble = null;
        foreach (var (session, _, wanted, _) in pending)
        {
            try { session.SimpleAudioVolume.Volume = wanted; set++; }
            catch (Exception ex) { trouble ??= ex.Message; }
        }
        if (Diagnostics)
            Console.Error.WriteLine($"[mix] pending {pending.Count}, set {set}"
                + (trouble is null ? "" : $", first failure: {trouble}"));
    }

    /// <summary>
    /// Find where the chat application is playing instead.
    ///
    /// A session that exists at all is the signal, not one that is currently
    /// making noise: a voice application sits Inactive between sounds, so
    /// waiting for Active would report it as missing every time nobody is
    /// talking. Having opened a device is what matters.
    /// </summary>
    private void LookElsewhere(List<string> chatApps, string headsetId)
    {
        long now = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            if (_lastLook != 0
                && (now - _lastLook) < LookGap.TotalSeconds * Stopwatch.Frequency) return;
            _lastLook = now;
        }

        ChatElsewhere? found = null;
        try
        {
            foreach (var device in _devices.EnumerateAudioEndPoints(
                         DataFlow.Render, DeviceState.Active))
                using (device)
                {
                    if (device.ID == headsetId || found is not null) continue;
                    device.AudioSessionManager.RefreshSessions();
                    var sessions = device.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count && found is null; i++)
                    {
                        var session = sessions[i];
                        if (session.State == AudioSessionState.AudioSessionStateExpired) continue;
                        if (!IsChat(session.GetProcessID, chatApps)) continue;
                        found = new ChatElsewhere(
                            ProcessName(session.GetProcessID), device.FriendlyName);
                    }
                }
        }
        catch { /* a device that will not answer is not where chat is */ }

        lock (_gate) _elsewhere = found;
    }

    /// <summary>
    /// Put back what we turned down: on every device the journal holds
    /// anything for, or only on <paramref name="only"/>. A device that is not
    /// plugged in keeps its record for when it is.
    /// </summary>
    private static void RestoreDevices(MMDeviceEnumerator devices, string? only = null)
    {
        foreach (string id in only is null ? VolumeJournal.Shared.Devices : [only])
        {
            try
            {
                using var device = devices.GetDevice(id);
                if (device.State != DeviceState.Active) continue;
                device.AudioSessionManager.RefreshSessions();
                VolumeJournal.Shared.RestoreInto(id, Volumes(device.AudioSessionManager.Sessions));
            }
            catch { /* devices come and go; never fail on the way out */ }
        }
    }

    private static IEnumerable<ISessionVolume> Volumes(SessionCollection sessions)
    {
        for (int i = 0; i < sessions.Count; i++) yield return new SessionVolume(sessions[i]);
    }

    private sealed class SessionVolume(AudioSessionControl session) : ISessionVolume
    {
        public string? Id => session.GetSessionIdentifier;

        public float Volume
        {
            get => session.SimpleAudioVolume.Volume;
            set => session.SimpleAudioVolume.Volume = value;
        }
    }

    private static bool IsChat(uint pid, List<string> chatApps)
    {
        if (chatApps.Count == 0) return false;
        string name = ProcessName(pid);
        if (name.Length == 0) return false;
        foreach (string app in chatApps)
            if (name.Contains(app, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static string ProcessName(uint pid)
    {
        if (pid == 0) return "";
        try { return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; }
        catch { return ""; }
    }

    /// <summary>
    /// The headset endpoint the person is actually listening on.
    ///
    /// <b>Not the first one whose name matches.</b> Two transmitters plugged
    /// in at once give two endpoints that both answer to "Stealth Pro" — the
    /// charging hub's and the dongle's — and picking the first meant the mix
    /// could be holding levels on one device while the person listened to the
    /// other. Same fault as choosing a control device by product id, one
    /// layer up, and just as quiet about it.
    ///
    /// The default output is the only endpoint the mix has any business
    /// touching, so ask for that one and check it is the headset, rather than
    /// finding a headset and then asking whether it is the default.
    /// </summary>
    private MMDevice? FindHeadset()
    {
        try
        {
            var current = _devices.GetDefaultAudioEndpoint(
                DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia);
            if (current.FriendlyName.Contains(_headsetMatch, StringComparison.OrdinalIgnoreCase))
                return current;
            current.Dispose();
            return null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Put back anything a previous run left turned down. Call this at
    /// launch, before anything else.
    /// </summary>
    public static void Recover()
    {
        try
        {
            using var devices = new MMDeviceEnumerator();
            RestoreDevices(devices);
        }
        catch { /* no audio system to ask; the journal keeps it for next time */ }
    }

    public void Dispose()
    {
        Stop();
        _devices.Dispose();
    }
}
