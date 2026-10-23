using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Neap.Core;
using Neap.Core.Connection;
using Neap.Core.Diagnostics;
using Neap.Core.Mix;

namespace Neap.Desktop.Controls;

/// <summary>
/// The game/chat crossfade as a dial, and the choice of which applications
/// carry chat.
/// </summary>
/// <remarks>
/// <para>
/// Before an application is chosen there is nothing to mix, so the dial is
/// absent rather than disabled, and the panel only asks which applications
/// carry chat, with the list open. Once one is chosen the list folds
/// beneath the dial to a line naming them.
/// </para>
/// <para>
/// Beside the dial, each side shows how loud the mix lets it play, not a
/// share: a balanced mix plays both at full level. <see cref="MixLevels"/>
/// decides both numbers and the word on the dial's cap. Each side's meter
/// shows what it is playing just now.
/// </para>
/// <para>
/// It stands on its own, needing nothing from the page around it, and can say
/// for itself why the chat wheel is not reaching it (<see cref="ExplainsWheel"/>),
/// so the same panel can sit anywhere the mix is wanted.
/// </para>
/// <para>
/// Naming the application is the whole configuration: no virtual audio
/// cable, install, reboot or second audio device is needed.
/// </para>
/// </remarks>
public partial class MixPanel : UserControl
{
    public static readonly StyledProperty<bool> ExplainsWheelProperty =
        AvaloniaProperty.Register<MixPanel, bool>(nameof(ExplainsWheel), defaultValue: true);

    /// <summary>The narrowest the panel can be and still hold the dial beside its own column.</summary>
    private const double SideBySide = 480;

    private bool _painting;

    /// <summary>How often the meters are read: often enough to follow speech, as the microphone meter does.</summary>
    private static readonly TimeSpan LevelReading = TimeSpan.FromMilliseconds(40);

    private ISideMeter? _levels;
    private IDisposable? _levelTimer;
    private SideLevel _levelShown;
    private bool _levelReading;

