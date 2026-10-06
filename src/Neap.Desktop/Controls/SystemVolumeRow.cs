using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Neap.Core.Audio;

namespace Neap.Desktop.Controls;

/// <summary>
/// A settings row for a volume that belongs to the system's device, not the
/// headset.
/// </summary>
/// <remarks>
/// <para>
/// Master volume and microphone sensitivity look like headset settings and
/// are not. The headset reports both, but its copies are mirrors: writing one
/// changes the number it reports, then the system overwrites it and nothing
/// sounds different. Measured on both. So these rows read and write the
/// system's device.
/// </para>
/// <para>
/// The headset's mirror is still useful for display: it is pushed whenever
/// the level moves, from anywhere, so the slider follows it between polls.
/// Every row also polls the system once a second; mute has no mirror, so for
/// mute the poll is the only way to see it change.
/// </para>
/// </remarks>
public sealed class SystemVolumeRow : SettingsCard
{
    public static readonly StyledProperty<bool> CaptureProperty =
        AvaloniaProperty.Register<SystemVolumeRow, bool>(nameof(Capture));

    public static readonly StyledProperty<bool> WithMuteProperty =
        AvaloniaProperty.Register<SystemVolumeRow, bool>(nameof(WithMute));

    private const int SliderWidth = 220;

    /// <summary>The slider beside a mute: shorter by the mute's room, so the line is as wide as any other row's.</summary>
    private const int MutedSliderWidth = 164;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private Slider? _slider;
    private ToggleButton? _mute;
    private PathIcon? _muteIcon;
    private TextBlock? _readout;
    private IDisposable? _poll;
    private bool _painting;
    private bool _built;

    protected override Type StyleKeyOverride => typeof(SettingsCard);

    /// <summary>Gets or sets whether the row controls the microphone (true) or the headset's output (false).</summary>
    public bool Capture
    {
        get => GetValue(CaptureProperty);
        set => SetValue(CaptureProperty, value);
    }

    /// <summary>Gets or sets whether a volume row carries the system's mute beside its slider, so the two take one line.</summary>
    public bool WithMute
    {
        get => GetValue(WithMuteProperty);
        set => SetValue(WithMuteProperty, value);
    }

    private Flow Flow => Capture ? Flow.Input : Flow.Output;

    /// <summary>The registry name of the headset's mirror of this level.</summary>
    private string MirrorName => Capture ? "mic_volume" : "master_volume";

    /// <remarks>
    /// Listens and polls on every load, not only the first: a row is unloaded
    /// and loaded again whenever it moves, as a HeadsetSection's rows are.
    /// </remarks>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Build();
        AppServices.Headset.Changed += Paint;
        WindowPresence.Changed += OnPresence;
        if (WindowPresence.InFront) Poll();
        _ = Refresh();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (IsLoaded) return;
        AppServices.Headset.Changed -= Paint;
        WindowPresence.Changed -= OnPresence;
        StopPolling();
    }

    private void Poll() => _poll ??= Platform.Current.Ui.Every(PollInterval, () => _ = Refresh());

    private void StopPolling()
    {
        _poll?.Dispose();
        _poll = null;
    }

    /// <summary>Polls the system only while the window is in front; see <see cref="WindowPresence"/>.</summary>
    private void OnPresence()
    {
        if (!WindowPresence.InFront)
        {
            StopPolling();
            return;
        }
        Poll();
        _ = Refresh();
    }

    /// <summary>Builds the row's control, once.</summary>
    private void Build()
    {
        if (_built) return;
        _built = true;

        _slider = new Slider { Minimum = 0, Maximum = 100, VerticalAlignment = VerticalAlignment.Center };
        _slider.ValueChanged += (_, args) =>
        {
            if (_readout is not null) _readout.Text = Strings.Format("Level_Percent", (int)args.NewValue);
            if (!_painting) _ = SoundVolume.SetVolume((int)args.NewValue, Flow);
        };
        _readout = new TextBlock
        {
            MinWidth = 44,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            FontFeatures = FontFeatureCollection.Parse("tnum"),
        };
        var line = new SliderLine
        {
            Preferred = WithMute ? MutedSliderWidth : SliderWidth,
            Flexible = _slider,
            Children = { _slider, _readout },
        };
        if (WithMute)
        {
            // As tall as the buttons elsewhere on the page: it is the one
            // pressed most often, and an icon alone leaves it shorter.
            _muteIcon = new PathIcon { Width = 16, Height = 16, Data = Glyph("IconAudio") };
            _mute = new ToggleButton { MinHeight = 32, Content = _muteIcon };
            AutomationProperties.SetName(_mute, Strings.Get("Volume_Mute"));
            ToolTip.SetTip(_mute, Strings.Get("Volume_MuteTip"));
            _mute.Click += (_, _) =>
            {
                if (!_painting) _ = SoundVolume.SetMuted(_mute.IsChecked == true, Flow);
            };
            line.Children.Insert(0, _mute);
        }
        Content = line;

        // Named for screen readers on the control rather than the card around
        // it, since the control takes focus; otherwise every slider announces
        // as "slider".
        AutomationProperties.SetName(_slider, Header ?? Strings.Get("Volume_Name"));
    }

    private static Geometry Glyph(string key) => (Geometry)Application.Current!.FindResource(key)!;

    /// <summary>Reads the level and mute state from the system. A slider being dragged is left alone.</summary>
    private async Task Refresh()
    {
        var state = await SoundVolume.Read(Flow);
        _painting = true;
        try
        {
            if (_mute is not null)
            {
                _mute.IsChecked = state.Muted;
                _muteIcon!.Data = Glyph(state.Muted ? "IconMute" : "IconAudio");
            }
            if (_slider is not null && !_slider.IsPointerOver)
            {
                _slider.Value = state.Percent;
                if (_readout is not null) _readout.Text = Strings.Format("Level_Percent", state.Percent);
            }
        }
        finally { _painting = false; }
    }

    /// <summary>
    /// Follows the headset's mirror between polls.
    /// </summary>
    /// <remarks>
    /// The mirror arrives the moment the level moves anywhere, including from
    /// the headset's own volume wheel, so the slider is live rather than up to
    /// a second behind.
    /// </remarks>
    private void Paint()
    {
        if (_slider is null || !AppServices.Headset.TryGetNumber(MirrorName, out int level)) return;
        _painting = true;
        try
        {
            _slider.Value = level;
            if (_readout is not null) _readout.Text = Strings.Format("Level_Percent", level);
        }
        finally { _painting = false; }
    }
}
