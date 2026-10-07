using Avalonia.Threading;
using Neap.Core.Audio;

namespace Neap.Desktop;

/// <summary>What the app runs on when it runs through Avalonia, on Windows or Linux.</summary>
/// <remarks>
/// What a system cannot offer yet is left out through <see cref="Unsupported"/>
/// and says so, so the screens leave the control out rather than show one
/// that does nothing.
/// </remarks>
public sealed class DesktopPlatform : IPlatform
{
    public IUiThread Ui { get; } = new AvaloniaUi();

    public IRouteSource Routes => SystemRoutes.Instance;

    public IVolumes Volumes => SystemVolumes.Instance;

    /// <remarks>Only the build made for Windows can reach Windows' spatial sound.</remarks>
    public ISpatialAudio Spatial { get; } =
#if WINDOWS
        OperatingSystem.IsWindows() ? new Neap.WinRt.WindowsSpatial() : Unsupported.Spatial;
#else
        Unsupported.Spatial;
#endif

    /// <remarks>A Linux desktop with no X display, or no library to reach it, has no keys to take.</remarks>
    public IHotkeys CreateHotkeys(MixService mix) =>
        OperatingSystem.IsWindows() ? new HotkeyService(mix)
        : OperatingSystem.IsLinux() && X11Hotkeys.Available ? new X11Hotkeys(mix)
        : Unsupported.Hotkeys(mix);

    public IRing CreateRing(AudioRoute route) =>
        OperatingSystem.IsWindows() ? new RingService(route) : Unsupported.Ring;

    public SoundSurvey Survey(TimeSpan listen, Func<uint, string> name) =>
        OperatingSystem.IsWindows()
            ? Routing.Survey(listen, name)
            : throw new InvalidOperationException("this system cannot yet say where its sound is going, for a problem report");

    public string ProgramName(uint pid) => SystemTools.ProgramName(pid);

    public Task Open(Uri link) => SystemTools.Open(link);

    public void Reveal(string path) => SystemTools.Reveal(path);

    public string Downloads() => SystemTools.Downloads();

    /// <summary>Avalonia's UI thread.</summary>
    private sealed class AvaloniaUi : IUiThread
    {
        public void Post(Action action) => Dispatcher.UIThread.Post(action);

        public IDisposable Every(TimeSpan interval, Action tick)
        {
            var timer = new DispatcherTimer(interval, DispatcherPriority.Normal, (_, _) => tick());
            timer.Start();
            return new Stop(timer);
        }

        private sealed class Stop(DispatcherTimer timer) : IDisposable
        {
            public void Dispose() => timer.Stop();
        }
    }
}
