using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Neap.App.Services;
using Neap.Core.Audio;
using Neap.Core.Presets;
using Windows.Globalization.NumberFormatting;

namespace Neap.App.Controls;

/// <summary>
/// Formats and parses a frequency for the parametric equaliser: hertz below
/// 1 kHz, kilohertz above.
/// </summary>
/// <remarks>Parsing is <see cref="FrequencyEntry.Parse"/>.</remarks>
internal sealed class FrequencyFormatter : INumberFormatter2, INumberParser
{
    public static string Text(double hertz) => hertz >= 1000
        ? Strings.Format("Parametric_KiloHertz", (hertz / 1000).ToString("0.##", CultureInfo.InvariantCulture))
        : Strings.Format("Parametric_Hertz", Math.Round(hertz).ToString(CultureInfo.InvariantCulture));

    public string FormatInt(long value) => Text(value);

    public string FormatUInt(ulong value) => Text(value);

    public string FormatDouble(double value) => Text(value);

    public double? ParseDouble(string text) => FrequencyEntry.Parse(text);

    public long? ParseInt(string text) => ParseDouble(text) is double d ? (long)Math.Round(d) : null;

    public ulong? ParseUInt(string text) => ParseDouble(text) is double d and >= 0 ? (ulong)Math.Round(d) : null;
}

/// <summary>
/// The parametric equaliser for the game bank: a plot of the curve asked for
/// against the curve heard, and the fields for each adjustment.
/// </summary>
/// <remarks>
/// <para>
/// Every change goes to <see cref="PresetService.SetAdjustments"/>, which
/// fits the adjustments to the ten bands and writes whichever moved. What
/// is shown is then read back from there, so the plot, the fields and the
/// ten gains under them always agree.
/// </para>
/// <para>
/// The test tone above the plot plays on the headset, so it is heard through
/// the equaliser being set. It is moved by hand with its slider or swept, and
/// held where it stands out, to be cut or boosted there. It plays only while
/// the panel is on screen: leaving the page, hiding the window or going back
/// to the bands stops it.
/// </para>
/// <para>
/// The fields edit whichever adjustment is chosen; a point pressed on the
/// plot chooses its adjustment too. Frequency has a slider, on a
/// logarithmic scale as heard, and a field for typing an exact value.
/// </para>
/// </remarks>
public sealed partial class ParametricPanel : UserControl
{
    private const Bank Game = Bank.Game;

    private readonly FrequencyFormatter _frequencies = new();
    private readonly List<TextBlock> _sent = new();
    private int _selected;
    private bool _painting;

    private enum Tone { Off, Sweeping, Held }

    /// <summary>The tone's level, kept for the session so it comes back where it was left.</summary>
    private static int _level = ToneSweep.FirstLevel;

    private readonly DispatcherQueueTimer _sweep;
    private readonly Stopwatch _swept = new();
    private IPlayingTone? _player;
    private Task? _opening;
    private int _stops;
    private Tone _tone;
    private double _frequency = Adjustment.LowestFrequency;
    private double _sweptFrom;

    /// <summary>Raised after the adjustments change, so the equaliser can repaint around them.</summary>
    public event Action? Changed;

