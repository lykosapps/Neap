using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Win32;
using Neap.Core;
using Neap.Core.Connection;
using Neap.Core.Updates;
using Neap.Desktop.Controls;

namespace Neap.Desktop;

/// <summary>
/// The notification-area icon: closing the window puts the app there instead
/// of stopping it, and it says the headset's state and battery on hover.
/// </summary>
/// <remarks>
/// The mix, the chat wheel and the headset's own controls are the point of
/// running, and none of them need a window. A click on the icon opens the
/// mini panel, with the settings changed mid-game; the window and Quit
/// are on the icon's menu.
/// </remarks>
public partial class MainWindow : IDisposable
{
    private TrayIcon? _tray;
    private bool _quitting;
    private MiniPanelWindow? _mini;
    private NativeMenuItem? _miniItem;
    private long _miniClosedAt;

    /// <summary>Gives memory back every minute while the window is hidden, on Windows.</summary>
    private IDisposable? _trimming;

    /// <summary>Puts the icon in the notification area and makes closing the window a hide.</summary>
    private void StartTray()
    {
        // A picture of the window is taken by a run that then exits; it has
        // no use for an icon.
        if (Pretend.Snapshot is not null) return;

        // A second launch asks this copy for its window, and an update
        // restarts it, whether or not there is an icon.
        OneCopy.Current?.ListenForShow(() => Dispatcher.UIThread.Post(Reveal));
        AppServices.Updates.Installed += OnUpdateInstalled;

        // Without an icon to be got back by, the window closing closes the app,
        // and the mini panel, which only the icon opens, is not offered.
        if (!TrayAvailability.Exists)
        {
            AppLog.Write("mini panel: not offered, as this desktop shows no notification-area icons");
            return;
        }

        var open = new NativeMenuItem(Strings.Get("Main_TrayOpen"));
        open.Click += (_, _) => Reveal();
        var mini = new NativeMenuItem(Strings.Get("Main_TrayMini"));
        mini.Click += (_, _) => ToggleMini();
        mini.IsVisible = AppSettings.Current.MiniPanel;
        _miniItem = mini;
        var quit = new NativeMenuItem(Strings.Get("Main_TrayQuit"));
        quit.Click += (_, _) => Quit();
        _tray = new TrayIcon
        {
            Icon = TrayPicture(),
            Menu = new NativeMenu { open, mini, new NativeMenuItemSeparator(), quit },
        };
        if (Application.Current?.PlatformSettings is { } settings) settings.ColorValuesChanged += RepaintTrayPicture;
        _tray.Clicked += (_, _) =>
        {
            if (MiniPanel.Click(AppSettings.Current.MiniPanel) == TrayClick.ToggleMiniPanel) ToggleMini();
            else Reveal();
        };
        if (Application.Current is { } app) TrayIcon.SetIcons(app, new TrayIcons { _tray });

        AppServices.Updates.Found += OnUpdateFound;

        AppServices.Headset.StatusChanged += OnHeadsetStatus;
        AppServices.Headset.Changed += PostPaintTray;
        AppServices.AudioRoute.Changed += PostPaintTray;
        AppServices.Headset.AccessChanged += PaintTray;
        PaintTray();
    }

    private void OnUpdateInstalled(string program) => Dispatcher.UIThread.Post(() => _ = Restart(program));

    private void OnUpdateFound(Release release) => Dispatcher.UIThread.Post(() => TellAboutUpdate(release));

    private void OnHeadsetStatus(HeadsetStatus status) => PostPaintTray();

    private void PostPaintTray() => Dispatcher.UIThread.Post(PaintTray);

    /// <summary>Takes the mini panel off the icon's menu and closes it when it is switched off, and puts it back when it is switched on.</summary>
    public void MiniPanelSettingChanged()
    {
        bool on = AppSettings.Current.MiniPanel;
        if (_miniItem is not null) _miniItem.IsVisible = on;
        if (!on) _mini?.Close();
    }

    /// <summary>Opens the mini panel, or closes it when it is open.</summary>
    /// <remarks>
    /// A click on the icon takes focus from an open panel, which closes it
    /// before the click arrives; <see cref="MiniPanel.Opens"/> tells that
    /// click apart from one that asks to open it.
    /// </remarks>
    private void ToggleMini()
    {
        if (!MiniPanel.Opens(_mini is not null, Stopwatch.GetElapsedTime(_miniClosedAt)))
        {
            _mini?.Close();
            return;
        }
        AppLog.Write("mini panel: opened from the notification area");
        var panel = new MiniPanelWindow { Main = this };
        panel.Closed += (_, _) =>
        {
            _miniClosedAt = panel.ClosedAt;
            _mini = null;
        };
        _mini = panel;
        panel.Show();
        panel.Activate();
    }

