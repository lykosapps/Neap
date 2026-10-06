using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
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
/// running, and none of them need a window. Quit is on the icon's menu.
/// </remarks>
public partial class MainWindow : IDisposable
{
    private TrayIcon? _tray;
    private bool _quitting;

    /// <summary>Puts the icon in the notification area and makes closing the window a hide.</summary>
    private void StartTray()
    {
        // A picture of the window is taken by a run that then exits; it has
        // no use for an icon.
        if (Pretend.Snapshot is not null) return;

        // A second launch asks this copy for its window, and an update
        // restarts it, whether or not there is an icon.
        OneCopy.Current?.ListenForShow(() => Dispatcher.UIThread.Post(Reveal));
        AppServices.Updates.Installed += program => Dispatcher.UIThread.Post(() => _ = Restart(program));

        // Without an icon to be got back by, the window closing closes the app.
        if (!TrayAvailability.Exists) return;

        var open = new NativeMenuItem(Strings.Get("Main_TrayOpen.Text"));
        open.Click += (_, _) => Reveal();
        var quit = new NativeMenuItem(Strings.Get("Main_TrayQuit.Text"));
        quit.Click += (_, _) => Quit();
        _tray = new TrayIcon
        {
            Icon = Icon,
            Menu = new NativeMenu { open, new NativeMenuItemSeparator(), quit },
        };
        _tray.Clicked += (_, _) => Reveal();
        if (Application.Current is { } app) TrayIcon.SetIcons(app, new TrayIcons { _tray });

        AppServices.Headset.StatusChanged += _ => Dispatcher.UIThread.Post(PaintTray);
        AppServices.Headset.Changed += () => Dispatcher.UIThread.Post(PaintTray);
        AppServices.AudioRoute.Changed += () => Dispatcher.UIThread.Post(PaintTray);
        PaintTray();
    }

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
        string state = StateCopy.Label(StatusLook.Of(status, AppServices.AudioRoute.SoundElsewhere(status)).Headline);
        string text = AppServices.Headset.Battery is { } battery
            ? Strings.Format(battery.Charging ? "Tray_StateCharging" : "Tray_StateBattery", Title ?? AppInfo.Name, state, battery.Percent)
            : Strings.Format("Tray_State", Title ?? AppInfo.Name, state);
        if (_tray.ToolTipText != text) _tray.ToolTipText = text;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_quitting || _tray is null) return;
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
    }

    /// <summary>
    /// Brings the window forward, for a second launch of the app or a click on
    /// the icon: someone opening it from the menu while it sits in the
    /// notification area wants the window, not a second copy.
    /// </summary>
    public void Reveal()
    {
        AppLog.Write("window: opened from the notification area");
        if (Body.Content is null) ShowCurrentPage();
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void Dispose()
    {
        _tray?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Quit()
    {
        AppLog.Write("quit from the notification area");
        StopRunning();
        Shutdown();
    }

    private static void Shutdown() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();

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
