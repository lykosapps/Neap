using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Neap.Core.Audio;

namespace Neap.Desktop.Controls;

/// <summary>
/// A settings row that raises the headset's own playback level, so everything
/// plays louder than the system volume alone allows.
/// </summary>
/// <remarks>
/// <para>
/// Unlike master volume, this is a headset setting: a level the headset keeps
/// and applies to what it plays, set by a RACE command (see
/// <see cref="LoudnessBoost"/>). It is shown only on a headset that answers for
/// it, which Neap learns by reading the level on connect.
/// </para>
/// <para>
/// The slider is the boost above the headset's stock level, in decibels; zero
/// is the level the headset ships with. What it shows for a given reading, and
/// whether it is shown at all, is <see cref="LoudnessBoostLook"/>.
/// </para>
/// </remarks>
public sealed class LoudnessBoostRow : SettingsCard
{
    private const int SliderWidth = 220;

    private Slider? _slider;
    private TextBlock? _readout;
    private bool _painting;
    private bool _built;

    protected override Type StyleKeyOverride => typeof(SettingsCard);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Build();
        AppServices.Headset.LoudnessChanged += Refresh;
        Refresh();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (IsLoaded) return;
        AppServices.Headset.LoudnessChanged -= Refresh;
    }

    private void Build()
    {
        if (_built) return;
        _built = true;

        _slider = new Slider
        {
            Minimum = 0,
            Maximum = LoudnessBoostLook.MaxBoostDb,
            TickFrequency = LoudnessBoostLook.StepDb,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _slider.ValueChanged += (_, args) =>
        {
            if (_readout is not null) _readout.Text = Readout((int)args.NewValue);
            if (_painting) return;
            AppServices.Headset.SetLoudnessBoost((float)args.NewValue);
        };
        _readout = new TextBlock
        {
            MinWidth = 44,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            FontFeatures = FontFeatureCollection.Parse("tnum"),
        };
        Content = new SliderLine
        {
            Preferred = SliderWidth,
            Flexible = _slider,
            Children = { _slider, _readout },
        };

        // Named on the slider rather than the card, since the slider takes focus.
        AutomationProperties.SetName(_slider, Header ?? Strings.Get("Audio_LoudnessBoost.Header"));
    }

    private static string Readout(int boost) =>
        boost <= 0 ? Strings.Get("LoudnessBoost_Normal") : Strings.Format("LoudnessBoost_Decibels", boost);

    private void Refresh()
    {
        var look = LoudnessBoostLook.For(AppServices.Headset.Loudness, AppServices.Headset.ReadOnly);
        IsVisible = look.Shown;
        if (!look.Shown || _slider is null) return;
        _slider.IsEnabled = look.Enabled;
        _painting = true;
        try
        {
            _slider.Value = look.BoostDb;
            if (_readout is not null) _readout.Text = Readout(look.BoostDb);
        }
        finally { _painting = false; }
    }
}
