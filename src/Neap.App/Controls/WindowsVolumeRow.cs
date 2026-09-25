using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Neap.App.Services;
using Neap.Core.Audio;

namespace Neap.App.Controls;

public enum WindowsControl { Volume, Mute }

/// <summary>
/// A settings row for a volume or mute that belongs to the Windows endpoint,
/// not the headset.
/// </summary>
/// <remarks>
/// <para>
/// Master volume and microphone sensitivity look like headset settings and
/// are not. The headset reports both, but its copies are mirrors: writing one
/// changes the number it reports, then Windows overwrites it and nothing
/// sounds different. Measured on both. So these rows read and write the
/// Windows endpoint.
/// </para>
/// <para>
/// The headset's mirror is still useful for display: it is pushed whenever
/// the level moves, from anywhere, so the slider follows it between polls.
/// Every row also polls Windows once a second; mute has no mirror, so for
/// mute the poll is the only way to see it change.
/// </para>
/// </remarks>
public sealed class WindowsVolumeRow : SettingsCard
{
    private const int SliderWidth = 220;
    private const string UnmutedGlyph = "\uE994", MutedGlyph = "\uE74F";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private Slider? _slider;
    private ToggleSwitch? _toggle;
    private ToggleButton? _mute;
    private TextBlock? _readout;
    private DispatcherQueueTimer? _poll;
    private bool _painting;
    private bool _listening;

    /// <remarks>
    /// Listens and polls on every load, not only the first: a row is unloaded
    /// and loaded again whenever it moves, as a HeadsetSection's rows are. An
    /// Unloaded raised while still loaded, which a move can do after the new
    /// Loaded, is ignored.
    /// </remarks>
    public WindowsVolumeRow()
    {
        Loaded += (_, _) =>
        {
            Build();
            Listen(true);
        };
        Unloaded += (_, _) =>
        {
            if (!IsLoaded) Listen(false);
        };
    }

    private void Listen(bool on)
    {
        if (on == _listening) return;
        _listening = on;
        if (on)
        {
            if (_slider is not null) AppServices.Headset.Changed += Paint;
            _poll!.Start();
            _ = Refresh();
        }
        else
        {
            AppServices.Headset.Changed -= Paint;
            _poll?.Stop();
        }
    }

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(WindowsControl), typeof(WindowsVolumeRow),
        new PropertyMetadata(WindowsControl.Volume));

    public WindowsControl Kind
    {
        get => (WindowsControl)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public static readonly DependencyProperty CaptureProperty = DependencyProperty.Register(
        nameof(Capture), typeof(bool), typeof(WindowsVolumeRow), new PropertyMetadata(false));

    /// <summary>Gets or sets whether the row controls the microphone (true) or the headset's output (false).</summary>
    public bool Capture
    {
        get => (bool)GetValue(CaptureProperty);
        set => SetValue(CaptureProperty, value);
    }

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(WindowsVolumeRow), new PropertyMetadata(""));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public static readonly DependencyProperty WithMuteProperty = DependencyProperty.Register(
        nameof(WithMute), typeof(bool), typeof(WindowsVolumeRow), new PropertyMetadata(false));

    /// <summary>Gets or sets whether a volume row carries Windows' mute beside its slider, so the two take one line.</summary>
    public bool WithMute
    {
        get => (bool)GetValue(WithMuteProperty);
        set => SetValue(WithMuteProperty, value);
    }

    private Flow Flow => Capture ? Flow.Input : Flow.Output;

    /// <summary>The registry name of the headset's mirror of this level, or null when it has none.</summary>
    private string? MirrorName => Kind == WindowsControl.Volume
        ? (Capture ? "mic_volume" : "master_volume") : null;

    /// <summary>
    /// Names the control for screen readers, on the control rather than the
    /// card around it, since the control takes focus; otherwise every slider
    /// announces as "slider".
    /// </summary>
    private void Speak()
    {
        string spoken = Header?.ToString() ?? Strings.Get("Volume_Name");
        foreach (UIElement? control in new UIElement?[] { _slider, _toggle })
            if (control is not null)
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control, spoken);
    }

    /// <summary>Builds the row's control and its poll, once.</summary>
    private void Build()
    {
        if (_poll is not null) return;
        if (Glyph.Length > 0) HeaderIcon = new FontIcon { Glyph = Glyph };

        if (Kind == WindowsControl.Mute)
        {
            _toggle = new ToggleSwitch { OnContent = Strings.Get("Switch_On"), OffContent = Strings.Get("Switch_Off") };
            _toggle.Toggled += (_, _) =>
            {
                if (!_painting) _ = WindowsAudio.SetMuted(_toggle.IsOn, Flow);
            };
            Content = _toggle;
        }
        else
        {
            _slider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Width = SliderWidth,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _slider.ValueChanged += (_, args) =>
            {
                if (_readout is not null) _readout.Text = Strings.Format("Level_Percent", (int)args.NewValue);
                if (!_painting) _ = WindowsAudio.SetVolume((int)args.NewValue, Flow);
            };
            _readout = new TextBlock
            {
                MinWidth = 44,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["NumeralBodyTextStyle"],
            };
            var line = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children = { _slider, _readout },
            };
            if (WithMute)
            {
                _mute = new ToggleButton { Content = new FontIcon { Glyph = UnmutedGlyph, FontSize = 16 } };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_mute, Strings.Get("Volume_Mute"));
                ToolTipService.SetToolTip(_mute, Strings.Get("Volume_MuteTip"));
                _mute.Click += (_, _) =>
                {
                    if (!_painting) _ = WindowsAudio.SetMuted(_mute.IsChecked == true, Flow);
                };
                line.Children.Insert(0, _mute);
            }
            Content = line;
        }

        Speak();

        _poll = DispatcherQueue.CreateTimer();
        _poll.Interval = PollInterval;
        _poll.Tick += (_, _) => _ = Refresh();
    }

    /// <summary>
    /// Reads the level or mute state from Windows. A slider being dragged is
    /// left alone.
    /// </summary>
    private async Task Refresh()
    {
        var state = await WindowsAudio.Read(Flow);
        _painting = true;
        try
        {
            if (_toggle is not null) _toggle.IsOn = state.Muted;
            if (_mute is not null)
            {
                _mute.IsChecked = state.Muted;
                if (_mute.Content is FontIcon icon) icon.Glyph = state.Muted ? MutedGlyph : UnmutedGlyph;
            }
            if (_slider is not null && !_slider.FocusState.Equals(FocusState.Pointer))
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
        if (_slider is null || MirrorName is null) return;
        if (!AppServices.Headset.TryGetNumber(MirrorName, out int level)) return;
        _painting = true;
        try
        {
            _slider.Value = level;
            if (_readout is not null) _readout.Text = Strings.Format("Level_Percent", level);
        }
        finally { _painting = false; }
    }
}
