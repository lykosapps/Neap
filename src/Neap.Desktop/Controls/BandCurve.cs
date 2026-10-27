using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Neap.Core.Presets;
using Path = Avalonia.Controls.Shapes.Path;

namespace Neap.Desktop.Controls;

/// <summary>
/// The parametric equaliser's curve: the ten bands as points on a logarithmic
/// frequency scale, the curve they make shaded between it and zero, and the
/// chosen band's own shape.
/// </summary>
/// <remarks>
/// <para>
/// A point is dragged for frequency and gain, the wheel over it sets its
/// width, a double press on it puts it back and a double press on empty space
/// makes a bell there. The arrow keys move the chosen point, Page Up and Page
/// Down choose another, and Delete switches it off. Shift makes every
/// movement fine. <see cref="BandEditing"/> and <see cref="PlotScale"/> decide
/// what each does; this draws the result and says what changed.
/// </para>
/// <para>
/// The line is what the bands are set to do on paper (<see cref="BandResponse"/>),
/// not a measurement of what is heard. A screen reader has the table of bands
/// beside it for the same values.
/// </para>
/// </remarks>
public sealed class BandCurve : UserControl
{
    /// <summary>How near a point a press must land to take it.</summary>
    private const double Reach = 14;

    private const double PointSize = 20;
    private const int Samples = 180;

    private static readonly Color[] BandColours =
    [
        Color.Parse("#E5484D"), Color.Parse("#F76B15"), Color.Parse("#D19A00"), Color.Parse("#3E9B4F"), Color.Parse("#12A594"),
        Color.Parse("#0090FF"), Color.Parse("#8E4EC6"), Color.Parse("#D6409F"), Color.Parse("#6E6E7A"), Color.Parse("#A07A3C"),
    ];

    private readonly Canvas _canvas = new() { ClipToBounds = true };
    private readonly Path _grid = new() { StrokeThickness = 0.5 };
    private readonly Path _zero = new() { StrokeThickness = 1.2 };
    private readonly Path _fill = new() { Opacity = 0.22 };
    private readonly Path _own = new() { Opacity = 0.28 };
    private readonly Path _curve = new() { StrokeThickness = 2.5, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round };
    private readonly Line _tone = new() { StrokeThickness = 1, StrokeDashArray = [4, 4] };
    private readonly List<Control> _labels = [];
    private readonly Ellipse[] _points = new Ellipse[BandWrites.Bands];
    private readonly TextBlock[] _numbers = new TextBlock[BandWrites.Bands];
    private readonly Ellipse _focusRing = new() { Width = PointSize + 10, Height = PointSize + 10, StrokeThickness = 2, IsVisible = false };

    private List<ParametricBand> _bands = [.. StockBands.Game.Select(band => band with { InUse = false })];
    private int _selected;
    private double? _toneHertz;
    private bool _muted;

    private int _dragging = -1;
    private ParametricBand _dragStart;
    private double _dragFromX, _dragFromY;

    public BandCurve()
    {
        Focusable = true;
        Content = _canvas;
        AutomationProperties.SetName(this, Strings.Get("Parametric_BandCurveName"));
        AutomationProperties.SetHelpText(this, Strings.Get("Parametric_BandCurveHelp"));

        Use(_grid, Shape.StrokeProperty, "NeapRuleBrush");
        Use(_zero, Shape.StrokeProperty, "NeapTextTertiaryBrush");
        Use(_fill, Shape.FillProperty, "NeapAccentFillBrush");
        Use(_curve, Shape.StrokeProperty, "NeapAccentFillBrush");
        Use(_tone, Shape.StrokeProperty, "NeapTextSecondaryBrush");
        Use(_focusRing, Shape.StrokeProperty, "NeapTextBrush");
        _canvas.Children.AddRange([_grid, _zero, _fill, _own, _curve, _tone, _focusRing]);

        for (int band = 0; band < _points.Length; band++)
        {
            _points[band] = new Ellipse { Width = PointSize, Height = PointSize, StrokeThickness = 2.5, Stroke = new SolidColorBrush(BandColours[band]) };
            Use(_points[band], Shape.FillProperty, "NeapPanelBrush");
            _numbers[band] = new TextBlock { Text = (band + 1).ToString(System.Globalization.CultureInfo.CurrentCulture), FontSize = 11, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, Width = PointSize };
            Use(_numbers[band], TextBlock.ForegroundProperty, "NeapTextBrush");
            _canvas.Children.AddRange([_points[band], _numbers[band]]);
        }

        SizeChanged += (_, _) => Rebuild();
    }

    /// <summary>The bands moved. Raised for every step of a drag, so a screen can send the latest.</summary>
    public event Action<IReadOnlyList<ParametricBand>>? BandsChanged;

