namespace Neap.Core.Presets;

/// <summary>The game equaliser's ten bands as the headset ships them.</summary>
/// <remarks>
/// Read from one headset's saved record (SP2Tools, 27 October), so units or
/// firmware that differ are not ruled out. Neap backs up the whole record
/// before it changes anything and compares against that; this table says what
/// to expect, and what the ten-band equaliser puts back.
/// </remarks>
public static class StockBands
{
    private static readonly double[] Hertz = [31.5, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    /// <summary>One octave wide, the stock width of every band.</summary>
    public const double Q = 1.4142;

    /// <summary>Flat bells at the stock frequencies.</summary>
    public static IReadOnlyList<ParametricBand> Game { get; } =
        [.. Hertz.Select(hertz => new ParametricBand(BandType.Peaking, hertz, Q, 0, true))];

    /// <summary>Whether each band has its stock filter, frequency and width, whatever its gain.</summary>
    /// <param name="bands">The ten bands to compare.</param>
    public static bool Match(IReadOnlyList<ParametricBand> bands)
    {
        if (bands.Count != Game.Count) return false;
        for (int band = 0; band < Game.Count; band++)
        {
            if (bands[band].Type != BandType.Peaking
                || Math.Abs(bands[band].Hertz - Game[band].Hertz) > 0.01
                || Math.Abs(bands[band].Q - Q) > 0.001)
            {
                return false;
            }
        }

        return true;
    }
}
