using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using StealthPro.App.Services;
using StealthPro.App.Views;
using Windows.Graphics;

namespace StealthPro.App;

public sealed partial class MainWindow : Window
{
    private bool _quitting;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _trim;

    /// <summary>
    /// The one window, so a page can move the selection rather than navigate
    /// the frame behind the rail's back and leave the two disagreeing.
    /// </summary>
    public static MainWindow? Instance { get; private set; }

    /// <summary>Go somewhere by its rail tag, as though it had been clicked.</summary>
    public void GoTo(string tag)
    {
        foreach (var item in Nav.MenuItems.OfType<NavigationViewItem>())
            if (item.Tag as string == tag) { Nav.SelectedItem = item; return; }
    }

    public MainWindow()
    {
        Instance = this;
        InitializeComponent();
        AppServices.Start();

        Title = AppInfo.Name;
        TitleText.Text = AppInfo.Name;
        Tray.ToolTipText = AppInfo.Name;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);

        // Clear of the window's own buttons, however wide Windows draws them.
        AppWindow.Changed += (_, _) => FitTitleBar();
        FitTitleBar();

        // Big enough for the equaliser's ten bands side by side, which is the
        // widest thing in the app and the one that reads badly when it is
        // cramped. Scaled by the monitor, because AppWindow works in real
        // pixels and this would otherwise come up half-size at 200%.
        double scale = Dpi() / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1180 * scale), (int)(900 * scale)));

        Nav.SelectedItem = Nav.MenuItems[0];

        // Closing the window puts the app in the notification area instead of
        // stopping it. The mix, the chat wheel and the headset's own controls
        // are the point of running at all, and none of them need a window —
        // so closing one must not switch them off. Quit is on the tray menu,
        // where somebody looking to stop it will look.
        AppWindow.Closing += (_, args) =>
        {
            if (_quitting) return;
            args.Cancel = true;
            Hide();
        };

        // The tray icon exposes a command rather than a click event, so the
        // one-line handler gets a one-line command.
        Tray.LeftClickCommand = new Do(Show);

    }

    /// <summary>
    /// Go straight to the notification area, for a login launch.
    ///
    /// Called after Activate rather than from the constructor: activation is
    /// what puts the window on screen, so hiding before it happens gets
    /// undone a moment later and the app greets you at every login anyway.
    /// </summary>
    public void HideToTray() => Hide();

    /// <summary>
    /// Bring the window forward, for a second launch of the app: someone
    /// opening it from the Start menu while it sits in the notification area
    /// wants the window, not a second copy.
    /// </summary>
    public void Reveal() => Show();

    /// <summary>An ICommand that is just a method. Nothing here needs more.</summary>
    private sealed class Do : System.Windows.Input.ICommand
    {
        private readonly Action _run;
        public Do(Action run) => _run = run;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _run();
    }

    /// <summary>
    /// Out of sight, and out of memory as far as Windows is concerned: the
    /// pages we are no longer drawing get pushed out of the working set. They
    /// come back on their own when the window is shown again.
    /// </summary>
    private void FitTitleBar()
    {
        double inset = AppWindow.TitleBar.RightInset / (Dpi() / 96.0);
        Status.Margin = new Thickness(0, 0, inset + 8, 0);
    }

    private void Hide()
    {
        AppWindow.Hide();

        // Unload the page, so what it watches and polls stops while nobody
        // is looking. Showing the window loads it again.
        Body.Content = null;
        TrimWorkingSet();
        TellThemOnce();

        // Trim again while it stays hidden. One trim at the moment of hiding
        // is not enough: the headset reader keeps working, and the pages it
        // touches fault straight back in. Repeating it keeps a day-long
        // background sit at a fraction of what an open window costs.
        if (_trim is null)
        {
            _trim = DispatcherQueue.CreateTimer();
            _trim.Interval = TimeSpan.FromSeconds(60);
            _trim.Tick += (_, _) => TrimWorkingSet();
        }
        _trim.Start();
    }

    /// <summary>
    /// Say, once, that the app is still running. Windows 11 files a new
    /// notification-area icon into the hidden overflow, so without this the
    /// first close looks exactly like quitting: window gone, no icon, mix
    /// still quietly working and no way to tell.
    /// </summary>
    private void TellThemOnce()
    {
        if (AppSettings.Current.ToldAboutTray) return;
        AppSettings.Update(s => s.ToldAboutTray = true);
        try
        {
            Tray.ShowNotification(
                "Still running",
                $"{AppInfo.Name} is in the notification area, keeping the mix and "
                + "the chat wheel working. Open or quit it from there.");
        }
        catch { /* notifications can be off; the setting is still recorded */ }
    }

    private void Show()
    {
        _trim?.Stop();
        if (Body.Content is null && Nav.SelectedItem is NavigationViewItem item)
            Body.Navigate(PageFor(item.Tag as string), null, new SuppressNavigationTransitionInfo());
        AppWindow.Show();
        SetForegroundWindow(Handle);
    }

    private void OnShowRequested(object sender, RoutedEventArgs e) => Show();

    private void OnQuitRequested(object sender, RoutedEventArgs e)
    {
        _quitting = true;
        Tray.Dispose();
        AppServices.Stop();
        Close();
    }

    private void OnNavigate(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        Type page = PageFor(item.Tag as string);
        if (Body.Content is null || Body.CurrentSourcePageType != page)
            Body.Navigate(page, null, new EntranceNavigationTransitionInfo());
    }

    private static Type PageFor(string? tag) => tag switch
    {
        "audio" => typeof(AudioPage),
        "mic" => typeof(MicrophonePage),
        "controls" => typeof(ControlsPage),
        "device" => typeof(DevicePage),
        "settings" => typeof(SettingsPage),
        _ => typeof(HomePage),
    };

    private IntPtr Handle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    private uint Dpi()
    {
        try
        {
            uint dpi = GetDpiForWindow(Handle);
            return dpi == 0 ? 96 : dpi;
        }
        catch { return 96; }
    }

    private static void TrimWorkingSet()
    {
        try { SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1); } catch { }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr min, IntPtr max);
}