    /// <summary>A change is about to begin, so a screen can keep what it was before.</summary>
    public event Action? Changing;

    /// <summary>Another band was chosen.</summary>
    public event Action<int>? Chosen;

    /// <summary>The ten bands drawn.</summary>
    public IReadOnlyList<ParametricBand> Bands
    {
        get => _bands;
        set
        {
            _bands = [.. value];
            Draw();
        }
    }

    /// <summary>The chosen band, counted from zero.</summary>
    public int Selected
    {
        get => _selected;
        set
        {
            _selected = Math.Clamp(value, 0, BandWrites.Bands - 1);
            Draw();
        }
    }

    /// <summary>The test tone's frequency, drawn as a line, or null when there is none.</summary>
    public double? ToneHertz
    {
        get => _toneHertz;
        set
        {
            _toneHertz = value;
            Draw();
        }
    }

    /// <summary>Whether the curve is being heard without its changes, so it is drawn flat and not applied.</summary>
    public bool Muted
    {
        get => _muted;
        set
        {
            _muted = value;
            Draw();
        }
    }

    protected override Type StyleKeyOverride => typeof(UserControl);

    private static void Use(AvaloniaObject target, AvaloniaProperty<IBrush?> property, string key) =>
        target.Bind(property, ((StyledElement)target).GetResourceObservable(key));

    private PlotScale Scale => new(Math.Max(Bounds.Width, 1), Math.Max(Bounds.Height, 1));

    // -- drawing -----------------------------------------------------------

    /// <summary>Draws the grid and its labels again for the control's size, then everything on it.</summary>
    private void Rebuild()
    {
        var scale = Scale;
        foreach (var label in _labels) _canvas.Children.Remove(label);
        _labels.Clear();

        var grid = new PathGeometry();
        foreach (double db in new[] { -12.0, -6, 6, 12 })
        {
            Line(grid, 0, scale.Y(db), scale.Width, scale.Y(db));
            Label($"{db:+0;-0} dB", 4, scale.Y(db) - 14, HorizontalAlignment.Left);
        }

        foreach (double hertz in new[] { 50.0, 100, 200, 500, 1000, 2000, 5000, 10000 })
        {
            Line(grid, scale.X(hertz), 0, scale.X(hertz), scale.Height);
            Label(hertz >= 1000 ? $"{hertz / 1000:0.#} k" : $"{hertz:0}", scale.X(hertz) + 3, scale.Height - 16, HorizontalAlignment.Left);
        }

        _grid.Data = grid;
        var zero = new PathGeometry();
        Line(zero, 0, scale.Y(0), scale.Width, scale.Y(0));
        _zero.Data = zero;
        Draw();
    }

    private static void Line(PathGeometry geometry, double x0, double y0, double x1, double y1)
    {
        var figure = new PathFigure { StartPoint = new Point(x0, y0), IsClosed = false, IsFilled = false };
        figure.Segments!.Add(new LineSegment { Point = new Point(x1, y1) });
        geometry.Figures!.Add(figure);
    }

    private void Label(string text, double x, double y, HorizontalAlignment alignment)
    {
        var label = new TextBlock { Text = text, FontSize = 11, IsHitTestVisible = false, HorizontalAlignment = alignment };
        Use(label, TextBlock.ForegroundProperty, "NeapTextSecondaryBrush");
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        _labels.Add(label);
        _canvas.Children.Insert(0, label);
    }

