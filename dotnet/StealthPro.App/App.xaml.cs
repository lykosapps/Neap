using Microsoft.UI.Xaml;
using StealthPro.Core;

namespace StealthPro.App;

public partial class App : Application
{
    public static MainWindow? Window { get; private set; }

    /// <summary>The mutex that keeps the app to one copy at a time.</summary>
    /// <remarks>
    /// <para>
    /// Two copies both open the headset's channel and take each other's
    /// replies, and the symptom is a headset that reads as "Connecting" for
    /// ever. Opening the app from the Start menu while it sits in the
    /// notification area would otherwise make two.
    /// </para>
    /// <para>
    /// A second launch asks the running copy to show its window and quits. A
    /// second launch at login (the app already running when Windows starts it)
    /// just quits, because there is nothing to show anyone.
    /// </para>
    /// </remarks>
    private const string OneName = @"Local\Neap";
    private const string WakeName = @"Local\Neap.Show";

    /// <summary>The pretend run's own lock, so it can run beside the real app.</summary>
    private const string PretendOneName = @"Local\Neap.Pretend";
    private const string PretendWakeName = @"Local\Neap.Pretend.Show";

    private static Mutex? _one;
    private static EventWaitHandle? _wake;

    /// <remarks>
    /// A crash leaves its exception in the app log, since Windows records only
    /// the module it happened in.
    /// </remarks>
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => Services.AppLog.Write($"crashed: {e.Exception}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppFolder.MoveFromEarlierName();
        Services.Pretend.Separate();
        bool atLogin = Services.Startup.LaunchedAtLogin;
        bool pretend = Services.Pretend.Active;
        string wakeName = pretend ? PretendWakeName : WakeName;

        _one = new Mutex(initiallyOwned: true, pretend ? PretendOneName : OneName, out bool first);
        if (!first)
        {
            if (!atLogin)
            {
                try { EventWaitHandle.OpenExisting(wakeName).Set(); } catch { }
            }
            Services.AppLog.Write(atLogin
                ? "started at login, but already running: left the running copy alone"
                : "started again while running: showed the running copy instead");
            Exit();
            return;
        }

        Services.AppLog.Write(pretend ? "started with the pretend headset"
            : atLogin ? "started at login" : "started");

        _wake = new EventWaitHandle(false, EventResetMode.AutoReset, wakeName);
        Window = new MainWindow();
        if (Services.Pretend.Behind) Window.ShowBehind();
        else Window.Activate();
        if (atLogin) Window.HideToTray();

        var ui = Window.DispatcherQueue;
        ThreadPool.RegisterWaitForSingleObject(_wake,
            (_, _) => ui.TryEnqueue(() => Window?.Reveal()),
            null, Timeout.Infinite, executeOnlyOnce: false);
    }
}
