using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Audio;

namespace StealthPro.App.Controls;

public enum WindowsControl { Volume, Mute }

/// <summary>
/// A volume or mute that belongs to Windows, not the headset.
///
/// Master volume and microphone sensitivity look like headset settings and
/// are not. The headset reports both, but its copies are <b>mirrors</b>:
/// write one and the number it reports changes, then Windows overwrites it
/// and nothing sounds different. Measured on both. So these rows read and
/// write the Windows endpoint.
///
/// The headset's mirror still earns its keep as the display: it is pushed to
/// us whenever the level moves, including when something else moves it, so
/// the row stays live without polling. Mute has no mirror and is polled.
/// </summary>
public sealed class WindowsVolumeRow : SettingsCard
{
    private const int SliderWidth = 220;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private Slider? _slider;
    private ToggleSwitch? _toggle;
    private TextBlock? _readout;
    private DispatcherQueueTimer? _poll;
    private bool _painting;

    public WindowsVolumeRow()
    {
        Loaded += OnLoaded;
        Unloaded += (_, _) =>
        {
            AppServices.Headset.Changed -= Paint;
            _poll?.Stop();
        };
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

    /// <summary>True for the microphone, false for the headset's output.</summary>
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

    private Flow Flow => Capture ? Flow.Input : Flow.Output;

    /// <summary>The headset's mirror of this level, when it has one.</summary>
    private string? MirrorName => Kind == WindowsControl.Volume
        ? (Capture ? "mic_volume" : "master_volume") : null;

    /// <summary>
    /// Named on the control, not on the card around it — the control is what
    /// takes focus, and a page where every slider announces as "slider" is
    /// the fault the equaliser bands had.
    /// </summary>
    private void Speak()
    {
        string spoken = Header?.ToString() ?? Strings.Get("Volume_Name");
        foreach (UIElement? control in new UIElement?[] { _slider, _toggle })
            if (control is not null)
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control, spoken);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (Glyph.Length > 0) HeaderIcon = new FontIcon { Glyph = Glyph };

        if (Kind == WindowsControl.Mute)
        {
            _toggle = new ToggleSwitch { OnContent = null, OffContent = null };
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
                if (_readout is not null) _readout.Text = $"{(int)args.NewValue}%";
                if (!_painting) _ = WindowsAudio.SetVolume((int)args.NewValue, Flow);
            };
            _readout = new TextBlock
            {
                MinWidth = 44,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
            };
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children = { _slider, _readout },
            };
            AppServices.Headset.Changed += Paint;
        }

        Speak();

        _poll = DispatcherQueue.CreateTimer();
        _poll.Interval = PollInterval;
        _poll.Tick += (_, _) => _ = Refresh();
        _poll.Start();
        _ = Refresh();
    }

    /// <summary>Ask Windows directly. The only way to see mute move.</summary>
    private async Task Refresh()
    {
        var state = await WindowsAudio.Read(Flow);
        _painting = true;
        try
        {
            if (_toggle is not null) _toggle.IsOn = state.Muted;
            else if (_slider is not null && !_slider.FocusState.Equals(FocusState.Pointer))
            {
                _slider.Value = state.Percent;
                if (_readout is not null) _readout.Text = $"{state.Percent}%";
            }
        }
        finally { _painting = false; }
    }

    /// <summary>
    /// Follow the headset's mirror between polls. It arrives the moment the
    /// level moves anywhere, including from the headset's own volume wheel,
    /// which is what makes this feel live rather than a second behind.
    /// </summary>
    private void Paint()
    {
        if (_slider is null || MirrorName is null) return;
        if (!AppServices.Headset.TryGetNumber(MirrorName, out int level)) return;
        _painting = true;
        try
        {
            _slider.Value = level;
            if (_readout is not null) _readout.Text = $"{level}%";
        }
        finally { _painting = false; }
    }
}
