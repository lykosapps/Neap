namespace Neap.Core.Presets;

/// <summary>How steeply a cut falls away, in decibels an octave.</summary>
public enum CutSlope
{
    Gentle = 6,
    Standard = 12,
    Steep = 24,
    Steepest = 36,
}

/// <summary>Cuts steeper than one band's, made by stacking bands at one frequency.</summary>
/// <remarks>
/// The headset has no slope control. One band falls 12 dB an octave, a gentle
/// one 6, and stacking two or three gives 24 and 36. Each stage needs its own
/// width for the cutoff to stay flat, so the stages are not copies of each
/// other. Computed for the firmware's filter by SP2Tools and not yet heard.
/// </remarks>
public static class CutBands
{
    private static readonly double[] Two = [0.5412, 1.3066];
    private static readonly double[] Three = [0.5176, 0.7071, 1.9319];

    private const double Single = 0.7071;

    /// <summary>How many of the ten bands a slope uses.</summary>
    /// <param name="slope">The slope.</param>
    public static int BandsFor(CutSlope slope) => slope switch
    {
        CutSlope.Gentle or CutSlope.Standard => 1,
        CutSlope.Steep => 2,
        CutSlope.Steepest => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(slope)),
    };

    /// <summary>The bands that make a cut.</summary>
    /// <param name="lowCut">True to remove what is below the frequency, false for what is above.</param>
    /// <param name="hertz">The cutoff, in hertz.</param>
    /// <param name="slope">How steeply it falls.</param>
    public static IReadOnlyList<ParametricBand> Bands(bool lowCut, double hertz, CutSlope slope)
    {
        var (whole, gentle) = lowCut
            ? (BandType.LowCut, BandType.GentleLowCut)
            : (BandType.HighCut, BandType.GentleHighCut);
        double[] widths = slope switch
        {
            CutSlope.Gentle or CutSlope.Standard => [Single],
            CutSlope.Steep => Two,
            CutSlope.Steepest => Three,
            _ => throw new ArgumentOutOfRangeException(nameof(slope)),
        };
        var type = slope == CutSlope.Gentle ? gentle : whole;
        return [.. widths.Select(q => new ParametricBand(type, hertz, q, 0, true))];
    }
}
