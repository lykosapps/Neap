using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Neap.Core.Presets;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Neap.App.Controls;

/// <summary>
/// Draws the parametric equaliser: the curve asked for, the curve the
/// headset plays, the gap between them, and a point per adjustment to drag.
/// </summary>
/// <remarks>
/// <para>
/// The curve asked for is dashed and the curve heard is solid, in the accent
/// as the band curve is. The gap between them is shaded, so how far the
/// headset falls short shows without a word. The ten bands are marked along
/// the bottom, so it is plain where the headset can act.
/// </para>
/// <para>
/// Dragging a point moves its adjustment's frequency and gain; the mouse
/// wheel over it changes its width. The fields under the plot do all of this
/// from the keyboard, so the plot is hidden from screen readers.
/// </para>
/// </remarks>
public sealed class ParametricCurve : UserControl
{
    /// <summary>The plot's range either side of 0 dB, in tenths: wider than a band, since bands add up.</summary>
    private const int Range = 120;

    private const double Inset = 10;
    private const double LabelRoom = 20;
    private const double HandleRadius = 9;
    private const double GrabRadius = 16;
    private const int Samples = 160;

    private readonly Canvas _canvas = new();
    private readonly Path _gap = new();
    private readonly Path _heard = new();
    private readonly Path _asked = new();
    private readonly List<Line> _grid = new();
    private readonly List<Line> _ticks = new();
    private readonly List<TextBlock> _labels = new();
    private readonly List<(Ellipse Grab, Ellipse Dot, TextBlock Number)> _handles = new();

    private IReadOnlyList<Adjustment> _adjustments = [];
    private int[] _bands = [];
    private int _selected;
    private int _dragging = -1;

    /// <summary>The bands' labels, as the band view shows them.</summary>
    public IReadOnlyList<string> Frequencies { get; set; } = [];

    /// <summary>Raised when a point is dragged or its width wheeled, with its index and new value.</summary>
    public event Action<int, Adjustment>? AdjustmentChanged;

    /// <summary>Raised when a point is pressed, choosing its adjustment for the fields.</summary>
    public event Action<int>? AdjustmentChosen;

    public ParametricCurve()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        Content = _canvas;
        _canvas.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _canvas.SizeChanged += (_, _) => Layout();

        _gap.Opacity = 0.3;
        _heard.StrokeThickness = 2;
        _heard.StrokeLineJoin = PenLineJoin.Round;
        _asked.StrokeThickness = 1.5;
        _asked.StrokeDashArray = new DoubleCollection { 4, 3 };

