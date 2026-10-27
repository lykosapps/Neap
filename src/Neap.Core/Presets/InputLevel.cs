namespace Neap.Core.Presets;

/// <summary>How far to lower the level before the game equaliser to make room for its boosts.</summary>
/// <remarks>
/// The level is the loudness level (<see cref="Audio.LoudnessBoost"/>), which
/// ships at -12 dB. Lowering it by as much as a band boosts returns the game
/// compressor's input to its unboosted level, which keeps the boost's shape
/// (SP2Tools' arithmetic, not listened to yet). Only lowering from stock is
/// offered with the equaliser; raising it is the loudness boost's job.
/// </remarks>
public static class InputLevel
{
    /// <summary>The most the level is lowered below stock for the equaliser, in decibels.</summary>
    public const double MostPullDown = 12;

    /// <summary>The pull-down that matches the biggest boost, in half decibels, held to the most offered.</summary>
    /// <param name="bands">The ten bands.</param>
    public static double Suggested(IReadOnlyList<ParametricBand> bands)
    {
        double boost = bands.Where(band => band.InUse && band.UsesGain).Select(band => band.Gain).DefaultIfEmpty(0).Max();
        return Math.Clamp(Math.Ceiling(boost * 2) / 2, 0, MostPullDown);
    }
}
