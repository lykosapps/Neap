using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using NAudio.CoreAudioApi;
using Neap.Core;
using Neap.Core.Audio;

namespace Neap.App.Services;

/// <summary>What the app runs on when it runs on Windows.</summary>
public sealed class WindowsPlatform(DispatcherQueue dispatcher) : IPlatform
{
    public IUiThread Ui { get; } = new DispatcherUi(dispatcher);

    public IRouteSource Routes { get; } = new WindowsRoutes();

    public ISpatialAudio Spatial { get; } = new WindowsSpatial();

    public IHotkeys CreateHotkeys(MixService mix) => new HotkeyService(mix);

    public IRing CreateRing(AudioRoute route) => new RingService(route);

    public SoundSurvey Survey(TimeSpan listen, Func<uint, string> name) => Routing.Survey(listen, name);

    public string ProgramName(uint pid) => Programs.NameOf(pid);

    public Task Open(Uri link) => Windows.System.Launcher.LaunchUriAsync(link).AsTask();

    /// <remarks>Explorer is started after the browser, so it lands in front of it, with the file ready to drag into the form.</remarks>
    public void Reveal(string path) => Process.Start("explorer.exe", $"/select,\"{path}\"");

    public string Downloads()
    {
        var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        if (SHGetKnownFolderPath(in id, 0, IntPtr.Zero, out string? path) == 0 && !string.IsNullOrEmpty(path)) return path;
        AppLog.Write("recording: Windows did not say where Downloads is, so the usual place is used");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(in Guid id, uint flags, IntPtr token, out string? path);

    /// <summary>The window's own dispatcher.</summary>
    private sealed class DispatcherUi(DispatcherQueue queue) : IUiThread
    {
        public void Post(Action action) => queue.TryEnqueue(() => action());

        public IDisposable Every(TimeSpan interval, Action tick)
        {
            var timer = queue.CreateTimer();
            timer.Interval = interval;
            timer.Tick += (_, _) => tick();
            timer.Start();
            return new Stop(timer);
        }

        private sealed class Stop(DispatcherQueueTimer timer) : IDisposable
        {
            public void Dispose() => timer.Stop();
        }
    }

    /// <summary>Windows' defaults, and its notice that one moved.</summary>
    private sealed class WindowsRoutes : IRouteSource
    {
        public RouteLook Look() => new(
            Routing.Default(output: true),
            Routing.Default(output: true, communications: true),
            Routing.Default(output: false),
            Routing.Default(output: false, communications: true),
            Routing.Cable());

        public List<string> Belonging(string product, bool output) => Routing.Belonging(product, output);

        public IDisposable? Watch(Action moved)
        {
            var devices = new MMDeviceEnumerator();
            try
            {
                var client = devices.CreateNotificationClient(useSynchronizationContext: false);
                client.DefaultDeviceChanged += (_, _) => moved();
                client.DeviceAdded += (_, _) => moved();
                client.DeviceRemoved += (_, _) => moved();
                client.DeviceStateChanged += (_, _) => moved();
                return new Watching(devices, client);
            }
            catch
            {
                devices.Dispose();
                throw;
            }
        }

        private sealed class Watching(MMDeviceEnumerator devices, MMDeviceNotificationClient client) : IDisposable
        {
            public void Dispose()
            {
                try { client.Dispose(); }
                finally { devices.Dispose(); }
            }
        }
    }

    /// <summary>Windows' spatial sound, which <see cref="SpatialAudio"/> reaches.</summary>
    private sealed class WindowsSpatial : ISpatialAudio
    {
        public bool Supported => true;

        public Task<SpatialPanel> Read() => SpatialAudio.Read();

        public Task<SpatialResult> Apply(SpatialFormat format) => SpatialAudio.Apply(format);

        public IDisposable? Watch(string endpointId, Action changed) => SpatialAudio.Watch(endpointId, changed);
    }
}
