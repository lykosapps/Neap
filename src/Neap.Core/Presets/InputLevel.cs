namespace Neap.Core.Presets;

/// <summary>
/// The level before the game equaliser, which Neap offers only as a pull-down
/// from the headset's stock level.
/// </summary>
/// <remarks>
/// The stock level is -12 dB and the firmware reaches +12 dB, but a level
/// above stock is louder and untested, so only lowering is offered. Lowering
/// by as much as a band boosts returns the game compressor's input to its
/// unboosted level, which keeps the boost's shape (SP2Tools' arithmetic, not
/// listened to yet).
/// </remarks>
public static class InputLevel
{
    /// <summary>The level the headset ships with, in decibels.</summary>
    public const double StockDb = -12;

    /// <summary>The most a user may pull the level down, in decibels.</summary>
    public const double MostPullDown = 12;

    /// <summary>The absolute level for a pull-down.</summary>
    /// <param name="pullDown">Decibels below stock, 0 to <see cref="MostPullDown"/>.</param>
    public static double Absolute(double pullDown) => StockDb - Math.Clamp(pullDown, 0, MostPullDown);

    /// <summary>The pull-down an absolute level comes to.</summary>
    /// <param name="absoluteDb">The level the headset reports.</param>
    public static double PullDown(double absoluteDb) => StockDb - absoluteDb;

    /// <summary>Whether the headset's level is one the screen offers, so it can be shown as a pull-down.</summary>
    /// <param name="absoluteDb">The level the headset reports.</param>
    public static bool IsOffered(double absoluteDb) => PullDown(absoluteDb) is >= 0 and <= MostPullDown;

    /// <summary>The pull-down that matches the biggest boost, in half decibels, held to what is offered.</summary>
    /// <param name="bands">The ten bands.</param>
    public static double Suggested(IReadOnlyList<ParametricBand> bands)
    {
        double boost = bands.Where(band => band.InUse && band.UsesGain).Select(band => band.Gain).DefaultIfEmpty(0).Max();
        return Math.Clamp(Math.Ceiling(boost * 2) / 2, 0, MostPullDown);
    }
}