    /// <summary>The notification-area glyph: light on a dark taskbar, dark on a light one.</summary>
    /// <remarks>
    /// Windows has a taskbar shade, set apart from the app's own theme. A Linux
    /// panel gives no such answer, so Core judges it from the desktop and its theme.
    /// </remarks>
    private static WindowIcon TrayPicture()
    {
        bool light = OperatingSystem.IsWindows()
            ? TaskbarIsLight()
            : PanelShade.IsLight(Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"),
                Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Light);
        string shade = light ? "light" : "dark";
        return new WindowIcon(AssetLoader.Open(new Uri($"avares://Neap.Desktop/Assets/tray-{shade}.ico")));
    }

    [SupportedOSPlatform("windows")]
    private static bool TaskbarIsLight() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is 1;

    /// <summary>Swaps the glyph when the system reports a colour change, such as the taskbar's shade or the desktop's theme.</summary>
    private void RepaintTrayPicture(object? sender, PlatformColorValues colours) =>
        Dispatcher.UIThread.Post(() => _tray?.Icon = TrayPicture());

    /// <summary>
    /// Says the headset's state and battery on the notification-area icon, so
    /// a glance at it answers "is it on, and how much is left?" without
    /// opening the window.
    /// </summary>
    /// <remarks>The state is worded as the title bar words it.</remarks>
    private void PaintTray()
    {
        if (_tray is null) return;
        var status = AppServices.Headset.Status;
        string state = StateCopy.Label(StatusLook.Of(status, AppServices.AudioRoute.SoundElsewhere(status),
            AppServices.Headset.AccessDenied).Headline);
        string text = AppServices.Headset.Battery is { Settling: false } battery
            ? Strings.Format(battery.Charging ? "Tray_StateCharging" : "Tray_StateBattery", Title ?? AppInfo.Name, state, battery.Percent)
            : Strings.Format("Tray_State", Title ?? AppInfo.Name, state);
        if (_tray.ToolTipText != text) _tray.ToolTipText = text;
    }

    /// <summary>Says a newer version is out, for someone whose window is closed; one with the window open has the banner.</summary>
    /// <remarks>Selecting the notice, where the system lets it be selected, opens Settings, where updating is.</remarks>
    private void TellAboutUpdate(Release release)
    {
        if (IsVisible) return;
        UpdateNotice.Show(
            Strings.Format("Tray_UpdateTitle", AppInfo.Name, release.Version.ToString(3)),
            Strings.Get("Tray_Update"),
            () => Dispatcher.UIThread.Post(OpenUpdate));
    }

    private void OpenUpdate()
    {
        AppLog.Write("window: opened from the update notification");
        Reveal();
        Open("settings");
    }

