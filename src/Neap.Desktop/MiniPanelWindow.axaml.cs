using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Neap.Core;
using Neap.Core.Connection;
using Neap.Core.Diagnostics;
using Neap.Core.Mix;
using Neap.Core.Settings;
using Neap.Desktop.Controls;

namespace Neap.Desktop;

/// <summary>
/// The mini panel: the things changed mid-game, in a small window by the
/// notification area that goes away when it loses focus.
/// </summary>
/// <remarks>
/// <para>
/// The mix, the microphone, noise cancellation, spatial sound, Superhuman
/// Hearing and the equaliser preset are the controls Home has, with the same
/// logic behind them; this only lays them out small. <see cref="MiniPanel"/>
/// decides where it sits.
/// </para>
/// <para>
/// The mix is always shown. The rest are the ones the person chose in
/// Settings, read when the panel opens, since it is made anew each time;
/// <see cref="MiniTiles"/> decides what is shown and how they are set out.
/// </para>
/// <para>
/// It closes on Escape and when another window takes focus, as the system's
/// volume flyout does. Opening the equaliser's presets takes focus to their
/// list without closing the panel; it closes once the list does, if focus
/// went elsewhere meanwhile.
/// </para>
/// <para>
/// A pretend run with <see cref="Pretend.Mini"/> shows it alone as the app's
/// only window, which stays open so it can be looked at or pictured.
/// </para>
/// </remarks>
public partial class MiniPanelWindow : Window
{
    /// <summary>The gap kept between the panel and the edge of the work area, in device-independent pixels.</summary>
    private const double EdgeGap = 12;

    private static readonly SettingKey HearingOn = Registry.ByName["superhuman_hearing"];
    private static readonly SettingKey HearingSound = Registry.ByName["shh_preset"];

    private readonly MainWindow? _main;
    private readonly IReadOnlySet<MiniTile> _chosen = MiniTiles.Chosen(AppSettings.Current.MiniTiles);
    private readonly BatteryMeter _battery = new("caption");
    private IDisposable? _recheck;
    private bool _painting;

