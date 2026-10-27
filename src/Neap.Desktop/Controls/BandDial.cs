using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

namespace Neap.Desktop.Controls;

/// <summary>
/// A dial for one value of a parametric band: frequency, width or gain, turned by
/// dragging up and down, by the wheel or by the keys, and reset by a double press.
/// </summary>
/// <remarks>
/// <para>
/// It is a range control in the value's own units, so a screen reader hears
/// "1000", not a position on the dial. A logarithmic dial (frequency and width)
/// moves by a ratio and a linear one (gain) by a step, with the keys, the wheel and the
/// drag alike. Shift makes each of them fine.
/// </para>
/// <para>
/// Its look is drawn here and takes its colours from the theme's brushes, so a
/// change of theme needs no redraw.
/// </para>
/// </remarks>
public sealed class BandDial : RangeBase
{
    private const double Size = 64;
    private const double Centre = Size / 2;
    private const double Radius = 25;
    private const double StartAngle = 135;
    private const double SweepAngle = 270;

    /// <summary>The share of the dial's range one pixel of dragging moves it.</summary>
    private const double PerPixel = 0.005;

    /// <summary>How much one tick of the wheel moves it, as a share of the range.</summary>
    private const double PerWheelTick = 0.02;

    /// <summary>How much finer Shift makes every movement.</summary>
    private const double Fine = 0.2;

    /// <summary>Whether the dial moves by ratio, as frequency and width do, and not by step.</summary>
    public static readonly StyledProperty<bool> LogarithmicProperty =
        AvaloniaProperty.Register<BandDial, bool>(nameof(Logarithmic));

    /// <summary>The value the arc is drawn from: the dial's lowest by default, zero for a gain.</summary>
    public static readonly StyledProperty<double?> OriginProperty =
        AvaloniaProperty.Register<BandDial, double?>(nameof(Origin));

    /// <summary>The value a double press and Home put it back to.</summary>
    public static readonly StyledProperty<double> DefaultValueProperty =
        AvaloniaProperty.Register<BandDial, double>(nameof(DefaultValue));

    private Path? _valueArc;
    private Line? _pointer;
    private Ellipse? _focusRing;
    private bool _dragging;
    private double _dragFromY;
    private double _dragFromShare;

    public BandDial()
    {
        Focusable = true;
        Width = Size;
        Height = Size;
        SmallChange = 1;
        LargeChange = 5;
        Template = new FuncControlTemplate<BandDial>((dial, _) => dial.Build());
    }

    /// <summary>Raised at the start of each movement, so a screen can keep what it was before.</summary>
    public event Action? Changing;

    /// <inheritdoc cref="LogarithmicProperty"/>
    public bool Logarithmic
    {
        get => GetValue(LogarithmicProperty);
        set => SetValue(LogarithmicProperty, value);
    }

    /// <inheritdoc cref="OriginProperty"/>
    public double? Origin
    {
        get => GetValue(OriginProperty);
        set => SetValue(OriginProperty, value);
    }