    /// <summary>What the list last showed each application as, by process name.</summary>
    private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);

    public MixPanel()
    {
        InitializeComponent();

        Dial.ValueChanged += (_, args) =>
        {
            if (_painting) return;
            AppServices.Mix.Apply((int)Math.Round(args.NewValue));
        };
        Refresh.Click += (_, _) => _ = LoadCandidates();
        Layout.SizeChanged += (_, _) => Arrange();
        KeysLink.IsVisible = AppServices.Hotkeys.Supported;
        KeysLink.Click += (_, _) => (TopLevel.GetTopLevel(this) as MainWindow)?.Open("controls");
    }

    /// <summary>
    /// Puts the dial beside the panel's own column when there is room, and
    /// above it when there is not.
    /// </summary>
    private void Arrange()
    {
        bool beside = Layout.Bounds.Width >= SideBySide;
        bool stacked = !beside;
        Layout.ColumnSpacing = beside ? Space.XL : 0;
        Layout.RowSpacing = stacked ? Space.M : 0;
        Grid.SetRow(Side, stacked ? 1 : 0);
        Grid.SetColumn(Side, stacked ? 0 : 1);
        Grid.SetColumnSpan(Side, stacked ? 2 : 1);
        Grid.SetColumnSpan(DialArea, stacked ? 2 : 1);
    }

    /// <summary>
    /// Gets or sets whether the mix itself says the chat wheel is not reaching
    /// the app while a transmitter is lost. False where a note beside it
    /// already says so, so one problem is not explained twice. The cable is
    /// always said here, as no note beside it covers that.
    /// </summary>
    public bool ExplainsWheel
    {
        get => GetValue(ExplainsWheelProperty);
        set => SetValue(ExplainsWheelProperty, value);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Mix.Changed += Paint;
        AppServices.Headset.StatusChanged += OnHeadset;
        // The wheel's notice names the keys once they are on.
        AppServices.Hotkeys.Changed += Paint;
        WindowPresence.Changed += WatchLevels;
        Paint();
        _ = LoadCandidates();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Mix.Changed -= Paint;
        AppServices.Headset.StatusChanged -= OnHeadset;
        AppServices.Hotkeys.Changed -= Paint;
        WindowPresence.Changed -= WatchLevels;
        if (!IsLoaded) StopLevels();
    }

    /// <summary>
    /// Repaints on a change of headset status. What reaches the mix depends on
    /// the transmitter, so the panel follows the headset as well as the mix.
    /// </summary>
    private void OnHeadset(HeadsetStatus status) => Paint();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ExplainsWheelProperty && IsLoaded) Paint();
    }

    /// <summary>Adds an application to those that carry chat, or takes it away.</summary>
    private static void Choose(string process, bool on)
    {
        var apps = AppServices.Mix.ChatApps
            .Where(a => !string.Equals(a, process, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (on) apps.Add(process);
        AppServices.Mix.ChatApps = apps;
    }

    /// <summary>Lists the applications playing to the headset, a check box each.</summary>
    /// <remarks>
    /// Cheap enough to run on arrival, and re-run on demand, since a chat
    /// application may have started since.
    /// </remarks>
    private async Task LoadCandidates()
    {
        var candidates = await AppServices.Mix.Candidates("the list of chat apps");
        var chosen = AppServices.Mix.ChatApps;

        AppList.Children.Clear();
        _names.Clear();
        foreach (var candidate in candidates)
        {
            _names[candidate.Process] = candidate.Display;
            var box = new CheckBox
            {
                Content = candidate.Playing
                    ? Strings.Format("Mix_Playing", candidate.Display)
                    : candidate.Display,
                IsChecked = chosen.Contains(candidate.Process, StringComparer.OrdinalIgnoreCase),
            };
            string process = candidate.Process;
            box.IsCheckedChanged += (_, _) => Choose(process, box.IsChecked == true);
            AppList.Children.Add(box);
        }
        Nothing.IsOpen = candidates.Count == 0;
        MissingHint.IsVisible = !Nothing.IsOpen;
        Paint();
    }

    /// <summary>
    /// Reads the levels only while the meters are on screen, the window is in
    /// front and animation is on. Animation off in the system means nothing
    /// moves by itself, and the meters are decoration, so they are not shown;
    /// nor where the system has no meter on each application.
    /// </summary>
    private void WatchLevels()
    {
        bool wanted = this.InWindow() && DialArea.IsEnabled && WindowPresence.InFront && AppServices.Mix.Running && Motion.Enabled;
        if (!wanted) { StopLevels(); return; }
        if (_levelTimer is not null) return;
        _levels ??= AppServices.Mix.OpenLevels();
        ShowMeters(_levels is not null);
        if (_levels is not null) _levelTimer = Platform.Current.Ui.Every(LevelReading, ReadLevels);
    }

    private void ShowMeters(bool shown)
    {
        GameBars.IsVisible = shown;
        ChatBars.IsVisible = shown;
    }

    private void PaintMeters(SideLevel level)
    {
        GameBars.Paint(SideLevels.Lit(level.Game, GameBars.Count));
        ChatBars.Paint(SideLevels.Lit(level.Chat, ChatBars.Count));
    }

    private void StopLevels()
    {
        _levelTimer?.Dispose();
        _levelTimer = null;
        _levelShown = default;
        PaintMeters(default);
        if (!Motion.Enabled) ShowMeters(false);
        if (_levels is not { } meter) return;
        _levels = null;
        _ = Task.Run(meter.Dispose);
    }

    private async void ReadLevels()
    {
        if (_levelReading || _levels is not { } meter) return;
        _levelReading = true;
        try
        {
            var heard = await Task.Run(meter.Read);
            if (_levelTimer is null) return;
            _levelShown = SideLevels.Next(_levelShown, heard);
            PaintMeters(_levelShown);
        }
        catch (Exception ex)
        {
            AppLog.Write($"mix: the meters could not be read: {ex.Message}");
            StopLevels();
        }
        finally { _levelReading = false; }
    }

    private void Paint()
    {
        var apps = AppServices.Mix.ChatApps;
        bool chosen = apps.Count > 0;
        SetupLine.IsVisible = !chosen;
        // Kept in the layout while nothing is chosen, so the card's height
        // does not change with the first app; unseen, unread and switched off
        // until then.
        Levels.Opacity = chosen ? 1 : 0;
        Levels.IsEnabled = chosen;
        AutomationProperties.SetAccessibilityView(Levels, chosen ? AccessibilityView.Content : AccessibilityView.Raw);
        DialArea.IsEnabled = chosen;
        WatchLevels();

        ChosenText.Text = chosen
            ? Strings.List(apps.Select(a => _names.GetValueOrDefault(a, a)).ToList())
            : Strings.Get("Mix_NoneChosen");
        AutomationProperties.SetName(ChatFromButton, Strings.Format("Mix_ChatFromName", ChosenText.Text));

        var status = AppServices.Headset.Status;

        // The wheel arrives over the very link that is missing, so the app
        // cannot fix this itself; it says what still moves the mix.
        WheelBar.IsOpen = chosen && (status.WheelOffOnCable || (ExplainsWheel && status.WheelUnreachable))
            && HeadsetModels.Shows(AppServices.Headset.Model, Feature.ChatWheel);
        WheelBar.Title = Strings.Get(status.WheelOffOnCable ? "Mix_WheelOnCable" : "Mix_WheelBar.Title");
        WheelBar.Message = StateCopy.MixWithoutWheel();

        // The chat application is playing to another device. It keeps its own
        // output setting, so changing the headset in the system does not move
        // it; name the app and the device so the person can.
        //
        // Not when that device is the transmitter the headset is on. Then chat
        // is in the right place and the system is not, which the routing
        // warning says with the right fix; this one would say to move chat
        // away from the headset.
        var elsewhere = chosen ? AppServices.Mix.Status?.Elsewhere : null;
        if (elsewhere is not null && status.Product.Length > 0
            && AudioRoute.Belonging(status.Product, output: true)
                .Contains(elsewhere.Device, StringComparer.OrdinalIgnoreCase))
            elsewhere = null;
        ElsewhereBar.IsOpen = elsewhere is not null;
        if (elsewhere is not null)
            ElsewhereBar.Message = Strings.Format("Mix_Elsewhere", elsewhere.App, elsewhere.Device);

        _painting = true;
        try
        {
            int mix = AppServices.Mix.Mix;
            int game = MixLevels.Game(mix), chat = MixLevels.Chat(mix);
            Dial.Value = mix;
            GameLevel.Text = game.ToString(CultureInfo.CurrentCulture);
            ChatLevel.Text = chat.ToString(CultureInfo.CurrentCulture);
            LeanText.Text = MixLevels.Lean(mix) switch
            {
                MixLean.GameOnly => Strings.Get("Mix_GameOnly"),
                MixLean.TowardGame => Strings.Get("Mix_TowardGame"),
                MixLean.Balanced => Strings.Get("Mix_Balanced"),
                MixLean.TowardChat => Strings.Get("Mix_TowardChat"),
                _ => Strings.Get("Mix_ChatOnly"),
            };
            // The dial's number is its position; what it means is said with it.
            AutomationProperties.SetHelpText(Dial, Strings.Format("Mix_Levels", LeanText.Text, game, chat));
        }
        finally { _painting = false; }
    }
}
