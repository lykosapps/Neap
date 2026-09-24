using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using StealthPro.App.Services;
using StealthPro.App.Views;
using StealthPro.Core;
using Windows.Graphics;

namespace StealthPro.App;

public sealed partial class MainWindow : Window
{
    private const int FirstWidth = 1180, FirstHeight = 900;
    private const int MinimumWidth = 500, MinimumHeight = 480;

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

        // A pretend run says so wherever the app's name appears, so it is
        // never mistaken for the copy talking to the real headset.
        string title = Pretend.Active ? Strings.Format("Window_PretendTitle", AppInfo.Name) : AppInfo.Name;
        Title = title;
        TitleText.Text = title;
        Tray.ToolTipText = title;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);

        // The window's own buttons follow the Windows theme as it changes.
        // Left to themselves they keep the colours of the theme the app
        // started in, and after a switch to light they are white on white.
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

        // Clear of the window's own buttons, however wide Windows draws them.
        AppWindow.Changed += (_, _) => FitTitleBar();
        FitTitleBar();

        Place();

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
    /// Shows the window beneath every other one without activating it, for a
    /// pretend run a script drives while somebody works.
    /// </summary>
    /// <remarks>
    /// It stays drawn, so the script can still read it and take screenshots,
    /// but it never covers anything, and it is made a window Windows does not
    /// activate: moving between pages through UI Automation otherwise brings
    /// it to the front, and the keys somebody is typing go to it.
    /// </remarks>
    public void ShowBehind()
    {
        SetWindowLongPtr(Handle, ExtendedStyle, GetWindowLongPtr(Handle, ExtendedStyle) | NoActivate);
        AppWindow.Show(activateWindow: false);
        SetWindowPos(Handle, HwndBottom, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
    }

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

    /// <summary>
    /// Opens the window where it was left, or centred on the main screen the
    /// first time, and sets how small it can be made.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first size is big enough for the equaliser's ten bands side by
    /// side, the widest thing in the app and the one that reads badly when
    /// cramped. Sizes are scaled by the monitor's DPI, because AppWindow works
    /// in physical pixels and they would otherwise come up half-size at 200%.
    /// </para>
    /// <para>
    /// A pretend run always opens at the first size, so its screenshots can be
    /// compared from one run to the next.
    /// </para>
    /// </remarks>
    private void Place()
    {
        double scale = Dpi() / 96.0;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(MinimumWidth * scale);
            presenter.PreferredMinimumHeight = (int)(MinimumHeight * scale);
        }

        Box? saved = !Pretend.Active && AppSettings.Current.Window is [int x, int y, int w, int h]
            ? new Box(x, y, w, h) : null;
        Box? savedArea = null;
        if (saved is { } box)
        {
            var (cx, cy) = WindowPlacement.Centre(box);
            savedArea = DisplayArea.GetFromPoint(new PointInt32(cx, cy), DisplayAreaFallback.None) is { } display
                ? Of(display.WorkArea) : null;
        }

        var place = WindowPlacement.Fit(saved, savedArea, Of(DisplayArea.Primary.WorkArea),
            (int)(FirstWidth * scale), (int)(FirstHeight * scale));
        AppWindow.MoveAndResize(new RectInt32(place.X, place.Y, place.Width, place.Height));

        static Box Of(RectInt32 rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>Records where the window is, for the next launch. Not while maximised or minimised.</summary>
    private void Remember()
    {
        if (Pretend.Active
            || AppWindow.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Restored })
            return;
        var (at, size) = (AppWindow.Position, AppWindow.Size);
        AppSettings.Update(s => s.Window = new[] { at.X, at.Y, size.Width, size.Height });
    }

    private void Hide()
    {
        Remember();
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
        Remember();
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

    private static readonly IntPtr HwndBottom = new(1);
    private const uint SwpNoSize = 0x0001, SwpNoMove = 0x0002, SwpNoActivate = 0x0010;

    private const int ExtendedStyle = -20;
    private const nint NoActivate = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(IntPtr window, int index, nint value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr min, IntPtr max);
}
