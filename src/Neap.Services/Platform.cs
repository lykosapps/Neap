using Neap.Core.Audio;

namespace Neap.Services;

/// <summary>What the app runs on that differs between operating systems, supplied once at launch.</summary>
/// <remarks>
/// <para>
/// Everything else the services do is the same on every system. What is here
/// is what reaches the system itself: the UI thread, keys that work from
/// inside a game, the dock's status ring, spatial sound, and where the
/// operating system says sound is going.
/// </para>
/// <para>
/// A feature a system cannot offer says so, so the screens can leave it out
/// rather than show a control that does nothing; see <see cref="Unsupported"/>.
/// </para>
/// </remarks>
public interface IPlatform
{
    /// <summary>The thread the screens run on.</summary>
    IUiThread Ui { get; }

    /// <summary>Where sound is going, and which transmitter owns it.</summary>
    IRouteSource Routes { get; }

    /// <summary>Spatial sound for the headset.</summary>
    ISpatialAudio Spatial { get; }

    /// <summary>Keys that move the mix from inside a game.</summary>
    IHotkeys CreateHotkeys(MixService mix);

    /// <summary>Keeps the Charging Dock's status ring purple.</summary>
    IRing CreateRing(AudioRoute route);

    /// <summary>What the operating system says it is doing with sound, for a problem report.</summary>
    /// <exception cref="InvalidOperationException">This system cannot say.</exception>
    SoundSurvey Survey(TimeSpan listen, Func<uint, string> name);

    /// <summary>The process name of the program behind a process id, "witcher3" for witcher3.exe; empty when unknown.</summary>
    string ProgramName(uint pid);

    /// <summary>Opens a link in the default browser.</summary>
    Task Open(Uri link);

    /// <summary>Shows a file in the system's file manager, selected where it can be.</summary>
    void Reveal(string path);

    /// <summary>The person's Downloads folder, wherever they have moved it.</summary>
    string Downloads();
}

/// <summary>Where the operating system sends sound and takes the microphone from.</summary>
public interface IRouteSource
{
    /// <summary>The default output, input, and the same for calls, the headset's own cable device and who owns what.</summary>
    RouteLook Look();

    /// <summary>The names of one transmitter's devices, output or microphone.</summary>
    List<string> Belonging(string product, bool output);

    /// <summary>Asks to be told when a route may have moved, or null when this system cannot say.</summary>
    /// <remarks>Raised on a thread of the system's own, which must not be held up.</remarks>
    IDisposable? Watch(Action moved);
}

/// <summary>A reading of the system's defaults.</summary>
/// <param name="Output">The default output, and the headset device behind it if it is one.</param>
/// <param name="Calls">The default output for calls, which chat apps can use, where the system has one.</param>
/// <param name="Input">The default microphone.</param>
/// <param name="CallsInput">The default microphone for calls.</param>
/// <param name="Cable">The product id of the headset's own device when it is plugged in with a cable, or empty.</param>
public sealed record RouteLook(Routed? Output, Routed? Calls, Routed? Input, Routed? CallsInput, string Cable);

/// <summary>The thread the screens run on.</summary>
public interface IUiThread
{
    /// <summary>Runs something on it, later.</summary>
    void Post(Action action);

    /// <summary>Runs something on it every so often until disposed.</summary>
    IDisposable Every(TimeSpan interval, Action tick);
}

/// <summary>Global keys that move the mix.</summary>
public interface IHotkeys : IDisposable
{
    /// <summary>A key was registered, refused, changed, or given up.</summary>
    event Action? Changed;

    /// <summary>Whether this system can register keys at all.</summary>
    bool Supported { get; }

    bool Enabled { get; }

    Shortcut Key(MixKey which);

    /// <summary>What the system said when it refused this one, or null.</summary>
    string? Trouble(MixKey which);

    void Enable(bool enabled);

    void Rebind(MixKey which, Shortcut shortcut);

    void ResetToDefaults();
}

/// <summary>The Charging Dock's status ring.</summary>
public interface IRing : IDisposable
{
    /// <summary>Whether this system can keep the ring purple.</summary>
    bool Supported { get; }

    /// <summary>Gets or sets whether the ring is kept purple.</summary>
    bool KeepPurple { get; set; }
}

/// <summary>The spatial formats on offer for the headset and the one on, or why there are none.</summary>
/// <param name="Offered">Every format this endpoint offers; empty when none could be read.</param>
/// <param name="Active">The format currently on, or null when none is, or none could be read.</param>
/// <param name="Trouble">Why nothing could be read, or null when it could.</param>
/// <param name="EndpointId">
/// Which of the headset's endpoints this is, so a caller can tell a real
/// transmitter switch from a re-read of the same one; null when unavailable.
/// </param>
/// <param name="Unrecognised">
/// Something is active that is not one of <see cref="SpatialFormat"/>: set
/// from the system's own settings, or a format added since. It is still a
/// deliberate choice, just not one this shows by name.
/// </param>
public sealed record SpatialPanel(IReadOnlyList<SpatialFormat> Offered, SpatialFormat? Active, string? Trouble,
    string? EndpointId = null, bool Unrecognised = false)
{
    public static SpatialPanel Unavailable(string trouble) => new([], null, trouble);
}

/// <summary>Spatial sound for the headset, off the UI thread.</summary>
public interface ISpatialAudio
{
    /// <summary>Whether this system has spatial sound for the headset to offer.</summary>
    bool Supported { get; }

    Task<SpatialPanel> Read();

    Task<SpatialResult> Apply(SpatialFormat format);

    /// <summary>Watches one endpoint for a change made outside Neap; null when there is nothing to watch.</summary>
    /// <remarks><paramref name="changed"/> arrives off the UI thread.</remarks>
    IDisposable? Watch(string endpointId, Action changed);
}

/// <summary>The app's platform, set once at launch before any service starts.</summary>
public static class Platform
{
    private static IPlatform? _current;

    /// <exception cref="InvalidOperationException">No platform has been set yet.</exception>
    public static IPlatform Current =>
        _current ?? throw new InvalidOperationException("the platform is set before the app's services start");

    public static void Use(IPlatform platform) => _current = platform;

    /// <summary>Runs something on the UI thread, later.</summary>
    public static void Post(Action action) => Current.Ui.Post(action);
}

/// <summary>The features a system has no way to offer, each saying so.</summary>
public static class Unsupported
{
    public static ISpatialAudio Spatial { get; } = new NoSpatial();

    public static IHotkeys Hotkeys(MixService mix) => new NoHotkeys();

    public static IRing Ring { get; } = new NoRing();

    private sealed class NoSpatial : ISpatialAudio
    {
        public bool Supported => false;
        public Task<SpatialPanel> Read() => Task.FromResult(SpatialPanel.Unavailable(""));
        public Task<SpatialResult> Apply(SpatialFormat format) => Task.FromResult(SpatialResult.NotSupportedOnAudioEndpoint);
        public IDisposable? Watch(string endpointId, Action changed) => null;
    }

    private sealed class NoHotkeys : IHotkeys
    {
        public event Action? Changed { add { } remove { } }
        public bool Supported => false;
        public bool Enabled => false;
        public Shortcut Key(MixKey which) => MixShortcuts.Defaults[which];
        public string? Trouble(MixKey which) => null;
        public void Enable(bool enabled) { }
        public void Rebind(MixKey which, Shortcut shortcut) { }
        public void ResetToDefaults() { }
        public void Dispose() { }
    }

    private sealed class NoRing : IRing
    {
        public bool Supported => false;
        public bool KeepPurple { get => false; set { } }
        public void Dispose() { }
    }
}
