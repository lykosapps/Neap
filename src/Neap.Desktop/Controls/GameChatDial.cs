using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Neap.Core.Mix;
using Path = Avalonia.Controls.Shapes.Path;

namespace Neap.Desktop.Controls;

/// <summary>
/// The game and chat mix as a knob: a flat disc with a pointer, inside a 270° ring of segments that light game from the left and chat
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
/// <para>
/// A value that changes by itself, the chat wheel turning it, glides to its
/// new place when animation is on; a hand on the dial is followed at once.
/// </para>
/// </remarks>
public sealed class GameChatDial : RangeBase
{
    /// <summary>The dial's width and height: the value of `NeapDialSize` in Tokens.axaml.</summary>
    private const double Size = 200;

    private const double Centre = Size / 2;
    private const double RingRadius = 90;

    /// <summary>The edge of the knob's surround: inside it a press turns the knob, outside it lands on the ring.</summary>
    private const double KnobEdge = 81;

    private const double PointerInner = 50, PointerOuter = 66;

    private static readonly TimeSpan Glide = TimeSpan.FromMilliseconds(160);

    /// <summary>Where the dial is drawn, which follows <see cref="RangeBase.Value"/> by a glide.</summary>
    private static readonly StyledProperty<double> ShownProperty =
        AvaloniaProperty.Register<GameChatDial, double>(nameof(Shown));

    private static readonly Transitions Gliding = [new DoubleTransition { Property = ShownProperty, Duration = Glide }];

    private readonly Path[] _segments = new Path[MixDial.SegmentCount];
    private Path? _pointer;
    private bool _placed;

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

        if (e.NameScope.Find<Canvas>("Ring") is { } ring)
        {
            ring.Children.Clear();
            for (int i = 0; i < MixDial.SegmentCount; i++)
            {
                _segments[i] = new Path { Data = Arc(MixDial.Segment(i), RingRadius) };
                ring.Children.Add(_segments[i]);
            }
        }
        Draw();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty) Follow();
        else if (change.Property == ShownProperty) Draw();
    }

    private double Shown
    {
        get => GetValue(ShownProperty);
        set => SetValue(ShownProperty, value);
    }

    /// <summary>Moves what is drawn to the value: at once for a hand or a first placing, by a glide otherwise.</summary>
    /// <remarks>Animation off in the system means no glide; see <see cref="Motion"/>.</remarks>
    private void Follow()
    {
        Transitions = _placed && _hold == Hold.None && Motion.Enabled ? Gliding : null;
        _placed = true;
        Shown = Value;
    }

    private int Mix => (int)Math.Round(Shown);

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

    private static PathGeometry Arc(DialArc arc, double radius)
    {
        var (x0, y0) = MixDial.PointAt(arc.From, radius);
        var (x1, y1) = MixDial.PointAt(arc.To, radius);
        var figure = new PathFigure { StartPoint = new Point(Centre + x0, Centre + y0), IsClosed = false };
        figure.Segments!.Add(new ArcSegment
        {
            Point = new Point(Centre + x1, Centre + y1),
            Size = new Size(radius, radius),
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
