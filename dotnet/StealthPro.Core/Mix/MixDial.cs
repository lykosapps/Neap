namespace StealthPro.Core.Mix;

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

    /// <summary>The whole dial, drawn behind both sides as its track.</summary>
    public static DialArc Track { get; } = new(GameEnd, ChatEnd);

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
        double angle = Math.Atan2(x, -y) * 180 / Math.PI;
        angle = Math.Clamp(angle, GameEnd, ChatEnd);
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
        double angle = Math.Atan2(x, -y) * 180 / Math.PI;
        return distance >= inner && distance <= outer && angle >= GameEnd && angle <= ChatEnd;
    }

    /// <summary>The point at an angle and distance from the centre.</summary>
    public static (double X, double Y) PointAt(double angle, double radius)
    {
        double radians = angle * Math.PI / 180;
        return (radius * Math.Sin(radians), -radius * Math.Cos(radians));
    }
}
