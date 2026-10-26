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
/// <para>
/// The column under the pointer says it can be dragged: a vertical-resize
/// cursor, and a halo around its point. While it is dragged, or chosen with
/// the keyboard, the point grows and a bubble beside it reads its value. The
/// curve is one tab stop: Left and Right choose a band, Up and Down move it.
/// </para>
/// </remarks>
public sealed class EqualiserCurve : UserControl
{
    /// <summary>Room at top and bottom for a handle sitting at full deflection.</summary>
    private const double Inset = 10;

    private const double HandleRadius = 5.5;
    private const double HaloRadius = 14;
    private const double GhostRadius = 4.5;

    /// <summary>The gap between a point and its bubble.</summary>
    private const double BubbleGap = 14;

    private readonly Canvas _canvas = new() { Background = Brushes.Transparent };
    private readonly Path _area = new() { Classes = { "curvearea" } };
    private readonly Path _curve = new() { Classes = { "curveheard" } };
    private readonly Path _ghost = new() { Classes = { "curvestored" }, StrokeDashArray = [3, 3] };
    private readonly List<(Line Line, int Tenths)> _lines = new();
    private readonly List<TextBlock> _marks = new();
    private readonly List<Rectangle> _grabs = new();
    private readonly List<Ellipse> _halos = new();
    private readonly List<Ellipse> _handles = new();
    private readonly List<Ellipse> _rings = new();
    private readonly TextBlock _bubbleText = new() { Classes = { "curvetonetext" } };
    private readonly Border _bubble = new() { Classes = { "curvetonetag" }, IsHitTestVisible = false, IsVisible = false };

    private int[] _live = [];
    private int[]? _stored;
    private int _dragging = -1;
    private int _hover = -1;
    private int _focused;
    private bool _keyboard;
    private double _pressedAt;
    private int _pressed;

    public int Minimum { get; set; } = -90;
    public int Maximum { get; set; } = 90;
    public IReadOnlyList<string> Frequencies { get; set; } = [];

    /// <summary>Raised as a band is dragged, with its index and new value in tenths of a dB.</summary>
    public event Action<int, int>? BandChanged;

    /// <summary>Raised when a drag begins and when it ends, so the edit made between them can be one step.</summary>
    public event Action? DragStarted, DragEnded;

    /// <summary>Raised when an arrow key moves a band, with its index and new value in tenths of a dB.</summary>
    public event Action<int, int>? BandNudged;

    /// <summary>
    /// Raised when a band's ring is clicked, asking for that band to be put
    /// back where the preset has it.
    /// </summary>
    public event Action<int>? BandReverted;

    public EqualiserCurve()
    {
        Focusable = true;
        IsTabStop = true;
        AutomationProperties.SetName(this, Strings.Get("Equaliser_CurveName"));
        AutomationProperties.SetHelpText(this, Strings.Get("Equaliser_CurveHelp"));

        Content = _canvas;
        _bubble.Child = _bubbleText;
        _canvas.SizeChanged += (_, _) => Plot();
        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += (_, args) => EndDrag(args);
        _canvas.PointerCaptureLost += (_, _) => FinishDrag();
    }

    /// <summary>
    /// Shows a curve and the preset it came from. With no stored values, or
    /// the same values as the live ones, nothing is drawn behind it.
    /// </summary>
    public void Show(int[] live, int[]? stored)
    {
        _live = live;
        _stored = stored is not null && stored.Length == live.Length ? stored : null;
        _focused = Math.Clamp(_focused, 0, Math.Max(0, live.Length - 1));
        Build(live.Length);
        Plot();
    }

    // -- shapes ------------------------------------------------------------

    private void Build(int bands)
    {
        if (_handles.Count == bands && _canvas.Children.Count > 0) return;

        _canvas.Children.Clear();
        _lines.Clear();
        _marks.Clear();
        _grabs.Clear();
        _halos.Clear();
        _handles.Clear();
        _rings.Clear();

        // Four gridlines and the 0 dB line, which is drawn last of the five
        // so it reads as the one that matters.
        foreach (int tenths in new[] { Maximum, Maximum / 2, Minimum / 2, Minimum, 0 })
        {
            var line = new Line { Classes = { tenths == 0 ? "curvecentre" : "curvegrid" } };
            _lines.Add((line, tenths));
            _canvas.Children.Add(line);

            // The scale, quiet, on the left edge beside its line. The curve is
            // drawn over it and a screen reader reads the fields instead.
            var mark = new TextBlock { Text = Db.Axis(tenths), Classes = { "caption", "tertiary" }, IsHitTestVisible = false };
            AutomationProperties.SetAccessibilityView(mark, AccessibilityView.Raw);
            _marks.Add(mark);
            _canvas.Children.Add(mark);
        }

        _canvas.Children.Add(_area);
        _canvas.Children.Add(_ghost);
        _canvas.Children.Add(_curve);

        for (int i = 0; i < bands; i++)
        {
            int index = i;

            var grab = new Rectangle { Fill = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.SizeNorthSouth) };
            grab.PointerPressed += (_, args) => BeginDrag(index, args);
            grab.PointerEntered += (_, _) => Hover(index);
            grab.PointerExited += (_, _) => Hover(-1);
            _grabs.Add(grab);
            _canvas.Children.Add(grab);

            var halo = new Ellipse
            {
                Width = HaloRadius * 2,
                Height = HaloRadius * 2,
                Classes = { "bandhalo" },
                IsHitTestVisible = false,
                IsVisible = false,
            };
            _halos.Add(halo);
            _canvas.Children.Add(halo);

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
                Cursor = new Cursor(StandardCursorType.Hand),
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
        _canvas.Children.Add(_bubble);
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

        for (int i = 0; i < _lines.Count; i++)
        {
            var (line, tenths) = _lines[i];
            double y = Y(tenths);
            line.StartPoint = new Point(0, y);
            line.EndPoint = new Point(width, y);

            // Above its line, except the top one, which would leave the plot.
            _marks[i].Measure(Size.Infinity);
            Canvas.SetLeft(_marks[i], 2);
            Canvas.SetTop(_marks[i], tenths == Maximum ? y + 1 : y - _marks[i].DesiredSize.Height);
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
            Canvas.SetLeft(_halos[i], points[i].X - HaloRadius);
            Canvas.SetTop(_halos[i], points[i].Y - HaloRadius);

            bool moved = edited && _stored![i] != _live[i];
            _rings[i].IsVisible = moved;
            if (!moved) continue;
            double ringY = Y(_stored![i]);
            Canvas.SetLeft(_rings[i], points[i].X - GhostRadius);
            Canvas.SetTop(_rings[i], ringY - GhostRadius);
            ToolTip.SetTip(_rings[i], Strings.Format("Equaliser_PutBack", Db.Text(_stored[i])));
        }
        Highlight();
    }

