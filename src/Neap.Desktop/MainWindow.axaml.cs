using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Neap.Desktop.Controls;
using Neap.Desktop.Localization;
using Neap.Desktop.Views;

namespace Neap.Desktop;

/// <summary>The window: the title row, the rail of places and the page in front.</summary>
/// <remarks>
/// A page is made when first opened and kept, so going back to it finds
/// what was there and nothing is read from the headset again.
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>The width above which the rail shows names as well as icons; the Windows app's own threshold.</summary>
    private const double Roomy = 1008;

    /// <summary>The width below which the rail folds into a button that opens it over the page; the Windows app's own threshold.</summary>
    private const double Narrow = 641;

    /// <summary>The title row's height, which is the height of the title bar where the app draws its own.</summary>
    private const double TitleRowHeight = 40;

    /// <summary>The width Windows gives its three caption buttons, which the status keeps clear of where the app draws its own title bar.</summary>
    private const double CaptionButtons = 138;

    private readonly Dictionary<string, Control> _pages = new();

    public MainWindow()
    {
        InitializeComponent();

        // A pretend run says so wherever the app's name appears, so it is
        // never mistaken for the copy talking to the real headset.
        string title = Pretend.Active ? Strings.Format("Window_PretendTitle", AppInfo.Name) : AppInfo.Name;
        Title = title;

        Activated += (_, _) => WindowPresence.Set(true);
        Deactivated += (_, _) => WindowPresence.Set(false);

        Places.SelectionChanged += (_, _) => OnChosen(Places, Foot);
        Foot.SelectionChanged += (_, _) => OnChosen(Foot, Places);
        SizeChanged += (_, e) => FitRail(e.NewSize.Width);
        NavToggle.Click += (_, _) => Rail.IsPaneOpen = !Rail.IsPaneOpen;
        Body.PageTransition = new EntranceTransition();
        DrawOwnTitleBar();

        Open(Pretend.Page ?? "home");
        StartTray();
        AddHandler(KeyDownEvent, OnPlaceKey, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Alt and a place's letter goes to that place: the letters are the
    /// resource file's, one per place, the same as in the Windows app.
    /// </summary>
    private void OnPlaceKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Alt) return;
        foreach (var rail in new[] { Places, Foot })
            foreach (var item in rail.Items.OfType<NavItem>())
            {
                if (Uid.GetValue(item) is not { } uid) continue;
                var letter = ResourceStrings.Of(uid).FirstOrDefault(entry => entry.Key == "AccessKey").Value;
                if (letter is not { Length: 1 } || !Enum.TryParse(letter, ignoreCase: true, out Key key) || key != e.Key) continue;
                Open(item.Page);
                e.Handled = true;
                return;
            }
    }

    /// <summary>Gets the page on show.</summary>
    public Control? CurrentPage => Body.Content as Control;

    /// <summary>Opens the page with this tag, and marks its place in the rail.</summary>
    public void Open(string page)
    {
        foreach (var rail in new[] { Places, Foot })
            foreach (var item in rail.Items.OfType<NavItem>())
                if (item.Page == page)
                {
                    rail.SelectedItem = item;
                    return;
                }
        AppLog.Write($"no page with the tag {page}: stayed where it was");
    }

    /// <summary>
    /// Shows the rail as the width allows: names beside the icons when there
    /// is room, icons alone when there is less, and in a narrow window only a
    /// button, which opens the rail over the page.
    /// </summary>
    private void FitRail(double width)
    {
        bool folded = width < Narrow;
        Rail.DisplayMode = folded ? SplitViewDisplayMode.Overlay : SplitViewDisplayMode.CompactInline;
        Rail.IsPaneOpen = !folded && width >= Roomy;
        NavToggle.IsVisible = folded;

        // The page moves down clear of the button, rather than have it sit against the page's title.
        PageFrame.Margin = folded ? new Thickness(0, TitleRowHeight, 0, 0) : new Thickness(0);
    }

    /// <summary>
    /// Where the app draws its own title bar, as on Windows, with the system's
    /// name, mark and buttons over it, keeps the status clear of the buttons.
    /// Elsewhere the desktop's own title bar stays.
    /// </summary>
    private void DrawOwnTitleBar()
    {
        if (!OperatingSystem.IsWindows()) return;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = TitleRowHeight;
        Status.Margin = new Thickness(0, 0, CaptionButtons + 16, 0);
    }

    /// <summary>A place was chosen in one half of the rail: show it, and let go of any choice in the other.</summary>
    private void OnChosen(ListBox chosen, ListBox other)
    {
        if (chosen.SelectedItem is not NavItem item) return;
        if (other.SelectedItem is not null) other.SelectedItem = null;
        Show(item);

        // A rail opened over the page has done its job once a place is chosen.
        if (Rail.DisplayMode == SplitViewDisplayMode.Overlay) Rail.IsPaneOpen = false;
    }

    /// <summary>Puts the page of the place chosen in the rail in front again, after the window was hidden.</summary>
    private void ShowCurrentPage()
    {
        if (Places.SelectedItem is NavItem place) Show(place);
        else if (Foot.SelectedItem is NavItem foot) Show(foot);
    }

    private void Show(NavItem item)
    {
        if (!_pages.TryGetValue(item.Page, out var page))
            _pages[item.Page] = page = item.Page switch
            {
                "home" => new HomePage(),
                "audio" => new AudioPage(),
                "mic" => new MicrophonePage(),
                "controls" => new ControlsPage(),
                "profiles" => new ProfilesPage(),
                "device" => new DevicePage(),
                "settings" => new SettingsPage(),
                _ => new NotYet(),
            };
        Body.Content = page;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (!Pretend.Active) Place();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        FitRail(Bounds.Width);
        if (Pretend.Snapshot is { } file) _ = SaveAndExit(file);

        // A launch at sign-in lives in the notification area rather than open
        // a window in front of whatever somebody was about to do. After the
        // window is on screen: hidden before it, it is shown a moment later.
        else if (Startup.LaunchedAtLogin)
        {
            if (TrayAvailability.Exists) HideToTray();
            else WindowState = WindowState.Minimized;
        }
    }

    /// <summary>A pretend run's picture of the window, once the headset has answered and the screens have settled.</summary>
    private async Task SaveAndExit(string file)
    {
        await Task.Delay(Pretend.SettleTime);
        if (Pretend.ScrolledBy is double by && (Body.Content as UserControl)?.Content is ScrollViewer scroller)
        {
            if (double.IsInfinity(by)) scroller.ScrollToEnd();
            else scroller.Offset = new Vector(0, by);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
        using var picture = new RenderTargetBitmap(size, new Vector(96, 96));
        picture.Render(this);
        picture.Save(file, new PngBitmapEncoderOptions());
        AppLog.Write($"saved a picture of the window to {file}");
        _quitting = true;
        Close();
    }
}
