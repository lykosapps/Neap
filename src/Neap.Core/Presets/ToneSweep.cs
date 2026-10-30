namespace Neap.Core.Presets;

/// <summary>
/// The rules of the parametric equaliser's test tone: how a sweep moves, how
/// loud a level is, where its slider puts it, and what cutting or boosting
/// where it stands adds.
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

    /// <summary>How far a new cut or boost goes, in decibels: enough to hear, easy to take further.</summary>
    private const double FirstGain = 3;

    private static double Span => ParametricBand.HighestHertz / ParametricBand.LowestHertz;

    /// <summary>Where a sweep that started at one frequency has got to after this long.</summary>
    public static double After(double from, double seconds) =>
        Math.Min(ParametricBand.HighestHertz, from * Math.Pow(Span, seconds / Seconds));

    /// <summary>Whether a sweep has reached the top of the range.</summary>
    public static bool Finished(double frequency) => frequency >= ParametricBand.HighestHertz;

    /// <summary>Where a sweep starts: from the tone's frequency, or from the bottom if it is already at the top.</summary>
    public static double Start(double frequency) =>
        Finished(frequency) ? ParametricBand.LowestHertz : frequency;

    /// <summary>The tone's loudness, from 0 to 1, for a level from 0 to 100.</summary>
    /// <remarks>Squared, so the level control moves evenly as heard rather than bunching at the quiet end.</remarks>
    public static double Amplitude(int level)
    {
        double share = Math.Clamp(level, 0, 100) / 100.0;
        return Loudest * share * share;
    }

    /// <summary>The frequency at a position along a slider, on a logarithmic scale.</summary>
    /// <param name="position">The slider's value.</param>
    /// <param name="maximum">The slider's largest value.</param>
    public static double Hertz(double position, double maximum) =>
        LogScale.At(position / maximum, ParametricBand.LowestHertz, ParametricBand.HighestHertz);

    /// <summary>The position along a slider of a frequency.</summary>
    /// <param name="hertz">The frequency.</param>
    /// <param name="maximum">The slider's largest value.</param>
    public static double Position(double hertz, double maximum) =>
        maximum * LogScale.Share(hertz, ParametricBand.LowestHertz, ParametricBand.HighestHertz);

    /// <summary>A new bell at the tone's frequency, a cut or a boost.</summary>
    /// <param name="frequency">Where the tone is.</param>
    /// <param name="boost">True for a boost, false for a cut.</param>
    public static ParametricBand Bell(double frequency, bool boost) =>
        new ParametricBand(BandType.Peaking, Math.Round(frequency), 1.0, boost ? FirstGain : -FirstGain, true).Held();
}
