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
    /// The app's one window.
    /// </summary>
    /// <remarks>
    /// Pages use it to move the rail's selection rather than navigate the frame
    /// behind the rail's back and leave the two disagreeing.
    /// </remarks>
    public static MainWindow? Instance { get; private set; }

    /// <summary>Selects the page with this rail tag, as though it had been clicked.</summary>
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

        // Big enough for the equaliser's ten bands side by side, the widest
        // thing in the app and the one that reads badly when cramped. Scaled
        // by the monitor's DPI, because AppWindow works in physical pixels and
        // this would otherwise come up half-size at 200%.
        double scale = Dpi() / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1180 * scale), (int)(900 * scale)));

        Nav.SelectedItem = Nav.MenuItems[0];

        // Closing the window puts the app in the notification area instead of
        // stopping it. The mix, the chat wheel and the headset's own controls
        // are the point of running, and none of them need a window. Quit is on
        // the tray menu.
        AppWindow.Closing += (_, args) =>
        {
            if (_quitting) return;
            args.Cancel = true;
            Hide();
        };

        // The tray icon exposes a command rather than a click event.
        Tray.LeftClickCommand = new Do(Show);

    }

    /// <summary>
    /// Hides the window to the notification area, for a launch at login.
    /// </summary>
    /// <remarks>
    /// Call after Activate, not from the constructor: activation puts the
    /// window on screen, so hiding before it is undone a moment later.
    /// </remarks>
    public void HideToTray() => Hide();

    /// <summary>
    /// Brings the window forward, for a second launch of the app: someone
    /// opening it from the Start menu while it sits in the notification area
    /// wants the window, not a second copy.
    /// </summary>
    public void Reveal() => Show();

    /// <summary>An <see cref="System.Windows.Input.ICommand"/> that runs a method and is always enabled.</summary>
    private sealed class Do : System.Windows.Input.ICommand
    {
        private readonly Action _run;
        public Do(Action run) => _run = run;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _run();
    }

    /// <summary>
    /// Keeps the status strip clear of the window's caption buttons, however
    /// wide Windows draws them.
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
        // touches fault straight back in. Repeating it keeps a day in the
        // background at a fraction of what an open window costs.
        if (_trim is null)
        {
            _trim = DispatcherQueue.CreateTimer();
            _trim.Interval = TimeSpan.FromSeconds(60);
            _trim.Tick += (_, _) => TrimWorkingSet();
        }
        _trim.Start();
    }

    /// <summary>
    /// Shows a notification, once ever, that the app is still running.
    /// </summary>
    /// <remarks>
    /// Windows 11 files a new notification-area icon into the hidden overflow,
    /// so without this the first close looks exactly like quitting: window
    /// gone, no icon, and the mix still working with no way to tell.
    /// </remarks>
    private void TellThemOnce()
    {
        if (AppSettings.Current.ToldAboutTray) return;
        AppSettings.Update(s => s.ToldAboutTray = true);
        try
        {
            Tray.ShowNotification(
                Strings.Get("Tray_StillRunningTitle"),
                Strings.Format("Tray_StillRunning", AppInfo.Name));
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

    /// <summary>
    /// Pushes the pages no longer being drawn out of the working set. Windows
    /// faults them back in on its own when the window is shown again.
    /// </summary>
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
