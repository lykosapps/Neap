using Microsoft.UI.Xaml;
using Neap.Core;

namespace Neap.App;

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
    /// just quits, because there is nothing to show anyone. So does a pretend
    /// launch behind every window, which a script makes while somebody works.
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
        if (Pretend.Theme is { } theme)
            RequestedTheme = theme == PretendTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        UnhandledException += (_, e) => AppLog.Write($"crashed: {e.Exception}");
    }

    /// <summary>Lets go of the one-copy lock, so a copy started now runs instead of handing over to this one.</summary>
    /// <remarks>Call on the UI thread, which took the lock, and only once the app has stopped its work.</remarks>
    public static void LetGo()
    {
        _one?.ReleaseMutex();
        _one?.Dispose();
        _one = null;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        bool pretend = Pretend.Active;
        if (!pretend)
        {
            try { AppFolder.MoveFromEarlierName(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppLog.Write($"could not move the folder kept under the app's earlier name: {ex.Message}");
            }
        }
        Pretend.Separate();
        bool atLogin = Startup.LaunchedAtLogin;
        bool quiet = atLogin || Pretend.Behind;
        string wakeName = pretend ? PretendWakeName : WakeName;

        _one = new Mutex(initiallyOwned: true, pretend ? PretendOneName : OneName, out bool first);
        if (!first)
        {
            if (!quiet)
            {
                try { EventWaitHandle.OpenExisting(wakeName).Set(); }
                catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException)
                {
                    AppLog.Write($"could not show the running copy: {ex.Message}");
                }
            }
            AppLog.Write(quiet
                ? "started while already running: left the running copy alone"
                : "started again while running: showed the running copy instead");
            Exit();
            return;
        }

        AppLog.Write(pretend ? "started with the pretend headset"
            : atLogin ? "started at login" : "started");

        _wake = new EventWaitHandle(false, EventResetMode.AutoReset, wakeName);
        Window = new MainWindow();
        if (Pretend.Behind) Window.ShowBehind();
        else Window.Activate();
        if (atLogin) Window.HideToTray();

        var ui = Window.DispatcherQueue;
        ThreadPool.RegisterWaitForSingleObject(_wake,
            (_, _) => ui.TryEnqueue(() => Window?.Reveal()),
            null, Timeout.Infinite, executeOnlyOnce: false);
    }
}
