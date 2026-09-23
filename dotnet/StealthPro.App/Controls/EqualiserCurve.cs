using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace StealthPro.App.Controls;

/// <summary>Band values are tenths of a decibel everywhere; this writes them.</summary>
internal static class Db
{
    public static string Text(int tenths)
    {
        string sign = tenths > 0 ? "+" : tenths < 0 ? "−" : "";
        return $"{sign}{Math.Abs(tenths / 10.0).ToString("0.0")}";
    }
}

/// <summary>
/// The equaliser as a curve rather than ten separate sliders.
///
/// An equaliser means a shape, and ten sliders do not show one — you had to
/// read ten numbers to see that the mids were scooped. This draws the
/// response instead, with a point per band you can drag.
///
/// <b>The stored preset is drawn behind the live one.</b> Only where the two
/// differ, as a dashed line and a ring on each moved band. That ring is also
/// the way back: clicking it puts that one band where the preset has it.
/// It replaces ten revert buttons that previously held a row of layout
/// whether they were needed or not, and it says something they could not —
/// <i>how far</i> the band has moved, and in which direction.
///
/// <b>The curve never overshoots.</b> Smoothing is monotone cubic
/// (Fritsch–Carlson), so a curve through the points cannot bulge past a
/// neighbouring band's value and invent a boost that is not set. An ordinary
/// Catmull-Rom spline does exactly that, which on an equaliser is not a
/// cosmetic difference: it draws gain that the headset is not applying.
///
/// A press anywhere in a band's column grabs that band, so a point does not
/// have to be hit precisely — but the value only moves once the pointer
/// does, so a stray click changes nothing.
/// </summary>
public sealed class EqualiserCurve : UserControl
{
    /// <summary>Room at top and bottom for a handle sitting at full deflection.</summary>
    private const double Inset = 10;

    private const double HandleRadius = 5.5;
    private const double GhostRadius = 4.5;

    private readonly Canvas _canvas = new();
    private readonly Path _area = new();
    private readonly Path _curve = new();
    private readonly Path _ghost = new();
    private readonly List<Line> _grid = new();
    private readonly List<Rectangle> _grabs = new();
    private readonly List<Ellipse> _handles = new();
    private readonly List<Ellipse> _rings = new();

    private int[] _live = Array.Empty<int>();
    private int[]? _stored;
    private int _dragging = -1;

    public int Minimum { get; set; } = -90;
    public int Maximum { get; set; } = 90;
    public IReadOnlyList<string> Frequencies { get; set; } = Array.Empty<string>();

    /// <summary>A band was dragged. Index, and its new value in tenths of a dB.</summary>
    public event Action<int, int>? BandChanged;

    /// <summary>A band's ring was clicked: put it back where the preset has it.</summary>
    public event Action<int>? BandReverted;

    public EqualiserCurve()
    {
        // The curve is a picture of the numbers below it, not a second copy
        // of them. A screen reader should read the bands once, from the
        // fields that are named after their frequencies.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAccessibilityView(
            this, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);

        Content = _canvas;
        _canvas.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _canvas.SizeChanged += (_, _) => Layout();

