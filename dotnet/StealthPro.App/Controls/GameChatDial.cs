using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using StealthPro.Core.Mix;
using Windows.Foundation;
using Windows.System;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace StealthPro.App.Controls;

/// <summary>
/// The game and chat mix as a dial: a 270° arc with game at its left end,
/// chat at its right and balanced at the top.
/// </summary>
/// <remarks>
/// <para>
/// It is a range control, from 0 (game only) to 100 (chat only), so a screen
/// reader announces it as a slider and it takes a slider's keys: the arrows
/// move it a step, Page Up and Page Down further, Home and End to the ends.
/// <see cref="MixDial"/> decides where everything is drawn; this draws it,
/// and its look is in GameChatDial.xaml.
/// </para>
/// <para>
/// A step is five points, the chat wheel's own. A step of one would never
/// leave the centre, because the mix holds a balanced setting against a nudge
/// of up to four.
/// </para>
/// <para>
/// Only a press on the arc moves the mix; anywhere else it only takes focus.
/// See <see cref="MixDial.Grabs"/>.
/// </para>
/// </remarks>
public sealed partial class GameChatDial : RangeBase
{
    /// <summary>The dial's width and height, as its style sets them.</summary>
    private const double Size = 240;

    private const double Centre = Size / 2;
    private const double Stroke = 12;
    private const double Radius = Centre - Stroke;
    private const double ThumbRadius = 12;

    /// <summary>How near the centre a press may start and still land on the arc.</summary>
    private const double FaceRadius = Radius - 2 * Stroke;

    private Path? _game;
    private Path? _chat;
    private FrameworkElement? _thumb;
    private bool _dragging;

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
        _game = GetTemplateChild("GameArc") as Path;
        _chat = GetTemplateChild("ChatArc") as Path;
        _thumb = GetTemplateChild("Thumb") as FrameworkElement;
        if (GetTemplateChild("Track") is Path track) track.Data = Arc(MixDial.Track);
        Draw();
    }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        Draw();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GameChatDialAutomationPeer(this);

    private int Mix => (int)Math.Round(Value);

    private void Draw()
    {
        int mix = Mix;
        Place(_game, MixDial.GameArc(mix));
        Place(_chat, MixDial.ChatArc(mix));

        if (_thumb is null) return;
        var (x, y) = MixDial.PointAt(MixDial.AngleOf(mix), Radius);
        _thumb.Margin = new Thickness(Centre + x - ThumbRadius, Centre + y - ThumbRadius, 0, 0);
    }

    /// <summary>
    /// Draws one side's arc, or hides it when that side is silent: a round
    /// cap on an arc of no length would still leave a dot.
    /// </summary>
    private static void Place(Path? path, DialArc arc)
    {
        if (path is null) return;
        path.Visibility = arc.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (arc.Length > 0) path.Data = Arc(arc);
    }

    private static PathGeometry Arc(DialArc arc)
    {
        var (x0, y0) = MixDial.PointAt(arc.From, Radius);
        var (x1, y1) = MixDial.PointAt(arc.To, Radius);
        var figure = new PathFigure { StartPoint = new Point(Centre + x0, Centre + y0) };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(Centre + x1, Centre + y1),
            Size = new Size(Radius, Radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = arc.IsLarge,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    // -- the pointer -------------------------------------------------------

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus(FocusState.Pointer);
        var point = e.GetCurrentPoint(this);
        var at = point.Position;
        if (!point.Properties.IsLeftButtonPressed
            || !MixDial.Grabs(at.X - Centre, at.Y - Centre, FaceRadius, Centre)) return;

        _dragging = CapturePointer(e.Pointer);
        MoveTo(at);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        MoveTo(e.GetCurrentPoint(this).Position);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        ReleasePointerCapture(e.Pointer);
        _dragging = false;
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    private void MoveTo(Point at) => Value = MixDial.MixAt(at.X - Centre, at.Y - Centre);

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
