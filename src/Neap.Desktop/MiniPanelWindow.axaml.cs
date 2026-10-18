using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Neap.Core;
using Neap.Core.Connection;
using Neap.Core.Diagnostics;
using Neap.Core.Mix;
using Neap.Desktop.Controls;

namespace Neap.Desktop;

/// <summary>
/// The mini panel: the five things changed mid-game, in a small window by the
/// notification area that goes away when it loses focus.
/// </summary>
/// <remarks>
/// <para>
/// The mix, the microphone, noise cancellation, spatial sound and the equaliser preset are
/// the controls Home has, with the same logic behind them; this only lays
/// them out small. <see cref="MiniPanel"/> decides where it sits.
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

    private readonly MainWindow? _main;
    private readonly BatteryMeter _battery = new("caption");
    private IDisposable? _recheck;
    private bool _painting;

    public MiniPanelWindow()
    {
        InitializeComponent();
        Title = Strings.Get("Main_TrayMini");
        Classes.Set("touch", Pretend.Touch);
        BatteryHolder.Content = _battery;
        if (Pretend.Behind) ShowActivated = false;

        Dial.ValueChanged += (_, args) =>
        {
            if (!_painting) AppServices.Mix.Apply((int)Math.Round(args.NewValue));
        };
        OpenNeap.Click += (_, _) => OpenWindow(null);
        ChooseApps.Click += (_, _) => OpenWindow("home");
        Preset.Flyout!.Closed += (_, _) => CloseIfAway();
        Activated += (_, _) => WindowPresence.Set(true);
        Deactivated += (_, _) =>
        {
            WindowPresence.Set(false);
            Dispatcher.UIThread.Post(CloseIfAway);
        };
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || e.Handled) return;
            e.Handled = true;
            Close();
        };
        SizeChanged += (_, _) => Place();
    }

    /// <summary>
    /// Gets the window it opens when asked, which also decides whether it is
    /// drawn at touch size; null for a pretend run showing it alone.
    /// </summary>
    public MainWindow? Main
    {
        get => _main;
        init
        {
            _main = value;
            if (value is not null) Classes.Set("touch", value.IsTouch);
        }
    }

    /// <summary>Gets when it closed, as a <see cref="Stopwatch"/> timestamp, so the icon's next click can tell whether that click was the one that did it.</summary>
    public long ClosedAt { get; private set; }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        AppServices.Headset.Changed += Paint;
        AppServices.Headset.StatusChanged += OnStatus;
        AppServices.Mix.Changed += Paint;
        Paint();
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
        if (Pretend.Mini || IsActive || Preset.Flyout!.IsOpen) return;
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

        NoiseRow.IsVisible = HeadsetModels.Shows(headset.Model, Feature.NoiseControl);

        bool chosen = AppServices.Mix.ChatApps.Count > 0;
        SetupArea.IsVisible = !chosen;
        DialArea.IsEnabled = chosen;

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
            AutomationProperties.SetHelpText(Dial, Strings.Format("Mix_Levels", LeanText.Text, game, chat));
        }
        finally { _painting = false; }
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