    public ParametricPanel()
    {
        InitializeComponent();
        FrequencyField.NumberFormatter = _frequencies;
        FrequencyField.Minimum = Adjustment.LowestFrequency;
        FrequencyField.Maximum = Adjustment.HighestFrequency;

        Shape.Frequencies = PresetStore.Game.Frequencies;
        Shape.AdjustmentChosen += index => { _selected = index; Paint(); };
        Shape.AdjustmentChanged += Replace;

        AddButton.Click += (_, _) =>
        {
            var adjustments = Current();
            adjustments.Add(ParametricEq.Next(adjustments));
            _selected = adjustments.Count - 1;
            Commit(adjustments);
        };
        RemoveButton.Click += (_, _) =>
        {
            var adjustments = Current();
            if (_selected >= adjustments.Count) return;
            adjustments.RemoveAt(_selected);
            _selected = Math.Max(0, _selected - 1);
            Commit(adjustments);
        };

        FrequencySlider.ValueChanged += (_, args) =>
            Edit(a => a with { Frequency = ParametricEq.FrequencyAt(args.NewValue, FrequencySlider.Maximum) });
        FrequencyField.ValueChanged += (_, args) =>
        {
            if (double.IsNaN(args.NewValue)) { Paint(); return; }
            Edit(a => a with { Frequency = (int)Math.Round(args.NewValue) });
        };
        GainSlider.ValueChanged += (_, args) =>
            Edit(a => a with { Gain = (int)Math.Round(args.NewValue * 10) });
        WidthSlider.ValueChanged += (_, args) =>
            Edit(a => a with { Width = (int)Math.Round(args.NewValue * 10) });

        _sweep = DispatcherQueue.CreateTimer();
        _sweep.Interval = TimeSpan.FromMilliseconds(33);
        _sweep.Tick += (_, _) => SweepOn();
        SweepButton.Click += async (_, _) => await SweepOrHold();
        StopButton.Click += (_, _) => StopTone();
        ToneSlider.ValueChanged += async (_, args) =>
        {
            if (_painting) return;
            await Hold(ParametricEq.FrequencyAt(args.NewValue, ToneSlider.Maximum));
        };
        LevelSlider.Value = _level;
        LevelSlider.ValueChanged += (_, args) =>
        {
            _level = (int)Math.Round(args.NewValue);
            if (_player is not null) _player.Amplitude = ToneSweep.Amplitude(_level);
        };
        CutButton.Click += (_, _) => AddAtTone(boost: false);
        BoostButton.Click += (_, _) => AddAtTone(boost: true);

        // A tone left playing where it cannot be seen is a tone nobody can stop.
        Unloaded += (_, _) => StopTone();
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) =>
        {
            if (Visibility != Visibility.Visible) StopTone();
        });

        BuildSent();
    }

    // -- the test tone -----------------------------------------------------

    private async Task SweepOrHold()
    {
        switch (_tone)
        {
            case Tone.Sweeping:
                _tone = Tone.Held;
                _sweep.Stop();
                break;
            default:
                _sweptFrom = ToneSweep.Start(_frequency);
                if (!await Play(_sweptFrom)) return;
                _tone = Tone.Sweeping;
                _swept.Restart();
                _sweep.Start();
                break;
        }
        PaintTone();
    }

    /// <summary>Holds the tone at a frequency, starting it if it is not playing.</summary>
    private async Task Hold(double frequency)
    {
        _sweep.Stop();
        if (!await Play(frequency)) return;
        _tone = Tone.Held;
        PaintTone();
    }

    private void SweepOn()
    {
        if (_tone != Tone.Sweeping) { _sweep.Stop(); return; }
        _frequency = ToneSweep.After(_sweptFrom, _swept.Elapsed.TotalSeconds);
        if (_player is not null) _player.Frequency = _frequency;
        if (ToneSweep.Finished(_frequency))
        {
            _tone = Tone.Held;
            _sweep.Stop();
        }
        PaintTone();
    }

    /// <summary>Moves the tone to a frequency, opening it on the headset if needed.</summary>
    /// <returns>Whether the tone is playing.</returns>
    private async Task<bool> Play(double frequency)
    {
        _frequency = frequency;
        // One opening at a time: a slider dragged while the tone opens asks
        // again at every step, and each would otherwise open a tone of its
        // own that nothing could stop.
        if (_player is null) await (_opening ??= Open());
        if (_player is null) return false;
        _player.Frequency = _frequency;
        return true;
    }

    private async Task Open()
    {
        int stops = _stops;
        double frequency = _frequency;
        ToneTrouble.Visibility = Visibility.Collapsed;
        try
        {
            // A pretend run leaves the real headset's sound alone.
            var player = await Task.Run(() => Pretend.Windows?.OpenTone(frequency) ?? TestTone.Open(frequency));
            // Stopped, or left, while it was opening.
            if (stops != _stops || !IsLoaded || Visibility != Visibility.Visible)
            {
                player.Dispose();
                return;
            }
            player.Stopped += fault => DispatcherQueue.TryEnqueue(() => Fail(fault.Message));
            player.Frequency = _frequency;
            player.Amplitude = ToneSweep.Amplitude(_level);
            _player = player;
        }
        catch (WindowsAudioException e) { Fail(e.Message); }
        finally { _opening = null; }
    }

    private void Fail(string trouble)
    {
        AppLog.Write($"test tone: {trouble}");
        StopTone();
        ToneTrouble.Text = Strings.Format("Tone_CouldNotPlay", trouble);
        ToneTrouble.Visibility = Visibility.Visible;
    }

    private void StopTone()
    {
        _stops++;
        _sweep.Stop();
        _player?.Dispose();
        _player = null;
        _tone = Tone.Off;
        PaintTone();
    }

    private void AddAtTone(bool boost)
    {
        var adjustments = Current();
        if (adjustments.Count >= ParametricEq.MostAdjustments) return;
        adjustments.Add(ToneSweep.At(_frequency, boost));
        _selected = adjustments.Count - 1;
        Commit(adjustments);
    }

    /// <summary>Shows the tone's state: its buttons, its slider and its line on the plot.</summary>
    private void PaintTone()
    {
        bool painting = _painting;
        _painting = true;
        try
        {
            (SweepIcon.Glyph, SweepText.Text) = _tone switch
            {
                Tone.Sweeping => ("\uE769", Strings.Get("Tone_Hold")),
                Tone.Held => ("\uE768", Strings.Get("Tone_Resume")),
                _ => ("\uE768", Strings.Get("Tone_Sweep")),
            };
            AutomationProperties.SetName(SweepButton, SweepText.Text);
            StopButton.IsEnabled = _tone != Tone.Off;
            ToneSlider.Value = Math.Round(ParametricEq.X(_frequency, ToneSlider.Maximum));
            ToneText.Text = FrequencyFormatter.Text(_frequency);

            // Only where the tone stands still: a cut at a moving frequency
            // lands somewhere the ear has already left.
            bool room = AppServices.Presets.Adjustments(Game).Count < ParametricEq.MostAdjustments;
            CutButton.IsEnabled = BoostButton.IsEnabled = _tone == Tone.Held && room;
            Shape.ShowTone(_tone == Tone.Off ? null : _frequency);
        }
        finally { _painting = painting; }
    }

    private void BuildSent()
    {
        var spec = PresetStore.Game;
        for (int i = 0; i < spec.Frequencies.Count; i++)
        {
            Sent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var value = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            };
            var hz = new TextBlock
            {
                Text = spec.Frequencies[i],
                HorizontalAlignment = HorizontalAlignment.Center,
                Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
            };
            var column = new StackPanel { Spacing = 2, Children = { value, hz } };
            Grid.SetColumn(column, i);
            Sent.Children.Add(column);
            _sent.Add(value);
        }
    }

    private static List<Adjustment> Current() => AppServices.Presets.Adjustments(Game).ToList();

    private void Replace(int index, Adjustment adjustment)
    {
        var adjustments = Current();
        if (index >= adjustments.Count) return;
        adjustments[index] = adjustment;
        _selected = index;
        Commit(adjustments);
    }

    private void Edit(Func<Adjustment, Adjustment> change)
    {
        if (_painting) return;
        var adjustments = Current();
        if (_selected >= adjustments.Count) return;
        var changed = change(adjustments[_selected]).Held();
        if (changed == adjustments[_selected]) return;
        Replace(_selected, changed);
    }

    private void Commit(List<Adjustment> adjustments)
    {
        AppServices.Presets.SetAdjustments(Game, adjustments);
        Changed?.Invoke();
    }

    /// <summary>Shows the adjustments and the gains they fit to, as the preset service has them.</summary>
    public void Paint()
    {
        var bands = AppServices.Presets.LiveBands(Game);
        if (bands is null) return;
        var adjustments = AppServices.Presets.Adjustments(Game);
        _selected = Math.Clamp(_selected, 0, Math.Max(0, adjustments.Count - 1));

        _painting = true;
        try
        {
            Shape.Show(adjustments, bands, _selected);
            Miss.Visibility = ParametricEq.FallsShort(adjustments, bands) ? Visibility.Visible : Visibility.Collapsed;

            PaintChips(adjustments);
            bool any = adjustments.Count > 0;
            Empty.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
            Fields.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            AddButton.IsEnabled = adjustments.Count < ParametricEq.MostAdjustments;
            RemoveButton.Visibility = any ? Visibility.Visible : Visibility.Collapsed;

            if (any)
            {
                var chosen = adjustments[_selected];
                AutomationProperties.SetName(RemoveButton,
                    Strings.Format("Parametric_RemoveNamed", _selected + 1));
                FrequencySlider.Value = Math.Round(ParametricEq.X(chosen.Frequency, FrequencySlider.Maximum));
                FrequencyField.Value = chosen.Frequency;
                GainSlider.Value = chosen.Gain / 10.0;
                GainText.Text = Strings.Format("Parametric_Decibels", Db.Text(chosen.Gain));
                WidthSlider.Value = chosen.Width / 10.0;
                WidthText.Text = Strings.Format("Parametric_Octaves",
                    (chosen.Width / 10.0).ToString("0.0", CultureInfo.InvariantCulture));
            }

            for (int i = 0; i < _sent.Count && i < bands.Length; i++)
                _sent[i].Text = Db.Text(bands[i]);
            PaintTone();
        }
        finally { _painting = false; }
    }

    /// <summary>One toggle per adjustment, numbered as on the plot, with its frequency.</summary>
    private void PaintChips(IReadOnlyList<Adjustment> adjustments)
    {
        // Collapsed when empty, or its spacing sets Add apart from nothing.
        Chips.Visibility = adjustments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        while (Chips.Children.Count > adjustments.Count) Chips.Children.RemoveAt(Chips.Children.Count - 1);
        while (Chips.Children.Count < adjustments.Count)
        {
            int index = Chips.Children.Count;
            var chip = new ToggleButton();
            chip.Click += (_, _) => { _selected = index; Paint(); };
            Chips.Children.Add(chip);
        }
        for (int i = 0; i < adjustments.Count; i++)
        {
            var chip = (ToggleButton)Chips.Children[i];
            string frequency = FrequencyFormatter.Text(adjustments[i].Frequency);
            chip.Content = Strings.Format("Parametric_Chip", i + 1, frequency);
            chip.IsChecked = i == _selected;
            AutomationProperties.SetName(chip, Strings.Format("Parametric_ChipName", i + 1, frequency));
        }
    }
}