    /// <inheritdoc cref="DefaultValueProperty"/>
    public double DefaultValue
    {
        get => GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    /// <summary>The ratio one key press moves a logarithmic dial by.</summary>
    public double Ratio { get; set; } = 1.1;

    protected override Type StyleKeyOverride => typeof(BandDial);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MinimumProperty || change.Property == MaximumProperty
            || change.Property == OriginProperty)
        {
            Draw();
        }
    }

    private Canvas Build()
    {
        var canvas = new Canvas { Width = Size, Height = Size, Background = Brushes.Transparent };
        var track = new Path { Data = Arc(StartAngle, StartAngle + SweepAngle), StrokeThickness = 5, StrokeLineCap = PenLineCap.Round };
        track.Bind(Shape.StrokeProperty, track.GetResourceObservable("NeapDialTrackBrush"));
        _valueArc = new Path { StrokeThickness = 5, StrokeLineCap = PenLineCap.Round };
        _valueArc.Bind(Shape.StrokeProperty, _valueArc.GetResourceObservable("NeapAccentFillBrush"));
        var knob = new Ellipse { Width = 36, Height = 36, StrokeThickness = 1 };
        knob.Bind(Shape.FillProperty, knob.GetResourceObservable("NeapKnobBrush"));
        knob.Bind(Shape.StrokeProperty, knob.GetResourceObservable("NeapKnobEdgeBrush"));
        Canvas.SetLeft(knob, Centre - 18);
        Canvas.SetTop(knob, Centre - 18);
        _pointer = new Line { StrokeThickness = 3, StrokeLineCap = PenLineCap.Round };
        _pointer.Bind(Shape.StrokeProperty, _pointer.GetResourceObservable("NeapTextBrush"));
        _focusRing = new Ellipse { Width = Size - 2, Height = Size - 2, StrokeThickness = 2, IsVisible = false };
        _focusRing.Bind(Shape.StrokeProperty, _focusRing.GetResourceObservable("NeapAccentFillBrush"));
        Canvas.SetLeft(_focusRing, 1);
        Canvas.SetTop(_focusRing, 1);
        canvas.Children.AddRange([track, _valueArc, knob, _pointer, _focusRing]);
        Draw();
        return canvas;
    }

    /// <summary>The dial's position as a share of its range, 0 to 1.</summary>
    private double Share(double value)
    {
        if (Maximum <= Minimum) return 0;
        double share = Logarithmic
            ? (Math.Log(Math.Max(value, Minimum)) - Math.Log(Minimum)) / (Math.Log(Maximum) - Math.Log(Minimum))
            : (value - Minimum) / (Maximum - Minimum);
        return Math.Clamp(share, 0, 1);
    }

    private double ValueAt(double share) => Math.Clamp(
        Logarithmic
            ? Math.Exp(Math.Log(Minimum) + (share * (Math.Log(Maximum) - Math.Log(Minimum))))
            : Minimum + (share * (Maximum - Minimum)),
        Minimum, Maximum);

    private static Point PointOn(double degrees, double radius)
    {
        double radians = degrees * Math.PI / 180;
        return new Point(Centre + (radius * Math.Cos(radians)), Centre + (radius * Math.Sin(radians)));
    }

    private static PathGeometry Arc(double from, double to)
    {
        var figure = new PathFigure { StartPoint = PointOn(from, Radius), IsClosed = false, IsFilled = false };
        figure.Segments!.Add(new ArcSegment
        {
            Point = PointOn(to, Radius),
            Size = new Size(Radius, Radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = to - from > 180,
        });
        return new PathGeometry { Figures = [figure] };
    }

    private void Draw()
    {
        if (_valueArc is null || _pointer is null) return;
        double at = StartAngle + (Share(Value) * SweepAngle);
        double from = StartAngle + (Share(Origin ?? Minimum) * SweepAngle);
        double low = Math.Min(from, at), high = Math.Max(from, at);
        _valueArc.IsVisible = high - low > 0.5;
        if (_valueArc.IsVisible) _valueArc.Data = Arc(low, high);
        _pointer.StartPoint = PointOn(at, 9);
        _pointer.EndPoint = PointOn(at, 16);
    }

    // -- the pointer -------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2)
        {
            Changing?.Invoke();
            Value = DefaultValue;
            e.Handled = true;
            return;
        }

        Changing?.Invoke();
        _dragging = true;
        _dragFromY = point.Position.Y;
        _dragFromShare = Share(Value);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        double scale = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? Fine : 1;
        Value = ValueAt(_dragFromShare + ((_dragFromY - e.GetPosition(this).Y) * PerPixel * scale));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Delta.Y == 0) return;
        Changing?.Invoke();
        double scale = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? Fine : 1;
        Value = ValueAt(Share(Value) + (Math.Sign(e.Delta.Y) * PerWheelTick * scale));
        e.Handled = true;
    }

    // -- the keyboard ------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int steps = e.Key switch
        {
            Key.Up or Key.Right => 1,
            Key.Down or Key.Left => -1,
            Key.PageUp => 5,
            Key.PageDown => -5,
            _ => 0,
        };
        if (e.Key == Key.Home)
        {
            Changing?.Invoke();
            Value = DefaultValue;
        }
        else if (e.Key == Key.End || steps == 0)
        {
            base.OnKeyDown(e);
            return;
        }
        else
        {
            Changing?.Invoke();
            double fine = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? Fine : 1;
            Value = Logarithmic
                ? Math.Clamp(Value * Math.Pow(Ratio, steps * fine), Minimum, Maximum)
                : Math.Clamp(Value + (steps * SmallChange * fine), Minimum, Maximum);
        }

        e.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_focusRing is not null) _focusRing.IsVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_focusRing is not null) _focusRing.IsVisible = false;
    }
}
