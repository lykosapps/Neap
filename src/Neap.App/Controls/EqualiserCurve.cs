using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Neap.App.Services;
using Neap.Core.Presets;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Neap.App.Controls;

/// <summary>Formats a band value, held everywhere in tenths of a decibel, as signed decibels.</summary>
internal static class Db
{
    public static string Text(int tenths)
    {
        string sign = tenths > 0 ? "+" : tenths < 0 ? "−" : "";
        return $"{sign}{Math.Abs(tenths / 10.0).ToString("0.0", CultureInfo.InvariantCulture)}";
    }
}

/// <summary>
/// Draws the equaliser response as a curve, with a draggable point per band.
/// </summary>
/// <remarks>
/// <para>
/// Where the live curve differs from the stored preset, the preset is drawn
/// behind it as a dashed line, with a ring on each moved band. The ring shows
/// how far and which way the band has moved, and clicking it puts that band
/// back where the preset has it.
/// </para>
/// <para>
/// The curve never overshoots: see <see cref="ResponseCurve"/>.
/// </para>
/// <para>
/// A press anywhere in a band's column grabs that band, so a point does not
/// have to be hit precisely. The value moves only once the pointer does, so a
/// stray click changes nothing.
/// </para>
/// </remarks>
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

    /// <summary>Raised when a band is dragged, with its index and new value in tenths of a dB.</summary>
    public event Action<int, int>? BandChanged;

    /// <summary>
    /// Raised when a band's ring is clicked, asking for that band to be put
    /// back where the preset has it.
    /// </summary>
    public event Action<int>? BandReverted;

    public EqualiserCurve()
    {
        // The curve pictures the fields below it. A screen reader reads the
        // bands once, from those fields, which are named by frequency.
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

        // Its colours are set in code, so a new theme redraws it.
        ActualThemeChanged += (_, _) =>
        {
            _handles.Clear();
            Build(_live.Length);
            Layout();
        };

        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += (_, args) => EndDrag(args);
        _canvas.PointerCaptureLost += (_, _) => _dragging = -1;
        _canvas.PointerCanceled += (_, _) => _dragging = -1;
    }

    /// <summary>
    /// Shows a curve and the preset it came from. With no stored values, or
    /// the same values as the live ones, nothing is drawn behind it.
    /// </summary>
    public void Show(int[] live, int[]? stored)
    {
        _live = live;
        _stored = stored is not null && stored.Length == live.Length ? stored : null;
        Build(live.Length);
        Layout();
    }

    // -- shapes ------------------------------------------------------------

    private static Brush Themed(string key) => (Brush)Application.Current.Resources[key];

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
        var behind = Themed("SettingsCardBackground");

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

    private static double PlotTop => Inset;
    private double PlotBottom => Math.Max(Inset, _canvas.ActualHeight - Inset);

    private double X(int index) => ResponseCurve.X(index, _live.Length, _canvas.ActualWidth);

    private double Y(double tenths) => ResponseCurve.Y(tenths, Minimum, Maximum, PlotTop, PlotBottom);

    private int TenthsAt(double y) => ResponseCurve.TenthsAt(y, Minimum, Maximum, PlotTop, PlotBottom);

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
        _curve.Data = CurveGeometry.Through(points);
        _area.Data = CurveGeometry.Through(points, closeAt: Y(0));

        bool edited = _stored is not null && !_stored.SequenceEqual(_live);
        _ghost.Visibility = edited ? Visibility.Visible : Visibility.Collapsed;
        if (edited)
            _ghost.Data = CurveGeometry.Through(_stored!.Select((v, i) => new Point(X(i), Y(v))).ToArray());

        double column = width / _live.Length;
        for (int i = 0; i < _live.Length; i++)
        {
            _grabs[i].Width = column;
            _grabs[i].Height = height;
            Canvas.SetLeft(_grabs[i], i * column);
            Canvas.SetTop(_grabs[i], 0);
            ToolTipService.SetToolTip(_grabs[i],
                i < Frequencies.Count ? Strings.Format("Equaliser_Band", Frequencies[i], Db.Text(_live[i])) : null);

            Canvas.SetLeft(_handles[i], points[i].X - HandleRadius);
            Canvas.SetTop(_handles[i], points[i].Y - HandleRadius);

            bool moved = edited && _stored![i] != _live[i];
            _rings[i].Visibility = moved ? Visibility.Visible : Visibility.Collapsed;
            if (!moved) continue;
            double ringY = Y(_stored![i]);
            Canvas.SetLeft(_rings[i], points[i].X - GhostRadius);
            Canvas.SetTop(_rings[i], ringY - GhostRadius);
            ToolTipService.SetToolTip(_rings[i], Strings.Format("Equaliser_BackTo", Db.Text(_stored[i])));
        }
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
