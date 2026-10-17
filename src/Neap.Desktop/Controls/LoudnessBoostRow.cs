using System.Diagnostics;
using System.Globalization;
using Avalonia;
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
    private TextBlock? _level;
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
            if (_readout is not null) _readout.Text = Boost(args.NewValue);
            if (_painting) return;
            _chosenAt = Stopwatch.GetTimestamp();
            AppServices.Headset.SetLoudnessBoost((float)args.NewValue);
        };
        _readout = new TextBlock
        {
            MinWidth = 52,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            FontFeatures = FontFeatureCollection.Parse("tnum"),
        };
        _level = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontFeatures = FontFeatureCollection.Parse("tnum") };
        Content = new StackPanel
        {
            Spacing = Space.XS,
            Children =
            {
                new SliderLine { Preferred = SliderWidth, Flexible = _slider, Children = { _slider, _readout, _level } },
                Caution(),
            },
        };

        // Named on the slider rather than the card, since the slider takes focus.
        AutomationProperties.SetName(_slider, Header ?? Strings.Get("Audio_LoudnessBoost.Header"));
    }

    /// <summary>A quiet line under the slider with a warning mark: go up slowly, and back off if it distorts.</summary>
    /// <remarks>In the row rather than a box of its own, and in the description's colour, so it is read without shouting.</remarks>
    private static Grid Caution()
    {
        var iconSize = (double)Application.Current!.FindResource("NeapIconRow")!;
        var mark = new PathIcon { Width = iconSize, Height = iconSize, Margin = new Avalonia.Thickness(0, Space.Hairline, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        mark.Bind(PathIcon.DataProperty, mark.GetResourceObservable("IconWarning"));
        mark.Bind(ForegroundProperty, mark.GetResourceObservable("NeapTextSecondaryBrush"));
        var words = new TextBlock { Text = Strings.Get("LoudnessBoost_Caution"), TextWrapping = TextWrapping.Wrap, MaxWidth = 260, HorizontalAlignment = HorizontalAlignment.Left };
        words.Classes.Add("caption");
        words.Classes.Add("secondary");
        Grid.SetColumn(words, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = Space.S, Children = { mark, words } };
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
        if (_level is not null) _level.Text = Level(look.LevelDb);
        if (_slider.IsPointerOver || JustChosen()) return;
        _painting = true;
        try
        {
            _slider.Value = look.BoostDb;
            if (_readout is not null) _readout.Text = Boost(look.BoostDb);
        }
        finally { _painting = false; }
    }
}
