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

    /// <summary>How tall the curve is drawn.</summary>
    private const double CurveHeight = 300;

    /// <summary>How wide the band's dials are when they sit beside the table: three dials and their fields.</summary>
    private const double EditorWidth = 360;

    /// <summary>How wide the panel must be for the band's dials to sit beside the table, leaving its fields room to read.</summary>
    private const double BesideFrom = 860;

    /// <summary>The width of a filter's drop-down, the same in the band's row and the table.</summary>
    private const double FilterWidth = 140;

    private readonly BandCurve _curve = new() { Height = CurveHeight, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ToneStrip _tone = new();
    private readonly CurvesBar _curvesBar = new();
    private readonly Button _undo = new() { Classes = { "quiet", "iconbutton" }, IsEnabled = false };
    private readonly Button _redo = new() { Classes = { "quiet", "iconbutton" }, IsEnabled = false };
    private readonly Button _compare = new() { Classes = { "quiet" }, HorizontalContentAlignment = HorizontalAlignment.Center };
    private bool _comparing;
    private Window? _window;
    private readonly Button _turnOff = new() { HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Notice _level = new() { Severity = Severity.Warning };
    private double _lowerBy;

    private readonly Grid _main = new() { ColumnSpacing = Space.L, RowSpacing = Space.L };
    private readonly Border _editor = new() { Classes = { "options" }, Margin = default, VerticalAlignment = VerticalAlignment.Top };
    private bool? _beside;

    private readonly TextBlock _title = new() { Classes = { "label" } };
    private readonly ComboBox _type = new() { Width = FilterWidth };
    private readonly Button _remove = new();
    private readonly Button _add = new();
    private readonly StackPanel _bandControls = new() { Spacing = Space.M };
    private readonly TextBlock _empty = new() { Classes = { "secondary" } };
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

    /// <summary>The table's column names, shown only once there is a band to name.</summary>
    private readonly List<TextBlock> _columns = [];
    private readonly ParametricPresetBar _presets;

    private readonly Stack<List<ParametricBand>> _undoStack = new();
    private readonly Stack<List<ParametricBand>> _redoStack = new();
    private List<ParametricBand> _bands = [.. ParametricService.Saved];
    private int _selected;
    private bool _painting;

    private sealed record Row(Border Frame, ComboBox Type, BandValueBox Hertz, BandValueBox Q, BandValueBox Gain, Button Remove)
    {
        public Control[] Cells => [Frame, Type, Hertz, Q, Gain, Remove];
    }

    /// <summary>The person asked to turn Parametric off, which the page asks about and does.</summary>
    public event Action? TurnOffAsked;

    /// <summary>Compare, Undo, Redo and Turn off, which the page puts beside its choice of mode.</summary>
    public Control Tools { get; }

    public BandPanel()
    {
        _presets = new ParametricPresetBar(() => _bands, bands => Replace(bands, keepSelection: false));
        Tools = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            ItemSpacing = Space.S,
            LineSpacing = Space.S,
            Children = { _compare, _undo, _redo, _turnOff },
        };
        Content = Build();
        Wire();
        SizeChanged += (_, _) => Arrange();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.LoudnessChanged += PaintLevel;
        AppServices.Parametric.ShapeChanged += ShowSaved;
        ShowSaved();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.LoudnessChanged -= PaintLevel;
        AppServices.Parametric.ShapeChanged -= ShowSaved;
        _tone.Stop();
        StopCompare();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null) _window.Deactivated += OnWindowDeactivated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_window is not null) _window.Deactivated -= OnWindowDeactivated;
        _window = null;
        StopCompare();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => StopCompare();

    private void ShowSaved() => Show(ParametricService.Saved);

    // A tone left playing, or Compare left holding flat, behind a hidden panel is one nobody can stop.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsVisibleProperty || IsVisible) return;
        _tone.Stop();
        StopCompare();
    }

    /// <summary>Draws a set of bands everywhere, in order, without sending them.</summary>
    /// <param name="bands">The ten bands.</param>
    public void Show(IReadOnlyList<ParametricBand> bands)
    {
        _bands = [.. BandEditing.InOrder(bands)];
        if (!_bands[Math.Min(_selected, BandWrites.Bands - 1)].InUse) _selected = 0;
        Paint();
    }

    // -- building ----------------------------------------------------------

    private StackPanel Build()
    {
        MakeIconButton(_undo, "Parametric_Undo", "IconUndo", mirrored: false);
        MakeIconButton(_redo, "Parametric_Redo", "IconUndo", mirrored: true);
        _compare.Content = Strings.Get("Parametric_Compare");
        ToolTip.SetTip(_compare, Strings.Get("Parametric_CompareTip"));
        _turnOff.Content = Strings.Get("Parametric_TurnOff");
        _remove.Content = Strings.Get("Parametric_RemoveBand");
        _add.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Space.S,
            Children = { Icon("IconAdd"), new TextBlock { Text = Strings.Get("Parametric_AddBand") } },
        };
        AutomationProperties.SetName(_add, Strings.Get("Parametric_AddBand"));
        _empty.Text = Strings.Get("Parametric_NoBands");
        foreach (var type in Types) _type.Items.Add(new ComboBoxItem { Content = Strings.Get(TypeKey(type)), Tag = type });
        _hint.Classes.Add("caption");
        _hint.Classes.Add("secondary");
        _level.Message = Strings.Get("Parametric_LevelNotice");
        AutomationProperties.SetName(_type, Strings.Get("Parametric_TypeLabel"));

        // Kept together at the left, whatever the width, so the dials sit under the band's own controls.
        var dials = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), ColumnSpacing = Space.XL, HorizontalAlignment = HorizontalAlignment.Left };
        Column(dials, 0, _frequencyLabel, _frequencyDial, _frequencyBox);
        Column(dials, 1, _widthLabel, _widthDial, _widthBox);
        Column(dials, 2, _gainLabel, _gainDial, _gainBox);

        // The band's own controls on one row, over the three dials.
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), ColumnSpacing = Space.M };
        _type.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_remove, 1);
        head.Children.AddRange([_type, _remove]);
        _bandControls.Children.AddRange([_title, head, dials, _hint]);
        _editor.Child = new Panel { Children = { _bandControls, _empty } };

        // No row spacing: the grid would keep it for the hidden rows too, so each row keeps its own gap above it.
        var table = new Grid { ColumnSpacing = Space.S, ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,*,*,Auto") };
        string[] columns = ["Parametric_ColBand", "Parametric_ColFilter", "Parametric_ColHz", "Parametric_ColQ", "Parametric_ColDb"];
        for (int c = 0; c < columns.Length; c++)
        {
            var header = new TextBlock { Text = Strings.Get(columns[c]) };
            header.Classes.Add("caption");
            header.Classes.Add("secondary");
            Grid.SetColumn(header, c);
            table.Children.Add(header);
            _columns.Add(header);
        }

        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int band = 0; band < BandWrites.Bands; band++) _rows[band] = AddRow(table, band);
        var all = new StackPanel
        {
            Spacing = Space.S,
            Children = { AllBandsHead(), table },
        };

        // The curve takes the whole width; the table and the chosen band's dials share the row under it.
        _main.Children.AddRange([all, _editor]);
        return new StackPanel { Spacing = Space.L, Children = { _tone, _presets, _level, new StackPanel { Spacing = Space.S, Children = { _curvesBar, _curve } }, _main } };
    }

    /// <summary>The table's label, with Add band at the end of its row.</summary>
    private Grid AllBandsHead()
    {
        var label = new TextBlock { Text = Strings.Get("Parametric_AllBands"), Classes = { "label" }, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_add, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { label, _add } };
    }

    private static PathIcon Icon(string key)
    {
        var icon = new PathIcon { Width = Space.Icon, Height = Space.Icon, Data = (Avalonia.Media.Geometry)Application.Current!.FindResource(key)! };
        AutomationProperties.SetAccessibilityView(icon, AccessibilityView.Raw);
        return icon;
    }

    /// <summary>Makes an icon button of Undo or Redo, as the ten-band equaliser's are, named for a screen reader and a tooltip.</summary>
    private static void MakeIconButton(Button button, string key, string icon, bool mirrored)
    {
        string name = Strings.Get(key);
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
        var drawn = Icon(icon);
        if (mirrored) drawn.RenderTransform = new Avalonia.Media.ScaleTransform(-1, 1);
        button.Content = drawn;
    }

    /// <summary>Puts the band's dials beside the table when there is room for both, and above it when there is not.</summary>
    private void Arrange()
    {
        bool beside = Bounds.Width >= BesideFrom;
        if (beside == _beside) return;
        _beside = beside;
        _main.ColumnDefinitions = beside ? new ColumnDefinitions($"*,{EditorWidth}") : new ColumnDefinitions("*");
        _main.RowDefinitions = beside ? new RowDefinitions("Auto") : new RowDefinitions("Auto,Auto");
        var all = _main.Children[0];
        Grid.SetColumn(_editor, beside ? 1 : 0);
        Grid.SetRow(_editor, 0);
        Grid.SetRow(all, beside ? 0 : 1);
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
        var type = new ComboBox { Width = FilterWidth };
        foreach (var t in Types) type.Items.Add(new ComboBoxItem { Content = Strings.Get(TypeKey(t)), Tag = t });
        var hertz = new BandValueBox();
        var q = new BandValueBox();
        var gain = new BandValueBox();
        AutomationProperties.SetName(type, Strings.Format("Parametric_RowFilter", band + 1));
        AutomationProperties.SetName(hertz, Strings.Format("Parametric_RowFrequency", band + 1));
        AutomationProperties.SetName(q, Strings.Format("Parametric_RowWidth", band + 1));
        AutomationProperties.SetName(gain, Strings.Format("Parametric_RowGain", band + 1));
        var remove = new Button { Classes = { "quiet", "iconbutton" }, Content = Icon("IconDelete") };
        string removeName = Strings.Format("Parametric_RowRemove", band + 1);
        AutomationProperties.SetName(remove, removeName);
        ToolTip.SetTip(remove, removeName);
        var frame = new Border { Padding = new Thickness(0, 2), Child = number };
        Control[] cells = [frame, type, hertz, q, gain, remove];
        for (int c = 0; c < cells.Length; c++)
        {
            Grid.SetRow(cells[c], at);
            Grid.SetColumn(cells[c], c);
            cells[c].Margin = new Thickness(0, Space.XS, 0, 0);
            table.Children.Add(cells[c]);
        }

        int index = band;
        remove.Click += (_, _) => RemoveBand(index);
        type.SelectionChanged += (_, _) => { if (!_painting && type.SelectedItem is ComboBoxItem { Tag: BandType t }) Change(index, BandEditing.WithType(_bands[index], t)); };
        hertz.Entered += text => Take(index, FrequencyEntry.Parse(text, _bands[index].Hertz), (b, v) => b with { Hertz = v });
        q.Entered += text => Take(index, BandEntry.Q(text), (b, v) => b with { Q = v });
        gain.Entered += text => Take(index, BandEntry.Gain(text), (b, v) => b with { Gain = v });
        foreach (Control cell in new Control[] { type, hertz, q, gain })
            cell.GotFocus += (_, _) => Choose(index);
        return new Row(frame, type, hertz, q, gain, remove);
    }

    private void Wire()
    {
        _curve.Changing += Keep;
        _curve.BandsChanged += bands => { _bands = [.. bands]; Send(); PaintValues(); PaintKey(); };
        _curve.Chosen += Choose;
        _tone.Moved += hertz => _curve.ToneHertz = hertz;
        _curvesBar.Changed += () =>
        {
            (_curve.Measured, _curve.Target) = (_curvesBar.Measured, _curvesBar.Target);
            PaintKey();
        };
        _curvesBar.AutoEqAsked += async (measured, target) =>
        {
            var bands = await Task.Run(() => AutoEq.Fit(measured.Toward(target)));
            int used = bands.Count(band => band.InUse);
            AppLog.Write($"parametric: Auto-EQ set {used} bands");
            if (used > 0) Replace(bands, keepSelection: false);
            else await ProfileDialogs.Complain(this, Strings.Get("Curves_AutoEq"), Strings.Get("Curves_AutoEqNone"));
        };
        _tone.Marked += (hertz, boost) =>
        {
            AddBand(ToneSweep.Bell(hertz, boost));
        };
        WireCompare();
        _undo.Click += (_, _) => Walk(_undoStack, _redoStack);
        _redo.Click += (_, _) => Walk(_redoStack, _undoStack);
        _level.ActionInvoked += (_, _) => LowerLevel();
        _presets.ImportAsked += async () =>
        {
            if (await FilterImportDialog.Ask(this) is not { } imported) return;
            _presets.CameFrom(imported.Source);
            Replace(imported.Bands, keepSelection: false);
        };
        _turnOff.Click += (_, _) => TurnOffAsked?.Invoke();

        _type.SelectionChanged += (_, _) => { if (!_painting && _type.SelectedItem is ComboBoxItem { Tag: BandType t }) Change(_selected, BandEditing.WithType(_bands[_selected], t)); };
        _remove.Click += (_, _) => RemoveBand(_selected);
        _add.Click += (_, _) => AddBand(BandEditing.NewBand(_bands));

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

    // -- compare -----------------------------------------------------------

    /// <summary>Holds Compare while the button is pressed, by pointer or by Space or Enter, as the ten-band equaliser's Compare does.</summary>
    private void WireCompare()
    {
        // The button handles its own presses, which is not a reason to miss one.
        _compare.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(_compare).Properties.IsLeftButtonPressed) StartCompare();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        _compare.AddHandler(PointerReleasedEvent, (_, _) => StopCompare(), RoutingStrategies.Bubble, handledEventsToo: true);
        _compare.PointerCaptureLost += (_, _) => StopCompare();
        _compare.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key is Avalonia.Input.Key.Space or Avalonia.Input.Key.Enter) StartCompare();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        _compare.AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key is Avalonia.Input.Key.Space or Avalonia.Input.Key.Enter) StopCompare();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        _compare.LostFocus += (_, _) => StopCompare();
    }

    /// <summary>Plays flat on the headset and draws the curve muted, until let go.</summary>
    private void StartCompare()
    {
        if (_comparing) return;
        _comparing = true;
        _curve.Muted = true;
        PaintKey();
        AppServices.Parametric.Compare(flat: true, _bands);
    }

    /// <summary>Puts the shape back on the headset. Safe to call whenever.</summary>
    private void StopCompare()
    {
        if (!_comparing) return;
        _comparing = false;
        _curve.Muted = false;
        PaintKey();
        AppServices.Parametric.Compare(flat: false, _bands);
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

    /// <summary>Adds a band, in its place by frequency, and chooses it: one change Undo can take back.</summary>
    private void AddBand(ParametricBand band)
    {
        if (BandEditing.Added(_bands, band) is not { } added) return;
        Keep();
        _bands = [.. added.Bands];
        _selected = added.At;
        Send();
        Paint();
    }

    /// <summary>Takes a band out of use, choosing the one now in its place: one change Undo can take back.</summary>
    private void RemoveBand(int band)
    {
        Keep();
        var (bands, chosen) = BandEditing.Removed(_bands, band);
        _bands = [.. bands];
        _selected = chosen;
        Send();
        Paint();
    }

    private void Replace(IReadOnlyList<ParametricBand> bands, bool keepSelection)
    {
        Keep();
        _bands = [.. BandEditing.InOrder(bands)];
        if (!keepSelection) _selected = 0;
        Send();
        Paint();
    }

    private void Send()
    {
        AppServices.Parametric.Set(_bands);
        _presets.Paint();
    }

    // -- painting ----------------------------------------------------------

    /// <summary>Lists the measurement with the bands added in the key only while the curve draws it.</summary>
    private void PaintKey() => _curvesBar.WithChanges = _curve.ShowsHeard;

    private void Paint()
    {
        _painting = true;
        try
        {
            _curve.Bands = _bands;
            _curve.Selected = _selected;
            PaintKey();
            _tone.CanMark = BandEditing.FirstFree(_bands) >= 0;
            PaintValues();
            PaintHistory();
            PaintLevel();
            _presets.Paint();
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
            int inUse = _bands.Count(b => b.InUse);
            _bandControls.IsVisible = band.InUse;
            _empty.IsVisible = inUse == 0;
            _add.IsEnabled = inUse < BandWrites.Bands;
            foreach (var column in _columns) column.IsVisible = inUse > 0;
            _title.Text = Strings.Format("Parametric_BandTitle", _selected + 1);
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
                foreach (var cell in row.Cells) cell.IsVisible = b.InUse;
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
            _level.IsOpen = false;
            return;
        }

        bool squashes = LevelHeadroom.Squashes(reading.LevelDb, _bands);
        _level.IsOpen = squashes;
        if (!squashes) return;
        _lowerBy = LevelHeadroom.LowerBy(reading.LevelDb, _bands);
        _level.ActionText = Strings.Format("Parametric_LevelLower", _lowerBy.ToString("0.#", CultureInfo.CurrentCulture));
    }

    private void LowerLevel()
    {
        if (AppServices.Headset.Loudness is not { Available: true } reading || _lowerBy <= 0) return;
        AppServices.Headset.SetLoudnessBoost((float)(reading.BoostDb - _lowerBy));
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
}