    /// <remarks>
    /// Only a person closing the window is turned into a hide. When the
    /// system is shutting down, restarting or signing out, or the app is being
    /// shut down, refusing would cancel it for everyone: the system names the
    /// program that stopped it, and the person has to find and quit it first.
    /// </remarks>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_quitting || _tray is null) return;
        if (e.CloseReason is WindowCloseReason.OSShutdown or WindowCloseReason.ApplicationShutdown)
        {
            AppLog.Write($"window: the system is closing the app ({e.CloseReason})");
            StopRunning();
            return;
        }
        e.Cancel = true;
        _ = TellThemOnceThenHide();
    }

    /// <summary>
    /// Says, once ever, that the app is still running.
    /// </summary>
    /// <remarks>
    /// A new notification-area icon can be filed out of sight, so without this
    /// the first close looks exactly like quitting: window gone, no icon, and
    /// the mix still working with no way to tell. Said in a dialog, which every
    /// system can show, rather than a pop-up one of them may have switched off.
    /// </remarks>
    private async Task TellThemOnceThenHide()
    {
        if (!AppSettings.Current.ToldAboutTray && !Pretend.Active)
        {
            AppSettings.Update(s => s.ToldAboutTray = true);
            await new NeapDialog
            {
                Heading = Strings.Get("Tray_StillRunningTitle"),
                Body = new TextBlock { Text = Strings.Format("Tray_StillRunning", AppInfo.Name) },
                CloseButtonText = Strings.Get("Dialog_OK"),
            }.ShowAsync(this);
        }
        HideToTray();
    }

    /// <summary>
    /// Hides the window to the notification area, and lets go of the page so
    /// what it watches and polls stops while nobody is looking.
    /// </summary>
    public void HideToTray()
    {
        AppLog.Write("window: closed to the notification area");
        Remember();
        Hide();
        Body.Content = null;

        // Hand back what the pages used, and again every minute while hidden;
        // see WorkingSet. Only Windows has a resident size to trim.
        if (!OperatingSystem.IsWindows()) return;
        WorkingSet.Trim();
        _trimming ??= Platform.Current.Ui.Every(WorkingSet.HiddenEvery, WorkingSet.Trim);
    }

    /// <summary>
    /// Brings the window forward, for a second launch of the app or a click on
    /// the icon: someone opening it from the menu while it sits in the
    /// notification area wants the window, not a second copy.
    /// </summary>
    public void Reveal()
    {
        AppLog.Write("window: opened from the notification area");
        _trimming?.Dispose();
        _trimming = null;
        if (Body.Content is null) ShowCurrentPage();
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>Stops following the services and takes the icon away.</summary>
    /// <remarks>
    /// The services outlive the window, and a change already on its way when
    /// the app stops would otherwise reach an icon that has gone.
    /// </remarks>
    public void Dispose()
    {
        if (Application.Current?.PlatformSettings is { } settings) settings.ColorValuesChanged -= RepaintTrayPicture;
        AppServices.Updates.Installed -= OnUpdateInstalled;
        AppServices.Updates.Found -= OnUpdateFound;
        AppServices.Headset.StatusChanged -= OnHeadsetStatus;
        AppServices.Headset.Changed -= PostPaintTray;
        AppServices.AudioRoute.Changed -= PostPaintTray;
        AppServices.Headset.AccessChanged -= PaintTray;
        _tray?.Dispose();
        _tray = null;
        GC.SuppressFinalize(this);
    }

    private void Quit()
    {
        AppLog.Write("quit from the notification area");
        StopRunning();
        Shutdown();
    }

    private static void Shutdown(int exitCode = 0) =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(exitCode);

    /// <summary>Puts the volumes back, lets go of the headset and takes the icon away, before the window closes.</summary>
    private void StopRunning()
    {
        _quitting = true;
        Remember();
        Dispose();
        AppServices.Stop();
    }

    /// <summary>Stops this version, starts the one just put in its place, and says so if that one doesn't open.</summary>
    /// <remarks>
    /// <para>
    /// The one-copy lock is let go first, or the new version would find this
    /// one still running and hand over to it.
    /// </para>
    /// <para>
    /// This version is hidden, not closed, while <see cref="RestartWatch"/>
    /// watches the new one: if it does not stay open, this is the only
    /// window left to tell the person, and to point them at the download.
    /// </para>
    /// </remarks>
    private async Task Restart(string program)
    {
        StopRunning();
        OneCopy.Current?.Dispose();
        Hide();

        TimeSpan? exitedAfter = await Launch(program);
        if (RestartWatch.Of(exitedAfter) == RestartOutcome.Started)
        {
            Shutdown();
            return;
        }

        AppLog.Write(exitedAfter is { } after && after > TimeSpan.Zero
            ? $"updates: the new version closed {after.TotalSeconds:0.#} s after starting"
            : "updates: the new version could not be started");
        Show();
        Activate();
        if (AppServices.Updates.Newer is { } release) await UpdateDialogs.ShowDidNotStart(this, release);
        Shutdown();
    }

    /// <summary>Starts a program and gives how long it ran, up to the watch window: zero if it would not start, null if it was still running.</summary>
    private static async Task<TimeSpan?> Launch(string program)
    {
        Process? process;
        try { process = Process.Start(new ProcessStartInfo(program) { WorkingDirectory = Path.GetDirectoryName(program) }); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            AppLog.Write($"updates: could not start the new version: {ex.Message}");
            return TimeSpan.Zero;
        }
        if (process is null) return TimeSpan.Zero;

        using (process)
        {
            var clock = Stopwatch.StartNew();
            using var window = new CancellationTokenSource(RestartWatch.Window);
            try
            {
                await process.WaitForExitAsync(window.Token);
                return clock.Elapsed;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
    }
}
