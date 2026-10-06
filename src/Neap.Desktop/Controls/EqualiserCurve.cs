using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Neap.Core.Presets;
using Path = Avalonia.Controls.Shapes.Path;

namespace Neap.Desktop.Controls;

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
/// have to be hit precisely. A drag moves it by the distance the pointer
/// goes, from where it was, so a click or a twitch changes nothing; see
/// <see cref="ResponseCurve.Dragged"/>.
/// </para>
/// </remarks>
public sealed class EqualiserCurve : UserControl
{
    /// <summary>Room at top and bottom for a handle sitting at full deflection.</summary>
    private const double Inset = 10;

    private const double HandleRadius = 5.5;
    private const double GhostRadius = 4.5;

    private readonly Canvas _canvas = new() { Background = Brushes.Transparent };
    private readonly Path _area = new() { Classes = { "curvearea" } };
    private readonly Path _curve = new() { Classes = { "curveheard" } };
    private readonly Path _ghost = new() { Classes = { "curvestored" }, StrokeDashArray = [3, 3] };
    private readonly List<(Line Line, int Tenths)> _lines = new();
    private readonly List<Rectangle> _grabs = new();
    private readonly List<Ellipse> _handles = new();
    private readonly List<Ellipse> _rings = new();

    private int[] _live = [];
    private int[]? _stored;
    private int _dragging = -1;
    private double _pressedAt;
    private int _pressed;

    public int Minimum { get; set; } = -90;
    public int Maximum { get; set; } = 90;
    public IReadOnlyList<string> Frequencies { get; set; } = [];

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
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);

        Content = _canvas;
        _canvas.SizeChanged += (_, _) => Plot();
        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += (_, args) => EndDrag(args);
        _canvas.PointerCaptureLost += (_, _) => _dragging = -1;
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
        Plot();
    }

    // -- shapes ------------------------------------------------------------

    private void Build(int bands)
    {
        if (_handles.Count == bands && _canvas.Children.Count > 0) return;

        _canvas.Children.Clear();
        _lines.Clear();
        _grabs.Clear();
        _handles.Clear();
        _rings.Clear();

        // Four gridlines and the 0 dB line, which is drawn last of the five
        // so it reads as the one that matters.
        foreach (int tenths in new[] { Maximum, Maximum / 2, Minimum / 2, Minimum, 0 })
        {
            var line = new Line { Classes = { tenths == 0 ? "curvecentre" : "curvegrid" } };
            _lines.Add((line, tenths));
            _canvas.Children.Add(line);
        }

        _canvas.Children.Add(_area);
        _canvas.Children.Add(_ghost);
        _canvas.Children.Add(_curve);

        for (int i = 0; i < bands; i++)
        {
            int index = i;

            var grab = new Rectangle { Fill = Brushes.Transparent };
            grab.PointerPressed += (_, args) => BeginDrag(index, args);
            _grabs.Add(grab);
            _canvas.Children.Add(grab);

            var handle = new Ellipse
            {
                Width = HandleRadius * 2,
                Height = HandleRadius * 2,
                Classes = { "bandhandle" },
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
                Classes = { "curvering" },
                IsVisible = false,
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
    private double PlotBottom => Math.Max(Inset, _canvas.Bounds.Height - Inset);

    private double X(int index) => ResponseCurve.X(index, _live.Length, _canvas.Bounds.Width);

    private double Y(double tenths) => ResponseCurve.Y(tenths, Minimum, Maximum, PlotTop, PlotBottom);

    private void Plot()
    {
        double width = _canvas.Bounds.Width, height = _canvas.Bounds.Height;
        if (width <= 0 || height <= 0 || _live.Length == 0) return;

        foreach (var (line, tenths) in _lines)
        {
            double y = Y(tenths);
            line.StartPoint = new Point(0, y);
            line.EndPoint = new Point(width, y);
        }

        var points = _live.Select((v, i) => new Point(X(i), Y(v))).ToArray();
        _curve.Data = CurveGeometry.Through(points);
        _area.Data = CurveGeometry.Through(points, closeAt: Y(0));

        bool edited = _stored is not null && !_stored.SequenceEqual(_live);
        _ghost.IsVisible = edited;
        if (edited)
            _ghost.Data = CurveGeometry.Through(_stored!.Select((v, i) => new Point(X(i), Y(v))).ToArray());

        double column = width / _live.Length;
        for (int i = 0; i < _live.Length; i++)
        {
            _grabs[i].Width = column;
            _grabs[i].Height = height;
            Canvas.SetLeft(_grabs[i], i * column);
            Canvas.SetTop(_grabs[i], 0);
            ToolTip.SetTip(_grabs[i],
                i < Frequencies.Count ? Strings.Format("Equaliser_Band", Frequencies[i], Db.Text(_live[i])) : null);

            Canvas.SetLeft(_handles[i], points[i].X - HandleRadius);
            Canvas.SetTop(_handles[i], points[i].Y - HandleRadius);

            bool moved = edited && _stored![i] != _live[i];
            _rings[i].IsVisible = moved;
            if (!moved) continue;
            double ringY = Y(_stored![i]);
            Canvas.SetLeft(_rings[i], points[i].X - GhostRadius);
            Canvas.SetTop(_rings[i], ringY - GhostRadius);
            ToolTip.SetTip(_rings[i], Strings.Format("Equaliser_BackTo", Db.Text(_stored[i])));
        }
    }

    // -- dragging ----------------------------------------------------------

    private void BeginDrag(int index, PointerPressedEventArgs args)
    {
        _dragging = index;
        _pressedAt = args.GetPosition(_canvas).Y;
        _pressed = index < _live.Length ? _live[index] : 0;
        args.Pointer.Capture(_canvas);
        args.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (_dragging < 0 || _dragging >= _live.Length) return;
        double down = args.GetPosition(_canvas).Y - _pressedAt;
        int tenths = ResponseCurve.Dragged(_pressed, down, Minimum, Maximum, PlotBottom - PlotTop);
        if (_live[_dragging] == tenths) return;
        BandChanged?.Invoke(_dragging, tenths);
    }

    private void EndDrag(PointerReleasedEventArgs args)
    {
        args.Pointer.Capture(null);
        _dragging = -1;
    }
}