    private void Draw()
    {
        if (Bounds.Width < 2 || Bounds.Height < 2) return;
        var scale = Scale;
        var bands = _muted ? [] : _bands;

        var curve = new PathFigure { IsClosed = false, IsFilled = false };
        var fill = new PathFigure { IsClosed = true, IsFilled = true };
        var own = new PathFigure { IsClosed = true, IsFilled = true };
        double zero = scale.Y(0);
        var chosen = _selected < _bands.Count ? _bands[_selected] : default;
        bool showOwn = !_muted && _selected < _bands.Count && chosen.InUse;
        for (int i = 0; i <= Samples; i++)
        {
            double x = scale.Width * i / Samples;
            double hertz = scale.Hertz(x);
            var at = new Point(x, scale.Y(BandResponse.TotalDb(bands, hertz)));
            if (i == 0)
            {
                curve.StartPoint = at;
                fill.StartPoint = new Point(0, zero);
                own.StartPoint = new Point(0, zero);
            }
            else
            {
                curve.Segments!.Add(new LineSegment { Point = at });
            }

            fill.Segments!.Add(new LineSegment { Point = at });
            if (showOwn) own.Segments!.Add(new LineSegment { Point = new Point(x, scale.Y(BandResponse.Db(chosen, hertz))) });
        }

        fill.Segments!.Add(new LineSegment { Point = new Point(scale.Width, zero) });
        own.Segments!.Add(new LineSegment { Point = new Point(scale.Width, zero) });
        _curve.Data = new PathGeometry { Figures = [curve] };
        _fill.Data = new PathGeometry { Figures = [fill] };
        _own.Data = new PathGeometry { Figures = [own] };
        _own.IsVisible = showOwn;
        _own.Fill = new SolidColorBrush(BandColours[_selected]);
        _curve.StrokeDashArray = _muted ? [6, 5] : null;

        if (_toneHertz is double tone)
        {
            _tone.StartPoint = new Point(scale.X(tone), 0);
            _tone.EndPoint = new Point(scale.X(tone), scale.Height);
        }

        _tone.IsVisible = _toneHertz is not null;

        for (int band = 0; band < _points.Length; band++)
        {
            double x = scale.X(_bands[band].Hertz), y = scale.Y(PlotScale.PointDb(_bands[band]));
            Canvas.SetLeft(_points[band], x - (PointSize / 2));
            Canvas.SetTop(_points[band], y - (PointSize / 2));
            Canvas.SetLeft(_numbers[band], x - (PointSize / 2));
            Canvas.SetTop(_numbers[band], y - 8);
            _points[band].Opacity = _bands[band].InUse && !_muted ? 1 : 0.45;
            _points[band].StrokeThickness = band == _selected ? 4 : 2.5;
        }

        Canvas.SetLeft(_focusRing, scale.X(_bands[_selected].Hertz) - ((PointSize + 10) / 2));
        Canvas.SetTop(_focusRing, scale.Y(PlotScale.PointDb(_bands[_selected])) - ((PointSize + 10) / 2));
    }

    // -- the pointer -------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetCurrentPoint(_canvas);
        if (!point.Properties.IsLeftButtonPressed) return;
        double x = point.Position.X, y = point.Position.Y;
        int hit = Scale.Nearest(_bands, x, y, Reach);

        if (e.ClickCount == 2)
        {
            if (hit >= 0) Change(hit, BandEditing.Reset(hit));
            else if (BandEditing.FirstFree(_bands) is >= 0 and var free) Change(free, BandEditing.BellAt(Scale, x, y));
            e.Handled = true;
            return;
        }

        if (hit < 0) return;
        Choose(hit);
        Changing?.Invoke();
        _dragging = hit;
        _dragStart = _bands[hit];
        (_dragFromX, _dragFromY) = (x, y);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging < 0) return;
        var at = e.GetPosition(_canvas);
        Set(_dragging, BandEditing.Dragged(_dragStart, Scale, _dragFromX, _dragFromY, at.X, at.Y, e.KeyModifiers.HasFlag(KeyModifiers.Shift)));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging < 0) return;
        _dragging = -1;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = -1;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Delta.Y == 0) return;
        var at = e.GetPosition(_canvas);
        int band = Scale.Nearest(_bands, at.X, at.Y, Reach);
        if (band < 0) band = _selected;
        Choose(band);
        Change(band, BandEditing.Widened(_bands[band], Math.Sign(e.Delta.Y), e.KeyModifiers.HasFlag(KeyModifiers.Shift)));
        e.Handled = true;
    }

    // -- the keyboard ------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        bool fine = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.Left: Change(_selected, BandEditing.Nudged(_bands[_selected], -1, 0, fine)); break;
            case Key.Right: Change(_selected, BandEditing.Nudged(_bands[_selected], 1, 0, fine)); break;
            case Key.Up: Change(_selected, BandEditing.Nudged(_bands[_selected], 0, 1, fine)); break;
            case Key.Down: Change(_selected, BandEditing.Nudged(_bands[_selected], 0, -1, fine)); break;
            case Key.PageUp: Choose((_selected + 1) % BandWrites.Bands); break;
            case Key.PageDown: Choose((_selected + BandWrites.Bands - 1) % BandWrites.Bands); break;
            case Key.Delete or Key.Back: Change(_selected, _bands[_selected] with { InUse = false }); break;
            case Key.Enter or Key.Space: Change(_selected, _bands[_selected] with { InUse = !_bands[_selected].InUse }); break;
            default:
                base.OnKeyDown(e);
                return;
        }

        e.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing.IsVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing.IsVisible = false;
    }

    // -- changes -----------------------------------------------------------

    private void Choose(int band)
    {
        if (band == _selected) return;
        _selected = band;
        Draw();
        Chosen?.Invoke(band);
    }

    /// <summary>One discrete change: tells the screen it is about to begin, then makes it.</summary>
    private void Change(int band, ParametricBand to)
    {
        Changing?.Invoke();
        Choose(band);
        Set(band, to);
    }

    private void Set(int band, ParametricBand to)
    {
        _bands[band] = to;
        Draw();
        BandsChanged?.Invoke(_bands);
    }
}
