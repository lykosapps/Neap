using Microsoft.UI.Xaml;

namespace StealthPro.App;

public partial class App : Application
{
    public static MainWindow? Window { get; private set; }

    /// <summary>
    /// <b>One copy at a time.</b> Two copies both open the headset's channel
    /// and take each other's replies, and the symptom is a headset that reads
    /// as "Connecting" for ever. Nothing stopped a second copy before: open the
    /// app from the Start menu while it sat in the notification area and there
    /// were two.
    ///
    /// A second launch now asks the running copy to show its window and quits.
    /// A second launch at login — the app already running when Windows starts
    /// it — just quits, because there is nothing to show anyone.
    /// </summary>
    private const string OneName = @"Local\StealthProIIControl";
    private const string WakeName = @"Local\StealthProIIControl.Show";

    private static Mutex? _one;
    private static EventWaitHandle? _wake;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        bool atLogin = Services.Startup.LaunchedAtLogin;

        _one = new Mutex(initiallyOwned: true, OneName, out bool first);
        if (!first)
        {
            if (!atLogin)
            {
                try { EventWaitHandle.OpenExisting(WakeName).Set(); } catch { }
            }
            Services.AppLog.Write(atLogin
                ? "started at login, but already running: left the running copy alone"
                : "started again while running: showed the running copy instead");
            Exit();
            return;
        }

        Services.AppLog.Write(atLogin ? "started at login" : "started");

        _wake = new EventWaitHandle(false, EventResetMode.AutoReset, WakeName);
        Window = new MainWindow();
        Window.Activate();
        if (atLogin) Window.HideToTray();

        var ui = Window.DispatcherQueue;
        ThreadPool.RegisterWaitForSingleObject(_wake,
            (_, _) => ui.TryEnqueue(() => Window?.Reveal()),
            null, Timeout.Infinite, executeOnlyOnce: false);
    }
}
