namespace Neap.Core.Mix;

/// <summary>Which side of the mix a lit stretch of the dial belongs to.</summary>
public enum DialSide { None, Game, Chat }

/// <summary>A stretch of the dial, from one angle to a later one.</summary>
public readonly record struct DialArc(double From, double To)
{
    public double Length => Math.Max(0, To - From);

    /// <summary>Gets whether the arc turns through more than half a circle, as drawing it needs to know.</summary>
    public bool IsLarge => Length > 180;
}

/// <summary>
/// The geometry of the game and chat dial: a 270° arc with game at its left
/// end, chat at its right and balanced at the top.
/// </summary>
/// <remarks>
/// <para>
/// Angles are degrees clockwise from twelve o'clock, and points are relative
/// to the dial's centre with y growing downward, as the screen draws them.
/// </para>
/// <para>
/// The dial moves the way the slider does: toward chat raises the mix. The
/// pointer shows the mix, and each side's arc shows that side's level,
/// growing from its own end. At balanced both levels are full, so both
/// halves are full and meet under the pointer; moving toward chat shortens
/// only the game arc.
/// </para>
/// </remarks>
public static class MixDial
{
    public const double Sweep = 270;
    public const double GameEnd = -Sweep / 2;
    public const double ChatEnd = Sweep / 2;

    /// <summary>Where the pointer sits for a mix.</summary>
    public static double AngleOf(int mix) => GameEnd + Sweep * Math.Clamp(mix, 0, 100) / 100.0;

    /// <summary>The game arc, from the game end, as long as the game side is loud.</summary>
    public static DialArc GameArc(int mix) => new(GameEnd, GameEnd + Sweep / 2 * MixLevels.GameScale(mix));

    /// <summary>The chat arc, back from the chat end, as long as the chat side is loud.</summary>
    public static DialArc ChatArc(int mix) => new(ChatEnd - Sweep / 2 * MixLevels.ChatScale(mix), ChatEnd);

    /// <summary>The mix for a point pressed or dragged on the dial.</summary>
    /// <remarks>
    /// A point in the gap below the dial goes to the nearer end, so dragging
    /// past an end stops there.
    /// </remarks>
    public static int MixAt(double x, double y)
    {
        double angle = Math.Clamp(AngleAt(x, y), GameEnd, ChatEnd);
        return (int)Math.Round((angle - GameEnd) / Sweep * 100);
    }

    /// <summary>Whether a press at this point lands on the arc, between two distances from the centre.</summary>
    /// <remarks>
    /// Only a press on the arc moves the mix. The face inside it carries the
    /// levels, and the gap below it lies past both ends: a press there would
    /// throw the mix to whichever end was nearer and silence the other side.
    /// Once a drag has started it may go anywhere; see <see cref="MixAt"/>.
    /// </remarks>
    public static bool Grabs(double x, double y, double inner, double outer)
    {
        double distance = Math.Sqrt(x * x + y * y);
        double angle = AngleAt(x, y);
        return distance >= inner && distance <= outer && angle >= GameEnd && angle <= ChatEnd;
    }

    /// <summary>How many lit segments the dial's ring is made of.</summary>
    public const int SegmentCount = 30;

    /// <summary>The gap left between two segments, in degrees.</summary>
    private const double SegmentGap = 3.2;

    /// <summary>One segment of the ring, counted from the game end.</summary>
    public static DialArc Segment(int index)
    {
        double width = Sweep / SegmentCount;
        double from = GameEnd + width * index;
        return new(from + SegmentGap / 2, from + width - SegmentGap / 2);
    }

    /// <summary>Which side lights a segment, or none: each side lights as far as its own arc reaches.</summary>
    public static DialSide Lit(int index, int mix)
    {
        var segment = Segment(index);
        double middle = (segment.From + segment.To) / 2;
        return middle <= GameArc(mix).To ? DialSide.Game
            : middle >= ChatArc(mix).From ? DialSide.Chat
            : DialSide.None;
    }

    /// <summary>The angle of a point from the centre, in the dial's degrees.</summary>
    public static double AngleAt(double x, double y) => Math.Atan2(x, -y) * 180 / Math.PI;

    /// <summary>The mix after the knob is turned from one angle to another.</summary>
    /// <remarks>
    /// A knob is turned, not pointed: grabbing it anywhere and turning moves
    /// the mix by as much as it turns, and a press that does not turn it
    /// changes nothing. A turn across the bottom of the dial, where the angle
    /// jumps from +180 to -180, is taken the short way round.
    /// </remarks>
    public static double Turn(double mix, double fromAngle, double toAngle)
    {
        double delta = toAngle - fromAngle;
        while (delta > 180) delta -= 360;
        while (delta <= -180) delta += 360;
        return Math.Clamp(mix + delta / Sweep * 100, 0, 100);
    }

    /// <summary>The point at an angle and distance from the centre.</summary>
    public static (double X, double Y) PointAt(double angle, double radius)
    {
        double radians = angle * Math.PI / 180;
        return (radius * Math.Sin(radians), -radius * Math.Cos(radians));
    }
}
