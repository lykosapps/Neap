using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Neap.Core.Presets;

namespace Neap.Desktop.Controls;

/// <summary>
/// The true parametric equaliser: the curve of ten bands, the chosen band's
/// dials and typed values, and a table of all ten.
/// </summary>
/// <remarks>
/// <para>
/// Every control sets the same ten bands, so the curve, the dials and the table
/// always agree, and a screen reader has the table for what the curve shows.
/// The rules are Core's (<see cref="BandEditing"/>, <see cref="BandEntry"/>,
/// <see cref="StartingShapes"/>, <see cref="LevelHeadroom"/>); this draws them
/// and sends the latest ten bands to the headset through
/// <see cref="ParametricService"/>.
/// </para>
/// <para>
/// Each change keeps the bands as they were, so Undo and Redo walk back and
/// forward through them.
/// </para>
/// </remarks>
public sealed class BandPanel : UserControl
{
    private static readonly BandType[] Types =
        [BandType.Peaking, BandType.LowShelf, BandType.HighShelf, BandType.LowCut, BandType.HighCut, BandType.Notch];

    private readonly BandCurve _curve = new() { Height = 300, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _undo = new();
    private readonly Button _redo = new();
    private readonly ToggleButton _plain = new();
    private readonly Button _shapes = new();
    private readonly Border _level = new() { IsVisible = false };
    private readonly TextBlock _levelText = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _levelLower = new();

    private readonly TextBlock _title = new();
    private readonly CheckBox _inUse = new();
    private readonly ComboBox _type = new() { MinWidth = 140 };
    private readonly Button _reset = new();
    private readonly TextBlock _hint = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _frequencyLabel = new();
    private readonly TextBlock _widthLabel = new();
    private readonly TextBlock _gainLabel = new();
    private readonly BandDial _frequencyDial = new() { Minimum = ParametricBand.LowestHertz, Maximum = ParametricBand.HighestHertz, Logarithmic = true, Ratio = 1.0595, Value = 1000, DefaultValue = 1000 };
    private readonly BandDial _widthDial = new() { Minimum = ParametricBand.LowestQ, Maximum = ParametricBand.HighestQ, Logarithmic = true, Ratio = 1.1, Value = 1, DefaultValue = StockBands.Q };
    private readonly BandDial _gainDial = new() { Minimum = -ParametricBand.MostGain, Maximum = ParametricBand.MostGain, Origin = 0, SmallChange = 0.5, Value = 0, DefaultValue = 0 };
    private readonly BandValueBox _frequencyBox = new() { Width = 96 };
    private readonly BandValueBox _widthBox = new() { Width = 96 };
    private readonly BandValueBox _gainBox = new() { Width = 96 };

    private readonly Row[] _rows = new Row[BandWrites.Bands];

    private readonly Stack<List<ParametricBand>> _undoStack = new();
    private readonly Stack<List<ParametricBand>> _redoStack = new();
    private List<ParametricBand> _bands = [.. ParametricService.Saved];
    private int _selected;
    private bool _painting;

    private sealed record Row(CheckBox InUse, ComboBox Type, BandValueBox Hertz, BandValueBox Q, BandValueBox Gain, Border Frame);

    public BandPanel()
    {
        Content = Build();
        Wire();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.LoudnessChanged += PaintLevel;
        Show(ParametricService.Saved);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.LoudnessChanged -= PaintLevel;
    }

    /// <summary>Draws a set of bands everywhere, without sending them.</summary>
    /// <param name="bands">The ten bands.</param>
    public void Show(IReadOnlyList<ParametricBand> bands)
    {
        _bands = [.. bands];
        _selected = Math.Min(_selected, BandWrites.Bands - 1);
        Paint();
    }

    // -- building ----------------------------------------------------------

    private StackPanel Build()
    {
        _undo.Content = Strings.Get("Parametric_Undo");
        _redo.Content = Strings.Get("Parametric_Redo");
        _plain.Content = Strings.Get("Parametric_HearPlain");
        _shapes.Content = Strings.Get("Parametric_StartFrom");
        _reset.Content = Strings.Get("Parametric_ResetBand");
        _inUse.Content = Strings.Get("Parametric_InUse");
        foreach (var type in Types) _type.Items.Add(new ComboBoxItem { Content = Strings.Get(TypeKey(type)), Tag = type });
        _levelLower.Content = string.Empty;
        _title.Classes.Add("bodystrong");
        _hint.Classes.Add("caption");
        _hint.Classes.Add("secondary");
        _levelText.Classes.Add("secondary");
        AutomationProperties.SetName(_type, Strings.Get("Parametric_TypeLabel"));
        AutomationProperties.SetName(_inUse, Strings.Get("Parametric_InUse"));

        _shapes.Flyout = ShapesMenu();
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.S, Children = { _plain, _shapes, _undo, _redo } };

        _level.Classes.Add("card");
        _level.Child = new StackPanel { Spacing = Space.S, Children = { _levelText, _levelLower } };

        var dials = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = Space.M };
        Column(dials, 0, _frequencyLabel, _frequencyDial, _frequencyBox);
        Column(dials, 1, _widthLabel, _widthDial, _widthBox);
        Column(dials, 2, _gainLabel, _gainDial, _gainBox);

        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.M, Children = { _title, _inUse, _type, _reset } };
        var editor = new Border { Classes = { "card" }, Child = new StackPanel { Spacing = Space.M, Children = { head, dials, _hint } } };

        var table = new Grid { RowSpacing = Space.XS, ColumnSpacing = Space.S, ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,*,*,*") };
        string[] columns = ["Parametric_ColBand", "Parametric_ColInUse", "Parametric_ColFilter", "Parametric_ColHz", "Parametric_ColQ", "Parametric_ColDb"];
        for (int c = 0; c < columns.Length; c++)
        {
            var header = new TextBlock { Text = Strings.Get(columns[c]) };
            header.Classes.Add("caption");
            header.Classes.Add("secondary");
            Grid.SetColumn(header, c);
            table.Children.Add(header);
        }

        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int band = 0; band < BandWrites.Bands; band++) _rows[band] = AddRow(table, band);

        return new StackPanel { Spacing = Space.M, Children = { tools, _level, _curve, editor, table } };
    }

    private static void Column(Grid grid, int at, TextBlock label, BandDial dial, BandValueBox box)
    {
        label.Classes.Add("caption");
        label.Classes.Add("secondary");
        label.HorizontalAlignment = HorizontalAlignment.Center;
        var column = new StackPanel { Spacing = Space.XS, HorizontalAlignment = HorizontalAlignment.Center, Children = { label, dial, box } };
        Grid.SetColumn(column, at);
        grid.Children.Add(column);
    }

    private Row AddRow(Grid table, int band)
    {
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        int at = band + 1;
        var number = new TextBlock { Text = (band + 1).ToString(CultureInfo.CurrentCulture), VerticalAlignment = VerticalAlignment.Center };
        var inUse = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
        var type = new ComboBox { MinWidth = 120 };
        foreach (var t in Types) type.Items.Add(new ComboBoxItem { Content = Strings.Get(TypeKey(t)), Tag = t });
        var hertz = new BandValueBox();
        var q = new BandValueBox();
        var gain = new BandValueBox();
        AutomationProperties.SetName(inUse, Strings.Format("Parametric_RowInUse", band + 1));
        AutomationProperties.SetName(type, Strings.Format("Parametric_RowFilter", band + 1));
        AutomationProperties.SetName(hertz, Strings.Format("Parametric_RowFrequency", band + 1));
        AutomationProperties.SetName(q, Strings.Format("Parametric_RowWidth", band + 1));
        AutomationProperties.SetName(gain, Strings.Format("Parametric_RowGain", band + 1));
        var frame = new Border { Padding = new Thickness(0, 2), Child = number };
        Control[] cells = [frame, inUse, type, hertz, q, gain];
        for (int c = 0; c < cells.Length; c++)
        {
            Grid.SetRow(cells[c], at);
            Grid.SetColumn(cells[c], c);
            table.Children.Add(cells[c]);
        }

        int index = band;
        inUse.IsCheckedChanged += (_, _) => { if (!_painting) Change(index, _bands[index] with { InUse = inUse.IsChecked == true }); };
        type.SelectionChanged += (_, _) => { if (!_painting && type.SelectedItem is ComboBoxItem { Tag: BandType t }) Change(index, BandEditing.WithType(_bands[index], t)); };
        hertz.Entered += text => Take(index, FrequencyEntry.Parse(text, _bands[index].Hertz), (b, v) => b with { Hertz = v });
        q.Entered += text => Take(index, BandEntry.Q(text), (b, v) => b with { Q = v });
        gain.Entered += text => Take(index, BandEntry.Gain(text), (b, v) => b with { Gain = v });
        foreach (Control cell in new Control[] { inUse, type, hertz, q, gain })
            cell.GotFocus += (_, _) => Choose(index);
        return new Row(inUse, type, hertz, q, gain, frame);
    }

    private MenuFlyout ShapesMenu()
    {
        var menu = new MenuFlyout();
        foreach (var shape in Enum.GetValues<StartingShape>())
        {
            var item = new MenuItem { Header = Strings.Get(ShapeKey(shape)) };
            var chosen = shape;
            item.Click += (_, _) => Replace(StartingShapes.Bands(chosen), keepSelection: false);
            menu.Items.Add(item);
        }

        return menu;
    }

    private void Wire()
    {
        _curve.Changing += Keep;
        _curve.BandsChanged += bands => { _bands = [.. bands]; Send(); PaintValues(); };
        _curve.Chosen += Choose;
        _plain.IsCheckedChanged += (_, _) => { _curve.Muted = _plain.IsChecked == true; };
        _undo.Click += (_, _) => Walk(_undoStack, _redoStack);
        _redo.Click += (_, _) => Walk(_redoStack, _undoStack);
        _levelLower.Click += (_, _) => LowerLevel();

        _inUse.IsCheckedChanged += (_, _) => { if (!_painting) Change(_selected, _bands[_selected] with { InUse = _inUse.IsChecked == true }); };
        _type.SelectionChanged += (_, _) => { if (!_painting && _type.SelectedItem is ComboBoxItem { Tag: BandType t }) Change(_selected, BandEditing.WithType(_bands[_selected], t)); };
        _reset.Click += (_, _) => Change(_selected, BandEditing.Reset(_selected));

        _frequencyDial.Changing += Keep;
        _widthDial.Changing += Keep;
        _gainDial.Changing += Keep;
        _frequencyDial.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_painting) Apply(b => b with { Hertz = _frequencyDial.Value }); };
        _widthDial.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_painting) Apply(b => b with { Q = _widthDial.Value }); };
        _gainDial.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_painting) Apply(b => b with { Gain = Math.Round(_gainDial.Value * 10) / 10 }); };

        _frequencyBox.Entered += text => Take(_selected, FrequencyEntry.Parse(text, _bands[_selected].Hertz), (b, v) => b with { Hertz = v });
        _widthBox.Entered += text => Take(_selected, BandEntry.Q(text), (b, v) => b with { Q = v });
        _gainBox.Entered += text => Take(_selected, BandEntry.Gain(text), (b, v) => b with { Gain = v });
        _frequencyBox.Stepped += (direction, large) => Change(_selected, BandEditing.Nudged(_bands[_selected], direction * (large ? 12 : 1), 0, fine: false));
        _gainBox.Stepped += (direction, large) => Change(_selected, BandEditing.Nudged(_bands[_selected], 0, direction * (large ? 6 : 1), fine: false));
        _widthBox.Stepped += (direction, large) => Change(_selected, BandEditing.Widened(_bands[_selected], -direction * (large ? 5 : 1), fine: false));
    }

    // -- changes -----------------------------------------------------------

    /// <summary>Keeps the bands as they are, so Undo can come back to them.</summary>
    private void Keep()
    {
        _undoStack.Push([.. _bands]);
        _redoStack.Clear();
        PaintHistory();
    }

    private void Walk(Stack<List<ParametricBand>> from, Stack<List<ParametricBand>> to)
    {
        if (from.Count == 0) return;
        to.Push([.. _bands]);
        _bands = from.Pop();
        Send();
        Paint();
    }

    private void Choose(int band)
    {
        if (band == _selected) return;
        _selected = band;
        Paint();
    }

    /// <summary>One whole change to a band: kept for Undo, sent and drawn.</summary>
    private void Change(int band, ParametricBand to)
    {
        Keep();
        _selected = band;
        _bands[band] = to.Held();
        Send();
        Paint();
    }

    /// <summary>A change a dial makes, step by step: the dial has kept the bands already.</summary>
    private void Apply(Func<ParametricBand, ParametricBand> change)
    {
        _bands[_selected] = change(_bands[_selected]).Held() with { InUse = true };
        Send();
        PaintValues();
        _curve.Bands = _bands;
    }

    /// <summary>A typed value: taken when it reads as a number, and the field puts back what it had otherwise.</summary>
    private void Take(int band, double? value, Func<ParametricBand, double, ParametricBand> change)
    {
        if (value is not double v)
        {
            Paint();
            return;
        }

        Change(band, change(_bands[band], v) with { InUse = true });
    }

    private void Replace(IReadOnlyList<ParametricBand> bands, bool keepSelection)
    {
        Keep();
        _bands = [.. bands];
        if (!keepSelection) _selected = 0;
        Send();
        Paint();
    }

    private void Send() => AppServices.Parametric.Set(_bands);

    // -- painting ----------------------------------------------------------

    private void Paint()
    {
        _painting = true;
        try
        {
            _curve.Bands = _bands;
            _curve.Selected = _selected;
            PaintValues();
            PaintHistory();
            PaintLevel();
        }
        finally { _painting = false; }
    }

    private void PaintHistory()
    {
        _undo.IsEnabled = _undoStack.Count > 0;
        _redo.IsEnabled = _redoStack.Count > 0;
    }

    /// <summary>Fills the chosen band's controls and the table from the bands.</summary>
    private void PaintValues()
    {
        bool was = _painting;
        _painting = true;
        try
        {
            var band = _bands[_selected];
            bool cut = band.Type is BandType.LowCut or BandType.HighCut or BandType.GentleLowCut or BandType.GentleHighCut;
            bool shelf = band.Type is BandType.LowShelf or BandType.HighShelf;
            _title.Text = Strings.Format("Parametric_BandTitle", _selected + 1);
            _inUse.IsChecked = band.InUse;
            _type.SelectedIndex = Array.IndexOf(Types, band.Type);
            _frequencyLabel.Text = Strings.Get(cut ? "Parametric_CutoffLabel" : shelf ? "Parametric_CornerLabel" : "Parametric_FrequencyLabel");
            _widthLabel.Text = Strings.Get(cut ? "Parametric_ResonanceLabel" : shelf ? "Parametric_SlopeLabel" : "Parametric_WidthLabel");
            _gainLabel.Text = Strings.Get("Parametric_GainLabel");
            _frequencyDial.Value = band.Hertz;
            _widthDial.Value = band.Q;
            _gainDial.Value = band.Gain;
            _gainDial.IsEnabled = band.UsesGain;
            _gainBox.Editable = band.UsesGain;
            _frequencyBox.Show(FrequencyText.Of(band.Hertz));
            _widthBox.Show(band.Q.ToString("0.0#", CultureInfo.CurrentCulture));
            _gainBox.Show(band.UsesGain ? band.Gain.ToString("+0.0;-0.0;0.0", CultureInfo.CurrentCulture) : string.Empty);
            NameValues(_selected + 1);
            _hint.Text = cut ? Strings.Get("Parametric_HintCut")
                : shelf ? Strings.Get("Parametric_HintShelf")
                : band.Type == BandType.Notch ? Strings.Format("Parametric_HintNotch", Octaves(band.Q))
                : Strings.Format("Parametric_HintBell", Octaves(band.Q));

            for (int i = 0; i < _rows.Length; i++)
            {
                var b = _bands[i];
                var row = _rows[i];
                row.InUse.IsChecked = b.InUse;
                row.Type.SelectedIndex = Array.IndexOf(Types, b.Type);
                row.Hertz.Show(FrequencyText.Of(b.Hertz));
                row.Q.Show(b.Q.ToString("0.0#", CultureInfo.CurrentCulture));
                row.Gain.Show(b.UsesGain ? b.Gain.ToString("+0.0;-0.0;0.0", CultureInfo.CurrentCulture) : string.Empty);
                row.Gain.Editable = b.UsesGain;
                ((TextBlock)row.Frame.Child!).FontWeight = i == _selected ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;
            }
        }
        finally { _painting = was; }
    }

    private void NameValues(int band)
    {
        AutomationProperties.SetName(_frequencyDial, Strings.Format("Parametric_RowFrequency", band));
        AutomationProperties.SetName(_widthDial, Strings.Format("Parametric_RowWidth", band));
        AutomationProperties.SetName(_gainDial, Strings.Format("Parametric_RowGain", band));
        AutomationProperties.SetName(_frequencyBox, Strings.Format("Parametric_RowFrequency", band));
        AutomationProperties.SetName(_widthBox, Strings.Format("Parametric_RowWidth", band));
        AutomationProperties.SetName(_gainBox, Strings.Format("Parametric_RowGain", band));
    }

    private static string Octaves(double q) =>
        (2 * Math.Asinh(1 / (2 * q)) / Math.Log(2)).ToString("0.0", CultureInfo.CurrentCulture);

    /// <summary>Says when the biggest boost at the headset's level would reach its compressor, and offers to lower the level.</summary>
    private void PaintLevel()
    {
        if (AppServices.Headset.Loudness is not { Available: true } reading)
        {
            _level.IsVisible = false;
            return;
        }

        bool squashes = LevelHeadroom.Squashes(reading.LevelDb, _bands);
        _level.IsVisible = squashes;
        if (!squashes) return;
        double lower = LevelHeadroom.LowerBy(reading.LevelDb, _bands);
        _levelText.Text = Strings.Get("Parametric_LevelNotice");
        _levelLower.Content = Strings.Format("Parametric_LevelLower", lower.ToString("0.#", CultureInfo.CurrentCulture));
        _levelLower.Tag = lower;
    }

    private void LowerLevel()
    {
        if (AppServices.Headset.Loudness is not { Available: true } reading || _levelLower.Tag is not double lower) return;
        AppServices.Headset.SetLoudnessBoost((float)(reading.BoostDb - lower));
    }

    private static string TypeKey(BandType type) => type switch
    {
        BandType.LowShelf => "Parametric_TypeLowShelf",
        BandType.HighShelf => "Parametric_TypeHighShelf",
        BandType.LowCut => "Parametric_TypeLowCut",
        BandType.HighCut => "Parametric_TypeHighCut",
        BandType.Notch => "Parametric_TypeNotch",
        _ => "Parametric_TypeBell",
    };

    private static string ShapeKey(StartingShape shape) => shape switch
    {
        StartingShape.CutMud => "Parametric_ShapeCutMud",
        StartingShape.VoiceClarity => "Parametric_ShapeVoiceClarity",
        StartingShape.TameHarshness => "Parametric_ShapeTameHarshness",
        StartingShape.AddBass => "Parametric_ShapeAddBass",
        StartingShape.CutRumble => "Parametric_ShapeCutRumble",
        StartingShape.TameSibilance => "Parametric_ShapeTameSibilance",
        StartingShape.QuietListening => "Parametric_ShapeQuietListening",
        StartingShape.AddTreble => "Parametric_ShapeAddTreble",
        _ => "Parametric_ShapeFlat",
    };
}
