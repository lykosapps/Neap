using Neap.Core.Audio;

namespace Neap.Core.Presets;

/// <summary>
/// Whether the game equaliser's boosts, at the pre-gain in use, reach the
/// headset's compressor, and the pre-gain that would keep them clear.
/// </summary>
/// <remarks>
/// The compressor sits after the equaliser with a threshold of -10 dB and a
/// ratio of 100 to 1, so past it a boost raises compression more than level.
/// A full-scale peak reaches it at the level plus the biggest boost. This is
/// SP2Tools' arithmetic and has not been listened to. It is advice only: a
/// person who raised the level on purpose may want the squash, so nothing here
/// changes the level. The most it asks for is 12 dB, the equaliser's largest
/// boost, which never takes the level below what the loudness control offers.
/// </remarks>
public static class LevelHeadroom
{
    /// <summary>Where the compressor starts to act, in decibels below full scale.</summary>
    public const double CompressorThresholdDb = -10;

    /// <summary>Where a full-scale peak reaches the compressor, in decibels below full scale.</summary>
    /// <param name="levelDb">The loudness level the headset holds.</param>
    /// <param name="bands">The ten bands.</param>
    public static double PeakDb(double levelDb, IReadOnlyList<ParametricBand> bands) => levelDb + BiggestBoost(bands);

    /// <summary>Whether a full-scale peak reaches past the compressor's threshold.</summary>
    /// <param name="levelDb">The loudness level the headset holds.</param>
    /// <param name="bands">The ten bands.</param>
    public static bool Squashes(double levelDb, IReadOnlyList<ParametricBand> bands) =>
        PeakDb(levelDb, bands) > CompressorThresholdDb;

    /// <summary>The highest pre-gain, in the whole decibels the control steps in, that brings a full-scale peak back to the threshold.</summary>
    /// <remarks>In whole decibels so the control shows exactly what is set, even from a level set elsewhere in fractions.</remarks>
    /// <param name="levelDb">The loudness level the headset holds.</param>
    /// <param name="bands">The ten bands.</param>
    public static int FittingBoost(double levelDb, IReadOnlyList<ParametricBand> bands) =>
        (int)Math.Floor(LoudnessBoost.BoostOf((float)levelDb) - Math.Max(0, PeakDb(levelDb, bands) - CompressorThresholdDb));

    private static double BiggestBoost(IReadOnlyList<ParametricBand> bands) =>
        bands.Where(band => band.InUse && band.UsesGain).Select(band => band.Gain).DefaultIfEmpty(0).Max(boost => Math.Max(0, boost));
}