        // Its colours are set in code, so a new theme rebuilds it.
        ActualThemeChanged += (_, _) =>
        {
            _canvas.Children.Clear();
            Build();
            Layout();
        };

        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += (_, args) =>
        {
            _canvas.ReleasePointerCapture(args.Pointer);
            _dragging = -1;
        };
        _canvas.PointerCaptureLost += (_, _) => _dragging = -1;
        _canvas.PointerCanceled += (_, _) => _dragging = -1;
    }

    /// <summary>Shows a set of adjustments, the band gains they fit to, and which one is chosen.</summary>
    public void Show(IReadOnlyList<Adjustment> adjustments, int[] bands, int selected)
    {
        _adjustments = adjustments;
        _bands = bands;
        _selected = selected;
        Build();
        Layout();
    }

    // -- shapes ------------------------------------------------------------

    private static Brush Themed(string key) => (Brush)Application.Current.Resources[key];

    private void Build()
    {
        if (_canvas.Children.Count == 0)
        {
            _grid.Clear();
            _ticks.Clear();
            _labels.Clear();
            _handles.Clear();

            var faint = Themed("DividerStrokeColorDefaultBrush");
            foreach (int tenths in new[] { Range, Range / 2, -Range / 2, -Range, 0 })
            {
                var line = new Line
                {
                    Stroke = tenths == 0 ? Themed("ControlStrongStrokeColorDefaultBrush") : faint,
                    StrokeThickness = 1,
                    Tag = tenths,
                };
                _grid.Add(line);
                _canvas.Children.Add(line);
            }

            for (int i = 0; i < ParametricEq.Centres.Count; i++)
            {
                var tick = new Line
                {
                    Stroke = faint,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 3 },
                };
                _ticks.Add(tick);
                _canvas.Children.Add(tick);

                var label = new TextBlock
                {
                    Text = i < Frequencies.Count ? Frequencies[i] : "",
                    Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
                };
                _labels.Add(label);
                _canvas.Children.Add(label);
            }

            _gap.Fill = Themed("SystemFillColorCautionBrush");
            _heard.Stroke = Themed("AccentFillColorDefaultBrush");
            _asked.Stroke = Themed("TextFillColorSecondaryBrush");
            _canvas.Children.Add(_gap);
            _canvas.Children.Add(_heard);
            _canvas.Children.Add(_asked);
        }

        while (_handles.Count > _adjustments.Count)
        {
            var (grab, dot, number) = _handles[^1];
            _canvas.Children.Remove(grab);
            _canvas.Children.Remove(dot);
            _canvas.Children.Remove(number);
            _handles.RemoveAt(_handles.Count - 1);
        }
        while (_handles.Count < _adjustments.Count)
        {
            int index = _handles.Count;
            var dot = new Ellipse
            {
                Width = HandleRadius * 2,
                Height = HandleRadius * 2,
                StrokeThickness = 1.5,
                IsHitTestVisible = false,
            };
            var number = new TextBlock
            {
                Text = (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = HandleRadius * 2,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
            };
            var grab = new Ellipse
            {
                Width = GrabRadius * 2,
                Height = GrabRadius * 2,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            };
            grab.PointerPressed += (_, args) => BeginDrag(index, args);
            grab.PointerWheelChanged += (_, args) => Widen(index, args);
            _handles.Add((grab, dot, number));
            _canvas.Children.Add(dot);
            _canvas.Children.Add(number);
            _canvas.Children.Add(grab);
        }

        var accent = Themed("AccentFillColorDefaultBrush");
        var behind = Themed("SettingsCardBackground");
        for (int i = 0; i < _handles.Count; i++)
        {
            bool chosen = i == _selected;
            _handles[i].Dot.Fill = chosen ? accent : behind;
            _handles[i].Dot.Stroke = chosen ? behind : accent;
            _handles[i].Number.Foreground = Themed(
                chosen ? "TextOnAccentFillColorPrimaryBrush" : "AccentTextFillColorPrimaryBrush");
        }
    }

    // -- geometry ----------------------------------------------------------

    private double PlotBottom => Math.Max(Inset, _canvas.ActualHeight - LabelRoom - Inset);

    private double X(double frequency) => ParametricEq.X(frequency, _canvas.ActualWidth);

    private double Y(double decibels) => ResponseCurve.Y(decibels * 10, -Range, Range, Inset, PlotBottom);

    private void Layout()
    {
        double width = _canvas.ActualWidth, height = _canvas.ActualHeight;
        if (width <= 0 || height <= 0 || _bands.Length == 0) return;

        foreach (var line in _grid)
        {
            double y = Y((int)line.Tag! / 10.0);
            (line.X1, line.X2, line.Y1, line.Y2) = (0, width, y, y);
        }

        for (int i = 0; i < _ticks.Count; i++)
        {
            double x = X(ParametricEq.Centres[i]);
            (_ticks[i].X1, _ticks[i].X2, _ticks[i].Y1, _ticks[i].Y2) = (x, x, Inset, PlotBottom);
            _labels[i].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double labelWidth = _labels[i].DesiredSize.Width;
            Canvas.SetLeft(_labels[i], Math.Clamp(x - labelWidth / 2, 0, width - labelWidth));
            Canvas.SetTop(_labels[i], PlotBottom + Inset);
        }

        var asked = new Point[Samples];
        var heard = new Point[Samples];
        for (int k = 0; k < Samples; k++)
        {
            double t = k / (Samples - 1.0);
            double frequency = Adjustment.LowestFrequency
                * Math.Pow((double)Adjustment.HighestFrequency / Adjustment.LowestFrequency, t);
            double x = width * t;
            asked[k] = new Point(x, Y(ParametricEq.Asked(_adjustments, frequency)));
            heard[k] = new Point(x, Y(ParametricEq.Heard(_bands, frequency)));
        }
        _asked.Data = Line(asked);
        _heard.Data = Line(heard);
        _gap.Data = Line(asked.Concat(heard.Reverse()).ToArray(), closed: true);

        for (int i = 0; i < _handles.Count; i++)
        {
            var adjustment = _adjustments[i];
            double x = X(adjustment.Frequency);
            double y = Y(ParametricEq.Asked(_adjustments, adjustment.Frequency));
            var (grab, dot, number) = _handles[i];
            Canvas.SetLeft(dot, x - HandleRadius);
            Canvas.SetTop(dot, y - HandleRadius);
            number.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(number, x - HandleRadius);
            Canvas.SetTop(number, y - number.DesiredSize.Height / 2);
            Canvas.SetLeft(grab, x - GrabRadius);
            Canvas.SetTop(grab, y - GrabRadius);
        }
    }

    private static PathGeometry Line(IReadOnlyList<Point> points, bool closed = false)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = closed, IsFilled = closed };
        var segment = new PolyLineSegment();
        foreach (var point in points.Skip(1)) segment.Points.Add(point);
        figure.Segments.Add(segment);
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    // -- dragging ----------------------------------------------------------

    private void BeginDrag(int index, PointerRoutedEventArgs args)
    {
        _dragging = index;
        _canvas.CapturePointer(args.Pointer);
        args.Handled = true;
        AdjustmentChosen?.Invoke(index);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_dragging < 0 || _dragging >= _adjustments.Count) return;
        var at = args.GetCurrentPoint(_canvas).Position;
        int frequency = ParametricEq.FrequencyAt(at.X, _canvas.ActualWidth);
        // Held to half-decibel steps, as the arrow keys step the fields.
        int gain = (int)Math.Round(
            ResponseCurve.TenthsAt(at.Y, -Range, Range, Inset, PlotBottom) / 5.0) * 5;
        var moved = (_adjustments[_dragging] with { Frequency = frequency, Gain = gain }).Held();
        if (moved != _adjustments[_dragging]) AdjustmentChanged?.Invoke(_dragging, moved);
    }

    private void Widen(int index, PointerRoutedEventArgs args)
    {
        if (index >= _adjustments.Count) return;
        args.Handled = true;
        int delta = args.GetCurrentPoint(_canvas).Properties.MouseWheelDelta;
        var adjustment = _adjustments[index];
        var widened = (adjustment with { Width = adjustment.Width + Math.Sign(delta) }).Held();
        AdjustmentChosen?.Invoke(index);
        if (widened != adjustment) AdjustmentChanged?.Invoke(index, widened);
    }
}