    public MiniPanelWindow()
    {
        InitializeComponent();
        Title = Strings.Get("Main_TrayMini");
        Classes.Set("touch", Pretend.Touch || OnSmallScreen());
        BatteryHolder.Content = _battery;
        WindowBehind.Keep(this);

        Dial.ValueChanged += (_, args) =>
        {
            if (!_painting) AppServices.Mix.Apply((int)Math.Round(args.NewValue));
        };
        OpenNeap.Click += (_, _) => OpenWindow(null);
        ChooseApps.Click += (_, _) => OpenWindow("home");
        Preset.DropDownClosed += (_, _) => CloseIfAway();
        Preset.EqualiserAsked += () =>
        {
            OpenWindow("equaliser");
            (_main?.CurrentPage as Views.EqualiserPage)?.ShowGame();
        };
        Activated += (_, _) => WindowPresence.Set(this, true);
        Closed += (_, _) => WindowPresence.Set(this, false);
        Deactivated += (_, _) =>
        {
            WindowPresence.Set(this, false);
            Dispatcher.UIThread.Post(CloseIfAway);
        };
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || e.Handled) return;
            e.Handled = true;
            Close();
        };
        SizeChanged += (_, _) => Place();

        NoiseTile.Icon = (Geometry)Application.Current!.FindResource("IconNoise")!;
        NoiseTile.Describe(Strings.Get("Mini_NoiseTile"), Strings.Get("Mini_Noise"), Strings.Get("Mini_NoiseChoices"));
        NoiseTile.Pressed += () =>
        {
            var noise = AppServices.Noise;
            if (noise.CanChoose && noise.Mode is { } mode) noise.Choose(QuickToggle.Press(mode, NoiseMode.Off, noise.LastOn));
        };

        HearingTile.Icon = (Geometry)Application.Current!.FindResource("IconHearing")!;
        HearingTile.Describe(Strings.Get("Mini_HearingTile"), Strings.Get("Quick_Shh.Text"), Strings.Get("Mini_HearingChoices"));
        HearingTile.Pressed += () =>
        {
            // Turned on when the headset has not said what it is.
            if (CanWrite(HearingOn)) AppServices.Headset.SetKey(HearingOn.Key, Hearing() == 1 ? 0 : 1);
        };
    }

    /// <summary>Gets the window it opens when asked; null for a pretend run showing it alone.</summary>
    public MainWindow? Main
    {
        get => _main;
        init => _main = value;
    }

    /// <summary>Whether the screen it opens on is small enough for the roomier touch size.</summary>
    /// <remarks>The screen, as the main window does; see <see cref="Density"/>.</remarks>
    private bool OnSmallScreen() =>
        Screens.Primary is { } screen
        && Density.For(screen.WorkingArea.Width / screen.Scaling, screen.WorkingArea.Height / screen.Scaling) == DensityMode.Touch;

    /// <summary>Gets when it closed, as a <see cref="Stopwatch"/> timestamp, so the icon's next click can tell whether that click was the one that did it.</summary>
    public long ClosedAt { get; private set; }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        AppServices.Headset.Changed += Paint;
        AppServices.Headset.StatusChanged += OnStatus;
        AppServices.Mix.Changed += Paint;
        AppServices.Noise.Changed += PaintNoise;
        Paint();
        PaintNoise();
        Place();

        // Focus begins on the dial, on the way to choose chat apps while the dial waits for
        // them, or else on the first thing that can take it. Given without the focus ring,
        // since the panel is mostly opened with a click; the ring shows from the first Tab.
        InputElement? first = Dial.IsEffectivelyVisible && Dial.IsEffectivelyEnabled ? Dial
            : ChooseApps.IsEffectivelyVisible ? ChooseApps
            : this.GetVisualDescendants().OfType<InputElement>()
                .FirstOrDefault(c => c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled);
        Dispatcher.UIThread.Post(() => first?.Focus());

        if (Pretend.Snapshot is { } file) _ = SaveAndExit(file);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        AppServices.Headset.Changed -= Paint;
        AppServices.Headset.StatusChanged -= OnStatus;
        AppServices.Mix.Changed -= Paint;
        AppServices.Noise.Changed -= PaintNoise;
        StopRechecking();
        ClosedAt = Stopwatch.GetTimestamp();
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    /// <summary>Opens the main window, on a page if one is given, and puts this away.</summary>
    private void OpenWindow(string? page)
    {
        if (_main is null) return;
        _main.Reveal();
        if (page is not null) _main.Open(page);
        Close();
    }

    /// <summary>Closes the panel once focus has gone elsewhere, unless the presets are open, which hold it.</summary>
    private void CloseIfAway()
    {
        if (Pretend.Mini || IsActive || Preset.IsDropDownOpen) return;
        Close();
    }

    /// <summary>Puts the panel in the corner by the notification area, and keeps it there as it grows or shrinks.</summary>
    private void Place()
    {
        if (Screens.Primary is not { } screen || Bounds.Height <= 0) return;
        double scale = RenderScaling;
        var box = MiniPanel.Place(Of(screen.Bounds), Of(screen.WorkingArea),
            (int)Math.Ceiling(Bounds.Width * scale), (int)Math.Ceiling(Bounds.Height * scale), (int)Math.Round(EdgeGap * scale));
        MaxHeight = box.Height / scale;
        var at = new PixelPoint(box.X, box.Y);
        if (Position != at) Position = at;

        static Box Of(PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
    }

    private void Paint()
    {
        var headset = AppServices.Headset;
        HeadsetName.Text = HeadsetTitle.Current();

        if (headset.Battery is { } battery)
        {
            if (battery.Settling) _battery.Show(null, false, Strings.Get("Home_BatterySettling"));
            else _battery.Show(battery.Percent, battery.Charging, Strings.Format("Home_BatteryLevel", battery.Percent));

            // Nothing says when a settling reading has settled, so look again until it has.
            if (battery.Settling) _recheck ??= Platform.Current.Ui.Every(TimeSpan.FromSeconds(30), Paint);
            else StopRechecking();
        }
        else StopRechecking();
        BatteryHolder.IsVisible = headset.Battery is not null && headset.Status.Link == Link.Connected;

        PaintTiles();
        PaintHearing();

        // The state line only when something is wrong: connected, the battery says enough.
        var status = headset.Status;
        Status.IsVisible = StatusLook.Of(status, AppServices.AudioRoute.SoundElsewhere(status), headset.AccessDenied).Headline
            != Headline.Connected;

        bool chosen = AppServices.Mix.ChatApps.Count > 0;
        ChooseApps.IsVisible = !chosen;
        DialArea.IsVisible = chosen;

        _painting = true;
        try
        {
            int mix = AppServices.Mix.Mix;
            Dial.Value = mix;
            string lean = MixLevels.Lean(mix) switch
            {
                MixLean.GameOnly => Strings.Get("Mix_GameOnly"),
                MixLean.TowardGame => Strings.Get("Mix_TowardGame"),
                MixLean.Balanced => Strings.Get("Mix_Balanced"),
                MixLean.TowardChat => Strings.Get("Mix_TowardChat"),
                _ => Strings.Get("Mix_ChatOnly"),
            };
            AutomationProperties.SetHelpText(Dial, Strings.Format("Mix_Levels", lean, MixLevels.Game(mix), MixLevels.Chat(mix)));
        }
        finally { _painting = false; }
    }

    /// <summary>Shows the tiles the person chose, where the headset has them, and sets them out.</summary>
    private void PaintTiles()
    {
        var headset = AppServices.Headset;
        Mic.IsVisible = _chosen.Contains(MiniTile.Microphone);
        NoiseTile.IsVisible = _chosen.Contains(MiniTile.NoiseCancellation) && HeadsetModels.Shows(headset.Model, Feature.NoiseControl);
        Spatial.IsVisible = _chosen.Contains(MiniTile.SpatialSound) && Platform.Current.Spatial.Supported;
        HearingTile.IsVisible = _chosen.Contains(MiniTile.SuperhumanHearing) && HeadsetModels.Shows(headset.Model, HearingOn.Name);
        PresetRow.IsVisible = _chosen.Contains(MiniTile.EqualiserPreset);

        int shown = Tiles.Children.Count(tile => tile.IsVisible);
        Tiles.IsVisible = shown > 0;
        Tiles.Columns = MiniTiles.Columns(shown);
        TilesArea.IsVisible = shown > 0 || PresetRow.IsVisible;
    }

    /// <summary>Gets Superhuman Hearing's value, or null where the headset has not reported it.</summary>
    private static int? Hearing() => AppServices.Headset.TryGetNumber(HearingOn.Name, out int on) ? on : null;

    /// <summary>Gets whether a setting can be changed: one Neap writes, on a headset whose settings it changes.</summary>
    private static bool CanWrite(SettingKey key) => key.Writable && !AppServices.Headset.ReadOnly;

    /// <summary>Gets whether the headset has reported a setting, or has it where Neap cannot yet read it, so it can still be tried.</summary>
    private static bool Readable(SettingKey key) =>
        AppServices.Headset.TryGetNumber(key.Name, out _) || HeadsetModels.Unread(AppServices.Headset.Model, key.Name);

    /// <summary>Shows Superhuman Hearing on its tile: on with its sound, or off, and its sounds in the tile's list while it is on.</summary>
    /// <remarks>The list is left off while Superhuman Hearing is, as the sounds are on Home, since a sound means nothing then.</remarks>
    private void PaintHearing()
    {
        var headset = AppServices.Headset;
        int? on = Hearing();
        int? sound = headset.TryGetNumber(HearingSound.Name, out int heard) ? heard : null;

        string state = on is null ? Strings.Get("Reading_None")
            : on != 1 ? Strings.Get("Switch_Off")
            : sound is { } chosen ? OptionNames.For(HearingSound, chosen)
            : Strings.Get("Switch_On");
        HearingTile.Show(on == 1, state, CanWrite(HearingOn) && Readable(HearingOn));
        HearingTile.Offer(
            (HearingSound.Options ?? new Dictionary<int, string>()).Keys.Select(option =>
                (OptionNames.For(HearingSound, option), option == sound, (Action)(() => headset.SetKey(HearingSound.Key, option)))),
            on == 1 && CanWrite(HearingSound) && Readable(HearingSound));
    }

    /// <summary>Shows noise control on its tile: on in either of its two ways, and both ways and off in its list.</summary>
    private void PaintNoise()
    {
        var noise = AppServices.Noise;
        var mode = noise.Mode;
        NoiseTile.Show(mode is NoiseMode.Cancelling or NoiseMode.Transparency,
            Strings.Get(mode is { } known ? QuickSettings.NoiseModeWord(known) : "Reading_None"), noise.CanChoose);
        NoiseTile.Offer(
            Enum.GetValues<NoiseMode>().Select(choice =>
                (Strings.Get(QuickSettings.NoiseModeWord(choice)), choice == mode, (Action)(() => noise.Choose(choice)))),
            noise.CanChoose);
    }

    private void StopRechecking()
    {
        _recheck?.Dispose();
        _recheck = null;
    }

    /// <summary>A pretend run's picture of the panel, once the headset has answered and it has settled.</summary>
    /// <remarks>Drawn on the app's ground rather than on nothing, so the card's rounded corners can be judged.</remarks>
    private async Task SaveAndExit(string file)
    {
        this.Bind(BackgroundProperty, this.GetResourceObservable("NeapGroundBrush"));
        await Task.Delay(Pretend.SettleTime);
        var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
        using var picture = new RenderTargetBitmap(size, new Vector(96, 96));
        picture.Render(this);
        picture.Save(file, new PngBitmapEncoderOptions());
        AppLog.Write($"saved a picture of the mini panel to {file}");
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
