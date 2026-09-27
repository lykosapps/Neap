namespace Neap.Core.Mix;

/// <summary>Where a mix move should land, and whether it earns the centre cue.</summary>
/// <param name="Value">Where the mix should be set.</param>
/// <param name="Cue">Whether landing here should sound the centre cue.</param>
/// <param name="Held">The detent state to pass into the next call.</param>
public readonly record struct MixSnap(int Value, bool Cue, bool Held);

/// <summary>
/// The mix's centre detent: catches a move onto or across 50 and holds it
/// there until the mix is deliberately moved away.
/// </summary>
/// <remarks>
/// The wheel steps in fives and rarely lands on exactly 50. A plain snap
/// window is not enough: a fast turn straight through centre skips it, and a
/// value snapped to 50 slides off again on the next notch, so the beep and
/// the number disagree. This catches a crossing, and holds it: the dial and
/// the wheel both take this same path, so the detent behaves identically
/// whichever moved it.
/// </remarks>
public static class MixDetent
{
    /// <summary>Centre detent half-width, in mix points.</summary>
    public const int Width = 4;

    /// <summary>
    /// Biggest step still treated as a wheel notch. The dial dragged across
    /// the whole range passes through centre rather than sticking to it.
    /// </summary>
    public const int CrossingLimit = 15;

    /// <summary>Where a move to <paramref name="want"/> should land.</summary>
    /// <param name="want">The mix asked for, 0 to 100.</param>
    /// <param name="previous">Where the mix was last set, or null for the first move.</param>
    /// <param name="held">Whether the previous move already landed the detent.</param>
    public static MixSnap Apply(int want, int? previous, bool held)
    {
        bool crossed = previous is int was
            && (was - 50) * (want - 50) < 0
            && Math.Abs(want - was) <= CrossingLimit;
        bool near = Math.Abs(want - 50) <= Width;

        if ((near || crossed) && !held) return new(50, Cue: true, Held: true);
        if (near) return new(50, Cue: false, Held: true);
        return new(want, Cue: false, Held: false);
    }
}
