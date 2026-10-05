using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Neap.Core.Mix;
using Path = Avalonia.Controls.Shapes.Path;

namespace Neap.Desktop.Controls;

/// <summary>
/// The game and chat mix as a hardware knob: a ridged knob with a pointer,
/// inside a 270° ring of segments that light game from the left and chat
/// from the right.
/// </summary>
/// <remarks>
/// <para>
/// It is a range control, from 0 (game only) to 100 (chat only), so a screen
/// reader announces it as a slider and it takes a slider's keys: the arrows
/// move it a step, Page Up and Page Down further, Home and End to the ends.
/// <see cref="MixDial"/> decides where everything is drawn and which segments
/// are lit; this draws it, and its look is in Dial.axaml.
/// </para>
/// <para>
/// It is handled like the wheel on the headset. Grabbing the knob and
/// turning it moves the mix by as much as it turns, so a press alone changes
/// nothing (<see cref="MixDial.Turn"/>). A press on the ring goes straight to
/// that point, and a press in the gap below the ring does nothing.
/// </para>
/// <para>
/// A step is five points, the chat wheel's own. A step of one would never
/// leave the centre, because the mix holds a balanced setting against a nudge
/// of up to four.
/// </para>
/// </remarks>
public sealed class GameChatDial : RangeBase
{
    /// <summary>The dial's width and height, as its style sets them.</summary>
    private const double Size = 240;

    private const double Centre = Size / 2;
    private const double RingRadius = 108;

    /// <summary>The edge of the well the knob sits in: inside it a press turns the knob, outside it lands on the ring.</summary>
    private const double KnobEdge = 97;

    private const double RidgeInner = 86, RidgeOuter = 94;
    private const int Ridges = 90;
    private const double PointerInner = 62, PointerOuter = 80;

    private readonly Path[] _segments = new Path[MixDial.SegmentCount];
    private Path? _pointer;

    private enum Hold { None, Ring, Knob }
    private Hold _hold;
    private double _turnFrom;
    private double _turned;

    public GameChatDial()
    {
        Minimum = 0;
        Maximum = 100;
        SmallChange = 5;
        LargeChange = 20;
        Focusable = true;
    }

    protected override Type StyleKeyOverride => typeof(GameChatDial);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _pointer = e.NameScope.Find<Path>("Pointer");
        if (e.NameScope.Find<Path>("Ridges") is { } ridges) ridges.Data = RidgeLines();

        if (e.NameScope.Find<Canvas>("Ring") is { } ring)
        {
            ring.Children.Clear();
            for (int i = 0; i < MixDial.SegmentCount; i++)
            {
                _segments[i] = new Path { Data = Arc(MixDial.Segment(i)) };
                ring.Children.Add(_segments[i]);
            }
        }
        Draw();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty) Draw();
    }

    private int Mix => (int)Math.Round(Value);

    /// <summary>Lights the segments and turns the pointer. Style classes carry the colours, so a theme change needs no redraw.</summary>
    private void Draw()
    {
        int mix = Mix;
        for (int i = 0; i < MixDial.SegmentCount; i++)
        {
            if (_segments[i] is null) return;
            var side = MixDial.Lit(i, mix);
            _segments[i].Classes.Set("game", side == DialSide.Game);
            _segments[i].Classes.Set("chat", side == DialSide.Chat);
            _segments[i].Classes.Set("segment", true);
        }

        if (_pointer is null) return;
        double angle = MixDial.AngleOf(mix);
        var (x0, y0) = MixDial.PointAt(angle, PointerInner);
        var (x1, y1) = MixDial.PointAt(angle, PointerOuter);
        _pointer.Data = Line(x0, y0, x1, y1);
        _pointer.Classes.Set("chat", mix > 50);
    }

    private static PathGeometry Arc(DialArc arc)
    {
        var (x0, y0) = MixDial.PointAt(arc.From, RingRadius);
        var (x1, y1) = MixDial.PointAt(arc.To, RingRadius);
        var figure = new PathFigure { StartPoint = new Point(Centre + x0, Centre + y0), IsClosed = false };
        figure.Segments!.Add(new ArcSegment
        {
            Point = new Point(Centre + x1, Centre + y1),
            Size = new Size(RingRadius, RingRadius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = arc.IsLarge,
        });
        return new PathGeometry { Figures = [figure] };
    }

    private static PathGeometry Line(double x0, double y0, double x1, double y1)
    {
        var figure = new PathFigure { StartPoint = new Point(Centre + x0, Centre + y0), IsClosed = false };
        figure.Segments!.Add(new LineSegment { Point = new Point(Centre + x1, Centre + y1) });
        return new PathGeometry { Figures = [figure] };
    }

    private static PathGeometry RidgeLines()
    {
        var geometry = new PathGeometry();
        for (int i = 0; i < Ridges; i++)
        {
            double angle = 360.0 * i / Ridges;
            var (xi, yi) = MixDial.PointAt(angle, RidgeInner);
            var (xo, yo) = MixDial.PointAt(angle, RidgeOuter);
            var figure = new PathFigure { StartPoint = new Point(Centre + xi, Centre + yi), IsClosed = false };
            figure.Segments!.Add(new LineSegment { Point = new Point(Centre + xo, Centre + yo) });
            geometry.Figures!.Add(figure);
        }
        return geometry;
    }

    // -- the pointer -------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;
        double x = point.Position.X - Centre, y = point.Position.Y - Centre;

        if (Math.Sqrt(x * x + y * y) < KnobEdge)
        {
            _hold = Hold.Knob;
            _turnFrom = MixDial.AngleAt(x, y);
            _turned = Value;
        }
        else if (MixDial.Grabs(x, y, KnobEdge, Centre))
        {
            _hold = Hold.Ring;
            Value = MixDial.MixAt(x, y);
        }
        else return;

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_hold == Hold.None) return;
        var at = e.GetPosition(this);
        double x = at.X - Centre, y = at.Y - Centre;
        if (_hold == Hold.Ring)
        {
            Value = MixDial.MixAt(x, y);
        }
        else
        {
            double angle = MixDial.AngleAt(x, y);
            _turned = MixDial.Turn(_turned, _turnFrom, angle);
            _turnFrom = angle;
            Value = Math.Round(_turned);
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_hold == Hold.None) return;
        e.Pointer.Capture(null);
        _hold = Hold.None;
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _hold = Hold.None;
    }

    // -- the keyboard ------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        double? to = e.Key switch
        {
            Key.Left or Key.Down => Value - SmallChange,
            Key.Right or Key.Up => Value + SmallChange,
            Key.PageDown => Value - LargeChange,
            Key.PageUp => Value + LargeChange,
            Key.Home => Minimum,
            Key.End => Maximum,
            _ => null,
        };
        if (to is not double target) { base.OnKeyDown(e); return; }
        Value = Math.Clamp(target, Minimum, Maximum);
        e.Handled = true;
    }
}
