using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Neap.Core.Mix;
using Windows.Foundation;
using Windows.System;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Neap.App.Controls;

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
/// are lit; this draws it, and its look is in GameChatDial.xaml.
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
public sealed partial class GameChatDial : RangeBase
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
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _pointer = GetTemplateChild("Pointer") as Path;
        if (GetTemplateChild("Ridges") is Path ridges) ridges.Data = RidgeLines();

        if (GetTemplateChild("Ring") is Canvas ring)
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

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        Draw();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GameChatDialAutomationPeer(this);

    private int Mix => (int)Math.Round(Value);

    /// <summary>Lights the segments and turns the pointer. Styles carry the colours, so a theme change needs no redraw.</summary>
    private void Draw()
    {
        int mix = Mix;
        for (int i = 0; i < MixDial.SegmentCount; i++)
        {
            if (_segments[i] is null) return;
            var side = MixDial.Lit(i, mix);
            _segments[i].Style = Styled(side switch
            {
                DialSide.Game => "NeapSegmentGameStyle",
                DialSide.Chat => "NeapSegmentChatStyle",
                _ => "NeapSegmentOffStyle",
            });
        }

        if (_pointer is null) return;
        double angle = MixDial.AngleOf(mix);
        var (x0, y0) = MixDial.PointAt(angle, PointerInner);
        var (x1, y1) = MixDial.PointAt(angle, PointerOuter);
        _pointer.Data = Line(x0, y0, x1, y1);
        _pointer.Style = Styled(mix <= 50 ? "NeapPointerGameStyle" : "NeapPointerChatStyle");
    }

    private static Style Styled(string key) => (Style)Application.Current.Resources[key];

    private static PathGeometry Arc(DialArc arc)
    {
        var (x0, y0) = MixDial.PointAt(arc.From, RingRadius);
        var (x1, y1) = MixDial.PointAt(arc.To, RingRadius);
        var figure = new PathFigure { StartPoint = new Point(Centre + x0, Centre + y0) };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(Centre + x1, Centre + y1),
            Size = new Size(RingRadius, RingRadius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = arc.IsLarge,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static PathGeometry Line(double x0, double y0, double x1, double y1)
    {
        var figure = new PathFigure { StartPoint = new Point(Centre + x0, Centre + y0) };
        figure.Segments.Add(new LineSegment { Point = new Point(Centre + x1, Centre + y1) });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static PathGeometry RidgeLines()
    {
        var geometry = new PathGeometry();
        for (int i = 0; i < Ridges; i++)
        {
            double angle = 360.0 * i / Ridges;
            var (xi, yi) = MixDial.PointAt(angle, RidgeInner);
            var (xo, yo) = MixDial.PointAt(angle, RidgeOuter);
            var figure = new PathFigure { StartPoint = new Point(Centre + xi, Centre + yi) };
            figure.Segments.Add(new LineSegment { Point = new Point(Centre + xo, Centre + yo) });
            geometry.Figures.Add(figure);
        }
        return geometry;
    }

    // -- the pointer -------------------------------------------------------

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus(FocusState.Pointer);
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

        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_hold == Hold.None) return;
        var at = e.GetCurrentPoint(this).Position;
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

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_hold == Hold.None) return;
        ReleasePointerCapture(e.Pointer);
        _hold = Hold.None;
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _hold = Hold.None;
    }

    // -- the keyboard ------------------------------------------------------

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        double? to = e.Key switch
        {
            VirtualKey.Left or VirtualKey.Down => Value - SmallChange,
            VirtualKey.Right or VirtualKey.Up => Value + SmallChange,
            VirtualKey.PageDown => Value - LargeChange,
            VirtualKey.PageUp => Value + LargeChange,
            VirtualKey.Home => Minimum,
            VirtualKey.End => Maximum,
            _ => null,
        };
        if (to is not double target) { base.OnKeyDown(e); return; }
        Value = Math.Clamp(target, Minimum, Maximum);
        e.Handled = true;
    }
}

/// <summary>Presents the dial to screen readers as a slider with its own class name.</summary>
public sealed partial class GameChatDialAutomationPeer(GameChatDial owner) : RangeBaseAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override string GetClassNameCore() => nameof(GameChatDial);
}
