namespace Neap.Core.Presets;

/// <summary>The curves the equaliser offers to start from.</summary>
/// <remarks>
/// Each is named for what it does to the curve, never for what it will sound
/// like: Neap makes no claim about what is heard.
/// </remarks>
public enum StartingShape
{
    Flat,
    CutMud,
    VoiceClarity,
    TameHarshness,
    AddBass,
    CutRumble,
    TameSibilance,
    QuietListening,
    AddTreble,
}

/// <summary>
/// The usual starting points for a parametric equaliser, as ten bands.
/// </summary>
/// <remarks>
/// The frequencies are the regions these are done at on any equaliser, and the
/// amounts are typical, not measured on the Stealth Pro II. Boosts stay at
/// 4 dB or less, since the headset's compressor squashes a larger one.
/// </remarks>
public static class StartingShapes
{
    /// <summary>The ten bands for a shape: its bands first, the rest not in use at their stock frequencies.</summary>
    /// <param name="shape">The shape.</param>
    public static IReadOnlyList<ParametricBand> Bands(StartingShape shape)
    {
        IEnumerable<ParametricBand> used = shape switch
        {
            StartingShape.Flat => [],
            StartingShape.CutMud => [Bell(200, 1.2, -3), Bell(320, 1, -2)],
            StartingShape.VoiceClarity => [Bell(300, 1, -2), Bell(3000, 1.2, 3), new(BandType.HighShelf, 9000, 0.7, 1.5, true)],
            StartingShape.TameHarshness => [Bell(3500, 2, -3), Bell(6000, 3, -4)],
            StartingShape.AddBass => [new(BandType.LowShelf, 90, 0.7, 4, true), Bell(140, 1, 1.5)],
            StartingShape.CutRumble => CutBands.Bands(lowCut: true, 35, CutSlope.Standard),
            StartingShape.TameSibilance => [Bell(6500, 4, -4)],
            StartingShape.QuietListening => [new(BandType.LowShelf, 100, 0.7, 3, true), new(BandType.HighShelf, 8000, 0.7, 2, true)],
            StartingShape.AddTreble => [new(BandType.HighShelf, 10000, 0.7, 3, true)],
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

        var bands = used.ToList();
        return [.. bands.Concat(StockBands.Game.Skip(bands.Count).Select(band => band with { InUse = false }))];
    }

    private static ParametricBand Bell(double hertz, double q, double gain) => new(BandType.Peaking, hertz, q, gain, true);
}
