namespace Neap.Core.Presets;

/// <summary>One parametric band as the app's settings keep it.</summary>
/// <param name="Type">The filter, by the headset's code.</param>
/// <param name="Hertz">The frequency.</param>
/// <param name="Q">The width.</param>
/// <param name="Gain">The gain in decibels.</param>
/// <param name="InUse">Whether the band is applied.</param>
public sealed record StoredBand(int Type, double Hertz, double Q, double Gain, bool InUse)
{
    /// <summary>The band as it is stored.</summary>
    /// <param name="band">The band.</param>
    public static StoredBand Of(ParametricBand band) => new((int)band.Type, band.Hertz, band.Q, band.Gain, band.InUse);

    /// <summary>The ten bands stored, or null when what is stored is not ten bands with filters the headset has.</summary>
    /// <param name="stored">What was stored.</param>
    public static IReadOnlyList<ParametricBand>? Bands(IReadOnlyList<StoredBand> stored)
    {
        if (stored.Count != BandWrites.Bands) return null;
        if (stored.Any(band => !Enum.IsDefined((BandType)band.Type))) return null;
        return [.. stored.Select(band => new ParametricBand((BandType)band.Type, band.Hertz, band.Q, band.Gain, band.InUse).Held())];
    }
}
