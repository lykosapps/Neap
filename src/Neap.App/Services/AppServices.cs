using Neap.Core.Hid;

namespace Neap.App.Services;

/// <summary>
/// The app's long-lived services, in one place.
/// </summary>
/// <remarks>
/// There is exactly one of each and they outlive every page, because the
/// headset connection does: navigating between tabs must not drop the link
/// or re-do the 1.2-second full read. Created once on the UI thread when the
/// main window is constructed.
/// </remarks>
public static class AppServices
{
    public static HeadsetService Headset { get; private set; } = null!;
    public static MixService Mix { get; private set; } = null!;
    public static NoiseService Noise { get; private set; } = null!;
    public static PresetService Presets { get; private set; } = null!;
    public static ProfileService Profiles { get; private set; } = null!;
    public static AutoSwitchService AutoSwitch { get; private set; } = null!;
    public static HotkeyService Hotkeys { get; private set; } = null!;
    public static AudioRoute AudioRoute { get; private set; } = null!;
    public static RingService Ring { get; private set; } = null!;
    public static SessionRecorder Recorder { get; private set; } = null!;
    public static UpdateService Updates { get; private set; } = null!;

    /// <summary>Where the headset's transmitters are looked for.</summary>
    public static IDeviceSource Devices { get; } = (IDeviceSource?)Pretend.Headset ?? SystemDevices.Instance;

    public static void Start()
    {
        if (Headset is not null) return;

        // Before anything else: put back volumes a previous run left turned
        // down. An unclean exit is the one case where somebody is left with
        // a quiet application and no idea why.
        _ = MixService.Recover();

        // If the app has been moved since "start with Windows" was switched
        // on, point the entry at where it is now. A pretend run is not the
        // copy Windows should start.
        if (!Pretend.Active) Startup.Refresh();
        Pretend.Start();

        AudioRoute = new AudioRoute();
        Ring = new RingService(AudioRoute);
        Headset = new HeadsetService(Devices, cabled: () => AudioRoute.Cable.Length > 0);
        Mix = new MixService(Headset);
        Noise = new NoiseService(Headset);
        Presets = new PresetService(Headset);
        Profiles = new ProfileService(Headset);
        AutoSwitch = new AutoSwitchService(Profiles, Headset,
            Pretend.Windows is { } windows ? windows.Programs : AutoSwitchService.RunningPrograms);
        Recorder = new SessionRecorder(Headset);
        Updates = new UpdateService();
        Hotkeys = new HotkeyService(Mix);
        Hotkeys.Enable(AppSettings.Current.MixHotkeys && !Pretend.Active);

        // Pick up where the last run left off. Otherwise the mix starts only
        // when somebody opens Home and chooses an application
        // again, and a login launch, which never shows a window, sits there
        // with the chat wheel doing nothing.
        if (Mix.ChatApps.Count > 0) Mix.Start();
    }

    public static void Stop()
    {
        Updates?.Dispose();
        Recorder?.Dispose();
        Hotkeys?.Dispose();
        Mix?.Dispose();
        Noise?.Dispose();
        AutoSwitch?.Dispose();
        Profiles?.Dispose();
        Headset?.Dispose();
        Ring?.Dispose();
        AudioRoute?.Dispose();
        Pretend.Stop();
    }
}
