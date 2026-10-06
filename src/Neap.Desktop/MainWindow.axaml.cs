using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Neap.Desktop.Controls;
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

    private readonly Dictionary<string, Control> _pages = new();

    public MainWindow()
    {
        InitializeComponent();

        // A pretend run says so wherever the app's name appears, so it is
        // never mistaken for the copy talking to the real headset.
        string title = Pretend.Active ? Strings.Format("Window_PretendTitle", AppInfo.Name) : AppInfo.Name;
        Title = title;
        TitleText.Text = title;

        Activated += (_, _) => WindowPresence.Set(true);
        Deactivated += (_, _) => WindowPresence.Set(false);

        Places.SelectionChanged += (_, _) => OnChosen(Places, Foot);
        Foot.SelectionChanged += (_, _) => OnChosen(Foot, Places);
        SizeChanged += (_, e) => Rail.IsPaneOpen = e.NewSize.Width >= Roomy;

        Open(Pretend.Page ?? "home");
        StartTray();
    }

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

    /// <summary>A place was chosen in one half of the rail: show it, and let go of any choice in the other.</summary>
    private void OnChosen(ListBox chosen, ListBox other)
    {
        if (chosen.SelectedItem is not NavItem item) return;
        if (other.SelectedItem is not null) other.SelectedItem = null;
        Show(item);
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

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Rail.IsPaneOpen = Bounds.Width >= Roomy;
        if (Pretend.Snapshot is { } file) _ = SaveAndExit(file);

        // A launch at sign-in lives in the notification area rather than open
        // a window in front of whatever somebody was about to do. After the
        // window is on screen: hidden before it, it is shown a moment later.
        else if (Startup.LaunchedAtLogin) HideToTray();
    }

    /// <summary>A pretend run's picture of the window, once the headset has answered and the screens have settled.</summary>
    private async Task SaveAndExit(string file)
    {
        await Task.Delay(TimeSpan.FromSeconds(4));
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