        _area.Opacity = 0.14;
        _curve.StrokeThickness = 2;
        _curve.StrokeLineJoin = PenLineJoin.Round;
        _ghost.StrokeThickness = 1;
        _ghost.StrokeDashArray = new DoubleCollection { 3, 3 };

        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += (_, args) => EndDrag(args);
        _canvas.PointerCaptureLost += (_, _) => _dragging = -1;
        _canvas.PointerCanceled += (_, _) => _dragging = -1;
    }

    /// <summary>
    /// Show a curve, and the preset it came from. Pass no baseline, or the
    /// same values, and nothing is drawn behind it.
    /// </summary>
    public void Show(int[] live, int[]? stored)
    {
        _live = live;
        _stored = stored is not null && stored.Length == live.Length ? stored : null;
        Build(live.Length);
        Layout();
    }

    // -- shapes ------------------------------------------------------------

    private Brush Themed(string key) => (Brush)Application.Current.Resources[key];

    private void Build(int bands)
    {
        if (_handles.Count == bands && _canvas.Children.Count > 0) return;

        _canvas.Children.Clear();
        _grid.Clear();
        _grabs.Clear();
        _handles.Clear();
        _rings.Clear();

        var faint = Themed("DividerStrokeColorDefaultBrush");
        var centre = Themed("ControlStrongStrokeColorDefaultBrush");
        var accent = Themed("AccentFillColorDefaultBrush");
        var ghost = Themed("TextFillColorTertiaryBrush");
        var behind = Themed("CardBackgroundFillColorDefaultBrush");

        // Four gridlines and the 0 dB line, which is drawn last of the five
        // so it reads as the one that matters.
        foreach (int tenths in new[] { Maximum, Maximum / 2, Minimum / 2, Minimum, 0 })
        {
            var line = new Line
            {
                Stroke = tenths == 0 ? centre : faint,
                StrokeThickness = 1,
                Tag = tenths,
            };
            _grid.Add(line);
            _canvas.Children.Add(line);
        }

        _area.Fill = accent;
        _curve.Stroke = accent;
        _ghost.Stroke = ghost;
        _canvas.Children.Add(_area);
        _canvas.Children.Add(_ghost);
        _canvas.Children.Add(_curve);

        for (int i = 0; i < bands; i++)
        {
            int index = i;

            var grab = new Rectangle { Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
            grab.PointerPressed += (sender, args) => BeginDrag(index, args);
            _grabs.Add(grab);
            _canvas.Children.Add(grab);

            var handle = new Ellipse
            {
                Width = HandleRadius * 2,
                Height = HandleRadius * 2,
                Fill = accent,
                Stroke = behind,
                StrokeThickness = 2,
                IsHitTestVisible = false,
            };
            _handles.Add(handle);
            _canvas.Children.Add(handle);
        }

        // Rings last: they sit over the grab areas so a click on one reverts
        // that band rather than starting a drag.
        for (int i = 0; i < bands; i++)
        {
            int index = i;
            var ring = new Ellipse
            {
                Width = GhostRadius * 2,
                Height = GhostRadius * 2,
                Fill = behind,
                Stroke = ghost,
                StrokeThickness = 1.5,
                Visibility = Visibility.Collapsed,
            };
            ring.PointerPressed += (_, args) =>
            {
                args.Handled = true;
                BandReverted?.Invoke(index);
            };
            _rings.Add(ring);
            _canvas.Children.Add(ring);
        }
    }

    // -- geometry ----------------------------------------------------------

    private double PlotTop => Inset;
    private double PlotBottom => Math.Max(Inset, _canvas.ActualHeight - Inset);

    private double X(int index) =>
        _live.Length == 0 ? 0 : (index + 0.5) * _canvas.ActualWidth / _live.Length;

    private double Y(double tenths)
    {
        double span = Maximum - Minimum;
        double t = span <= 0 ? 0.5 : (Maximum - tenths) / span;
        return PlotTop + t * (PlotBottom - PlotTop);
    }

    private int TenthsAt(double y)
    {
        double height = PlotBottom - PlotTop;
        double t = height <= 0 ? 0 : (y - PlotTop) / height;
        return (int)Math.Round(Math.Clamp(Maximum - t * (Maximum - Minimum), Minimum, Maximum));
    }

    private void Layout()
    {
        double width = _canvas.ActualWidth, height = _canvas.ActualHeight;
        if (width <= 0 || height <= 0 || _live.Length == 0) return;

        foreach (var line in _grid)
        {
            double y = Y((int)line.Tag!);
            line.X1 = 0;
            line.X2 = width;
            line.Y1 = y;
            line.Y2 = y;
        }

        var points = _live.Select((v, i) => new Point(X(i), Y(v))).ToArray();
        _curve.Data = Through(points, close: false);
        _area.Data = Through(points, close: true);

        bool edited = _stored is not null && !_stored.SequenceEqual(_live);
        _ghost.Visibility = edited ? Visibility.Visible : Visibility.Collapsed;
        if (edited)
            _ghost.Data = Through(_stored!.Select((v, i) => new Point(X(i), Y(v))).ToArray(), close: false);

        double column = width / _live.Length;
        for (int i = 0; i < _live.Length; i++)
        {
            _grabs[i].Width = column;
            _grabs[i].Height = height;
            Canvas.SetLeft(_grabs[i], i * column);
            Canvas.SetTop(_grabs[i], 0);
            ToolTipService.SetToolTip(_grabs[i],
                i < Frequencies.Count ? $"{Frequencies[i]}   {Db.Text(_live[i])} dB" : null);

            Canvas.SetLeft(_handles[i], points[i].X - HandleRadius);
            Canvas.SetTop(_handles[i], points[i].Y - HandleRadius);

            bool moved = edited && _stored![i] != _live[i];
            _rings[i].Visibility = moved ? Visibility.Visible : Visibility.Collapsed;
            if (!moved) continue;
            double ringY = Y(_stored![i]);
            Canvas.SetLeft(_rings[i], points[i].X - GhostRadius);
            Canvas.SetTop(_rings[i], ringY - GhostRadius);
            ToolTipService.SetToolTip(_rings[i], $"Back to {Db.Text(_stored[i])} dB");
        }
    }

    /// <summary>
    /// A smooth path through every point, and through no value between them
    /// that is not there. See the class remarks on overshoot.
    /// </summary>
    private PathGeometry Through(Point[] points, bool close)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = close, IsFilled = close };
        var slopes = Slopes(points);

        for (int i = 0; i < points.Length - 1; i++)
        {
            double run = (points[i + 1].X - points[i].X) / 3;
            figure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(points[i].X + run, points[i].Y + slopes[i] * run),
                Point2 = new Point(points[i + 1].X - run, points[i + 1].Y - slopes[i + 1] * run),
                Point3 = points[i + 1],
            });
        }

        if (close)
        {
            double zero = Y(0);
            figure.Segments.Add(new LineSegment { Point = new Point(points[^1].X, zero) });
            figure.Segments.Add(new LineSegment { Point = new Point(points[0].X, zero) });
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    /// <summary>Fritsch–Carlson tangents: smooth, and monotone between points.</summary>
    private static double[] Slopes(Point[] points)
    {
        int n = points.Length;
        var slope = new double[n];
        if (n < 2) return slope;

        var secant = new double[n - 1];
        for (int i = 0; i < n - 1; i++)
        {
            double run = points[i + 1].X - points[i].X;
            secant[i] = run == 0 ? 0 : (points[i + 1].Y - points[i].Y) / run;
        }

        slope[0] = secant[0];
        slope[n - 1] = secant[n - 2];
        for (int i = 1; i < n - 1; i++)
            slope[i] = secant[i - 1] * secant[i] <= 0 ? 0 : (secant[i - 1] + secant[i]) / 2;

        for (int i = 0; i < n - 1; i++)
        {
            if (secant[i] == 0) { slope[i] = 0; slope[i + 1] = 0; continue; }
            double a = slope[i] / secant[i], b = slope[i + 1] / secant[i];
            double size = a * a + b * b;
            if (size <= 9) continue;
            double scale = 3 / Math.Sqrt(size);
            slope[i] = scale * a * secant[i];
            slope[i + 1] = scale * b * secant[i];
        }
        return slope;
    }

    // -- dragging ----------------------------------------------------------

    private void BeginDrag(int index, PointerRoutedEventArgs args)
    {
        _dragging = index;
        _canvas.CapturePointer(args.Pointer);
        args.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_dragging < 0) return;
        int tenths = TenthsAt(args.GetCurrentPoint(_canvas).Position.Y);
        if (_dragging < _live.Length && _live[_dragging] == tenths) return;
        BandChanged?.Invoke(_dragging, tenths);
    }

    private void EndDrag(PointerRoutedEventArgs args)
    {
        _canvas.ReleasePointerCapture(args.Pointer);
        _dragging = -1;
    }
}
