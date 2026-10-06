using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Neap.Core.Connection;

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

        OneCopy.Current?.ListenForShow(() => Dispatcher.UIThread.Post(Reveal));
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
        HideToTray();
    }

    /// <summary>
    /// Hides the window to the notification area, and lets go of the page so
    /// what it watches and polls stops while nobody is looking.
    /// </summary>
    public void HideToTray()
    {
        AppLog.Write("window: closed to the notification area");
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
        _quitting = true;
        Dispose();
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
