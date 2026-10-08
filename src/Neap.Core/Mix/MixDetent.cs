namespace Neap.Core.Mix;

/// <summary>Where a mix move should land, and whether it earns the centre cue.</summary>
/// <param name="Value">Where the mix should be set.</param>
/// <param name="Cue">Whether landing here should sound the centre cue.</param>
public readonly record struct MixSnap(int Value, bool Cue);

/// <summary>
/// The mix's centre detent: catches a move onto or across 50 and holds it
/// there until the mix is deliberately moved away.
/// </summary>
/// <remarks>
/// <para>
/// The wheel steps in fives and rarely lands on exactly 50. A plain snap
/// window is not enough: a turn straight through centre skips it, and a
/// value snapped to 50 slides off again on the next notch, so the beep and
/// the number disagree. This catches a crossing, and holds it.
/// </para>
/// <para>
/// Turned fast, the wheel reports in jumps of up to 30, so every crossing by
/// the wheel is caught, however far it jumps. The dial and the keys are caught
/// only on a step the size of a notch: the dial dragged across the whole range
/// passes through centre rather than sticking to it.
/// </para>
/// <para>
/// A wheel that has just been caught stays caught for <see cref="Stick"/>. The
/// wheel is still turning as the beep sounds, and without the pause the rest of
/// the turn carries the mix straight off centre. Turning on past it moves the
/// mix on.
/// </para>
/// </remarks>
/// <param name="clock">The time now, from a clock that only moves forward.</param>
public sealed class MixDetent(Func<TimeSpan> clock)
{
    /// <summary>Centre detent half-width, in mix points.</summary>
    public const int Width = 4;

    /// <summary>Biggest step by the dial or the keys still caught crossing centre.</summary>
    public const int CrossingLimit = 15;

    /// <summary>How long the wheel stays at centre once caught there.</summary>
    public static readonly TimeSpan Stick = TimeSpan.FromMilliseconds(500);

    private bool _held;
    private TimeSpan _caughtAt;

    /// <summary>Where a move to <paramref name="want"/> should land.</summary>
    /// <param name="want">The mix asked for, 0 to 100.</param>
    /// <param name="previous">Where the mix was last set, or null for the first move.</param>
    /// <param name="wheel">Whether the chat wheel made the move.</param>
    public MixSnap Apply(int want, int? previous, bool wheel)
    {
        TimeSpan now = clock();
        if (wheel && _held && now - _caughtAt < Stick) return new(50, Cue: false);

        bool crossed = previous is int was
            && (was - 50) * (want - 50) < 0
            && (wheel || Math.Abs(want - was) <= CrossingLimit);
        bool near = Math.Abs(want - 50) <= Width;

        if ((near || crossed) && !_held)
        {
            _held = true;
            _caughtAt = now;
            return new(50, Cue: true);
        }
        _held = near;
        return new(near ? 50 : want, Cue: false);
    }
}
