using System.Diagnostics;
using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Neap.Core.Audio;

namespace Neap.Desktop.Controls;

/// <summary>
/// A settings row for the headset's pre-gain: the level it applies to what it
/// plays before its equaliser, raised so everything plays louder than the
/// system volume alone allows, or lowered to leave the equaliser room.
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
/// whether it is shown at all, is <see cref="LoudnessBoostLook"/>. A headset
/// without the feature gets the row with the model that has it in place of
/// the slider.
/// </para>
/// </remarks>
public sealed class LoudnessBoostRow : SettingsCard
{
    private const int SliderWidth = 220;

    private Slider? _slider;
    private TextBlock? _readout;
    private SliderLine? _line;
    private bool _painting;
    private bool _built;

    /// <summary>When the person last moved the slider, as a timestamp.</summary>
    private long _chosenAt;

    /// <summary>How long after a change the headset's report of the level leaves the slider alone.</summary>
    /// <remarks>
    /// The headset confirms each level most of a second after it is set, and a
    /// confirmation of one the slider has already passed would pull it back
    /// under the person's finger.
    /// </remarks>
    private static readonly TimeSpan ChoiceHold = TimeSpan.FromMilliseconds(1500);

    private bool JustChosen() => _chosenAt != 0 && Stopwatch.GetElapsedTime(_chosenAt) < ChoiceHold;

    /// <summary>The boosts the slider sent while the person moved it, the only reports the hold leaves alone.</summary>
    /// <remarks>A level set by something else during the hold, such as the equaliser's Lower pre-gain, is shown at once.</remarks>
    private readonly HashSet<int> _sent = [];

    /// <summary>Shows the level the headset holds once the hold is over, as the last confirmation may have come during it.</summary>
    private readonly DispatcherTimer _settle = new() { Interval = ChoiceHold };

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
        _settle.Stop();
    }

    private void Build()
    {
        if (_built) return;
        _built = true;

        _settle.Tick += (_, _) =>
        {
            _settle.Stop();
            _sent.Clear();
            Refresh();
        };
        _slider = NeapControls.Slider();
        _slider.Minimum = LoudnessBoostLook.MinBoostDb;
        _slider.Maximum = LoudnessBoostLook.MaxBoostDb;
        _slider.TickFrequency = LoudnessBoostLook.StepDb;
        _slider.IsSnapToTickEnabled = true;
        _slider.ValueChanged += (_, args) =>
        {
            if (_readout is not null) _readout.Text = Boost(args.NewValue);
            if (_painting) return;
            if (!JustChosen()) _sent.Clear();
            _sent.Add((int)Math.Round(args.NewValue));
            _chosenAt = Stopwatch.GetTimestamp();
            _settle.Stop();
            _settle.Start();
            AppServices.Headset.SetLoudnessBoost((float)args.NewValue);
        };
        _readout = new TextBlock
        {
            Classes = { "secondary" },
            MinWidth = 52,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            FontFeatures = FontFeatureCollection.Parse("tnum"),
        };
        _line = new SliderLine { Preferred = SliderWidth, Flexible = _slider, Children = { _slider, _readout } };
        Content = _line;
        Description = Strings.Get("LoudnessBoost_About");

        // Named on the slider rather than the card, since the slider takes focus.
        AutomationProperties.SetName(_slider, Header ?? Strings.Get("Audio_LoudnessBoost.Header"));
    }

    /// <summary>The boost the slider is at, such as "+0 dB" or "+12 dB".</summary>
    private static string Boost(double boost) =>
        Strings.Format("LoudnessBoost_Decibels",
            ((int)Math.Round(boost)).ToString("+0;-0;+0", CultureInfo.CurrentCulture));

    /// <summary>The level the headset reports, such as "Headset level: -12 dB".</summary>
    private static string Level(float db) =>
        Strings.Format("LoudnessBoost_Level",
            ((int)MathF.Round(db)).ToString("+0;-0;0", CultureInfo.CurrentCulture));

    private void Refresh()
    {
        var look = LoudnessBoostLook.For(AppServices.Headset.Loudness, AppServices.Headset.ReadOnly);
        IsVisible = look.Shown;
        if (!look.Shown || _slider is null) return;
        _slider.IsEnabled = look.Enabled;

        // The headset's own level, which the slider cannot always show: one set elsewhere can be past its ends.
        string level = Level(look.LevelDb);
        ToolTip.SetTip(_slider, level);
        // The row's description goes with it: the row gives it to the line the slider sits in, which a screen reader does not reach.
        AutomationProperties.SetHelpText(_slider, $"{Description} {level}");
        if ((_slider.IsPointerOver || JustChosen()) && _sent.Contains(look.BoostDb)) return;
        _painting = true;
        try
        {
            _slider.Value = look.BoostDb;
            if (_readout is not null) _readout.Text = Boost(look.BoostDb);
        }
        finally { _painting = false; }
    }
}
