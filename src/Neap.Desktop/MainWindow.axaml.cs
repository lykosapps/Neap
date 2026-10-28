using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Neap.Core;
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

    /// <summary>The width Windows gives its three caption buttons, which the status keeps clear of where the app draws its own title bar.</summary>
    /// <remarks>
    /// Avalonia does not report it: <see cref="Window.WindowDecorationMargin"/>
    /// is the thickness of the frame and title bar, and the buttons are drawn
    /// by Windows over the extended client area. It is the width at 100% scale
    /// in device-independent units, so it holds at other scales.
    /// </remarks>
    private const double CaptionButtons = 138;

    private readonly Dictionary<string, Control> _pages = new();

    /// <summary>Gets the title row's height, which is the height of the title bar where the app draws its own.</summary>
    private double TitleRowHeight => (double)this.FindResource("NeapTitleBarHeight")!;

    public MainWindow()
    {
        InitializeComponent();

        // A pretend run says so wherever the app's name appears, so it is
        // never mistaken for the copy talking to the real headset.
        string title = Pretend.Active ? Strings.Format("Window_PretendTitle", AppInfo.Name) : AppInfo.Name;
        Title = title;

        // A script driving a practice run while somebody works opens it
        // without taking the keyboard from them.
        if (Pretend.Behind) ShowActivated = false;

        Activated += (_, _) => WindowPresence.Set(this, true);
        Deactivated += (_, _) => WindowPresence.Set(this, false);

        Places.SelectionChanged += (_, _) => OnChosen(Places, Foot);
        Foot.SelectionChanged += (_, _) => OnChosen(Foot, Places);
        SizeChanged += (_, e) =>
        {
            FitRail(e.NewSize.Width);
            FitDensity();
        };
        NavToggle.Click += (_, _) => Rail.IsPaneOpen = !Rail.IsPaneOpen;

        // The profile switcher is one icon and the brand only its mark while the rail is icons only; both are in full whenever the rail is open, over the page too.
        Rail.PropertyChanged += (_, e) =>
        {
            if (e.Property != SplitView.IsPaneOpenProperty) return;
            Profile.Compact = !Rail.IsPaneOpen;
            BrandName.IsVisible = Rail.IsPaneOpen;
        };
        BrandName.Text = AppInfo.Name;
        Body.PageTransition = new EntranceTransition();
        DrawOwnTitleBar();

        FirmwareSettingChanged();
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

    /// <summary>Puts the Firmware page in the menu when it is asked for in Settings, and takes it out when it is not.</summary>
    /// <remarks>
    /// Taken out of the list rather than hidden: the list's panel shows every
    /// item it lays out, whatever the item says. Never shown in a release
    /// without firmware edits (<see cref="Core.Firmware.FirmwareEdit.Released"/>),
    /// except to a pretend run asked to open it, so a picture of it can be taken.
    /// </remarks>
    public void FirmwareSettingChanged()
    {
        bool wanted = ((Core.Firmware.FirmwareEdit.Released || Core.Firmware.FirmwareEdit.ParametricReleased) && AppSettings.Current.ShowFirmwareEdits)
            || (Pretend.Active && Pretend.Page == "firmware");
        bool listed = Places.Items.Contains(FirmwareNav);
        if (wanted && !listed) Places.Items.Add(FirmwareNav);
        else if (!wanted && listed) Places.Items.Remove(FirmwareNav);
    }

    /// <summary>Opens the page with this tag, and marks its place in the rail.</summary>
    /// <returns>Whether there is a page with that tag.</returns>
    public bool Open(string page)
    {
        foreach (var rail in new[] { Places, Foot })
            foreach (var item in rail.Items.OfType<NavItem>())
                if (item.Page == page)
                {
                    rail.SelectedItem = item;
                    return true;
                }
        AppLog.Write($"no page with the tag {page}: stayed where it was");
        return false;
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
    }

    /// <summary>Marks the window <c>touch</c> while its screen is small enough for the roomier size, which the styles take their larger sizes from.</summary>
    /// <remarks>Looked at again as the window changes size, which it does as it moves to a screen at another scaling.</remarks>
    private void FitDensity()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        bool small = screen is not null
            && Density.For(screen.WorkingArea.Width / screen.Scaling, screen.WorkingArea.Height / screen.Scaling) == DensityMode.Touch;
        Classes.Set("touch", Pretend.Touch || small);
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
                "equaliser" => new EqualiserPage(),
                "controls" => new ControlsPage(),
                "profiles" => new ProfilesPage(),
                "device" => new DevicePage(),
                "firmware" => new FirmwarePage(),
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
        if (Pretend.Active)
            Pretend.Drives(move => PretendFocus.Move(this, move), page => Dispatcher.UIThread.Invoke(() => Open(page)),
                () => Dispatcher.UIThread.Invoke(() => { if (_mini is null) ToggleMini(); }), PretendPress.Press, PretendColours.At);
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
        if (Pretend.ScrolledBy is double by && Body.Content is Visual page && page.GetVisualDescendants().OfType<PageView>().FirstOrDefault() is { Scroller: { } scroller })
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
