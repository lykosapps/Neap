namespace Neap.Core.Presets;

/// <summary>
/// The rules of the parametric equaliser's test tone: how a sweep moves, how
/// loud a level is, and what cutting or boosting where it stands adds.
/// </summary>
/// <remarks>
/// A sweep glides at an even pace on a logarithmic scale, as pitch is heard,
/// so every octave takes the same time and none rushes past.
/// </remarks>
public static class ToneSweep
{
    /// <summary>How long a sweep takes from the bottom of the range to the top.</summary>
    public const double Seconds = 30;

    /// <summary>The level the tone starts at: quiet, since a tone is harsher than music at the same volume.</summary>
    public const int FirstLevel = 20;

    /// <summary>The loudest the tone plays, as a share of full scale: 12 dB below it.</summary>
    private const double Loudest = 0.25;

    /// <summary>How far a new cut or boost goes, in tenths of a decibel: enough to hear, easy to take further.</summary>
    private const int FirstGain = 30;

    /// <summary>Where a sweep that started at one frequency has got to after this long.</summary>
    public static double After(double from, double seconds)
    {
        double span = (double)Adjustment.HighestFrequency / Adjustment.LowestFrequency;
        return Math.Min(Adjustment.HighestFrequency, from * Math.Pow(span, seconds / Seconds));
    }

    /// <summary>Whether a sweep has reached the top of the range.</summary>
    public static bool Finished(double frequency) => frequency >= Adjustment.HighestFrequency;

    /// <summary>Where a sweep starts: from the tone's frequency, or from the bottom if it is already at the top.</summary>
    public static double Start(double frequency) =>
        Finished(frequency) ? Adjustment.LowestFrequency : frequency;

    /// <summary>The tone's loudness, from 0 to 1, for a level from 0 to 100.</summary>
    /// <remarks>Squared, so the level control moves evenly as heard rather than bunching at the quiet end.</remarks>
    public static double Amplitude(int level)
    {
        double share = Math.Clamp(level, 0, 100) / 100.0;
        return Loudest * share * share;
    }

    /// <summary>A new adjustment at the tone's frequency, a cut or a boost.</summary>
    public static Adjustment At(double frequency, bool boost) =>
        new Adjustment((int)Math.Round(frequency), boost ? FirstGain : -FirstGain, 15).Held();
}
