using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Neap.App.Services;
using Neap.Core.Presets;
using Windows.Globalization.NumberFormatting;

namespace Neap.App.Controls;

/// <summary>
/// Formats and parses a frequency for the parametric equaliser: hertz below
/// 1 kHz, kilohertz above.
/// </summary>
/// <remarks>Parsing is lenient: "4915", "4.9k" and "4.9 kHz" are all accepted.</remarks>
internal sealed class FrequencyFormatter : INumberFormatter2, INumberParser
{
    public static string Text(double hertz) => hertz >= 1000
        ? Strings.Format("Parametric_KiloHertz", (hertz / 1000).ToString("0.##", CultureInfo.InvariantCulture))
        : Strings.Format("Parametric_Hertz", Math.Round(hertz).ToString(CultureInfo.InvariantCulture));

    public string FormatInt(long value) => Text(value);

    public string FormatUInt(ulong value) => Text(value);

    public string FormatDouble(double value) => Text(value);

    public double? ParseDouble(string text)
    {
        string lower = text.ToLowerInvariant().Replace(',', '.');
        string number = new(lower.Where(c => char.IsDigit(c) || c == '.').ToArray());
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return null;
        return lower.Contains('k', StringComparison.Ordinal) ? value * 1000 : value;
    }

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

        BuildSent();
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