    // -- showing which point can be moved ------------------------------------

    private void Hover(int index)
    {
        // The pointer is captured while dragging, and leaving the column then is not leaving the point.
        if (_dragging >= 0) return;
        _hover = index;
        Highlight();
    }

    /// <summary>
    /// Gives the point being pointed at, dragged or chosen by the keyboard its
    /// halo and its size, and a bubble with its value while it is being moved.
    /// </summary>
    private void Highlight()
    {
        int moving = _dragging >= 0 ? _dragging : _keyboard && IsKeyboardFocusWithin ? _focused : -1;
        int lit = moving >= 0 ? moving : _hover;
        for (int i = 0; i < _handles.Count; i++)
        {
            _handles[i].Classes.Set("active", i == lit);
            _halos[i].IsVisible = i == lit;
            _halos[i].Classes.Set("focus", i == moving && _dragging < 0);
        }

        bool bubble = moving >= 0 && moving < _live.Length && _canvas.Bounds.Width > 0;
        _bubble.IsVisible = bubble;
        if (!bubble) return;

        _bubbleText.Text = Strings.Format("Parametric_Decibels", Db.Text(_live[moving]));
        _bubble.Measure(Size.Infinity);
        double x = X(moving), y = Y(_live[moving]);
        double size = _bubble.DesiredSize.Width;
        // Beside the point, on the side with room.
        double left = x + BubbleGap + size <= _canvas.Bounds.Width ? x + BubbleGap : x - BubbleGap - size;
        Canvas.SetLeft(_bubble, Math.Max(0, left));
        Canvas.SetTop(_bubble, Math.Clamp(y - _bubble.DesiredSize.Height / 2, 0, Math.Max(0, _canvas.Bounds.Height - _bubble.DesiredSize.Height)));
    }

    // -- the keyboard ------------------------------------------------------

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.Source != this) return;
        _keyboard = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        Highlight();
        Announce();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboard = false;
        Highlight();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_live.Length == 0 || e.Source != this) return;

        int step = ResponseCurve.KeyStep(e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        switch (e.Key)
        {
            case Key.Left: _focused = Math.Max(0, _focused - 1); break;
            case Key.Right: _focused = Math.Min(_live.Length - 1, _focused + 1); break;
            case Key.Home: _focused = 0; break;
            case Key.End: _focused = _live.Length - 1; break;
            case Key.Up or Key.Down:
                int tenths = Math.Clamp(_live[_focused] + (e.Key == Key.Up ? step : -step), Minimum, Maximum);
                if (tenths != _live[_focused]) BandNudged?.Invoke(_focused, tenths);
                break;
            default: return;
        }
        e.Handled = true;
        _keyboard = true;
        Highlight();
        Announce();
    }

    /// <summary>Says which band is chosen and where it is, to a screen reader.</summary>
    private void Announce() => AutomationProperties.SetItemStatus(this,
        _focused < Frequencies.Count && _focused < _live.Length
            ? Strings.Format("Equaliser_Band", Frequencies[_focused], Db.Text(_live[_focused]))
            : "");

    // -- dragging ----------------------------------------------------------

    private void BeginDrag(int index, PointerPressedEventArgs args)
    {
        _dragging = index;
        _focused = index;
        _keyboard = false;
        _pressedAt = args.GetPosition(_canvas).Y;
        _pressed = index < _live.Length ? _live[index] : 0;
        Focus(NavigationMethod.Pointer);
        args.Pointer.Capture(_canvas);
        args.Handled = true;
        Highlight();
        DragStarted?.Invoke();
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
        FinishDrag();
    }

    private void FinishDrag()
    {
        if (_dragging < 0) return;
        _dragging = -1;
        Highlight();
        DragEnded?.Invoke();
    }
}
