namespace Neap.Core.Presets;

/// <summary>The filter a parametric band applies, by the code the headset uses for it.</summary>
/// <remarks>
/// Only the types the equaliser offers are named. The codes between them
/// (band-pass, all-pass) are never sent.
/// </remarks>
public enum BandType
{
    /// <summary>The band does nothing.</summary>
    Bypass = 0,

    /// <summary>Removes everything above the frequency: a low-pass filter.</summary>
    HighCut = 1,

    /// <summary>Removes everything below the frequency: a high-pass filter.</summary>
    LowCut = 2,

    /// <summary>A bell: boosts or cuts around the frequency.</summary>
    Peaking = 4,

    /// <summary>Removes a narrow band around the frequency.</summary>
    Notch = 5,

    /// <summary>Boosts or cuts everything below the frequency.</summary>
    LowShelf = 6,

    /// <summary>Boosts or cuts everything above the frequency.</summary>
    HighShelf = 7,

    /// <summary>A high cut that falls 6 dB an octave, not 12.</summary>
    GentleHighCut = 9,

    /// <summary>A low cut that falls 6 dB an octave, not 12.</summary>
    GentleLowCut = 10,
}

/// <summary>One band of the game equaliser: a filter, where it acts, how wide and by how much.</summary>
/// <remarks>
/// The headset has no switch for "not in use": a band that is off is sent as
/// <see cref="BandType.Bypass"/>. The band keeps its other values here, so
/// switching it back on returns what it was.
/// </remarks>
/// <param name="Type">The filter.</param>
/// <param name="Hertz">The frequency the filter acts at.</param>
/// <param name="Q">The width, or for a cut the resonance at the cutoff.</param>
/// <param name="Gain">The boost or cut in decibels; only a bell and the shelves use it.</param>
/// <param name="InUse">Whether the band is applied.</param>
public readonly record struct ParametricBand(BandType Type, double Hertz, double Q, double Gain, bool InUse)
{
    public const double LowestHertz = 20;
    public const double HighestHertz = 20000;
    public const double LowestQ = 0.1;
    public const double HighestQ = 10;
    public const double MostGain = 12;

    /// <summary>How far up the sample rate a frequency may go.</summary>
    /// <remarks>The firmware does not check, and a frequency near half the rate misbehaves.</remarks>
    private const double SampleRateShare = 0.45;

    /// <summary>Whether the filter has a gain, so the screen offers one.</summary>
    public bool UsesGain => Type is BandType.Peaking or BandType.LowShelf or BandType.HighShelf;

    /// <summary>The filter code to send: the band's own when it is in use, else bypass.</summary>
    public BandType WireType => InUse ? Type : BandType.Bypass;

    /// <summary>The gain to send: the band's own when it is in use and has one, else zero.</summary>
    public double WireGain => InUse && UsesGain ? Gain : 0;

    /// <summary>The highest frequency a band may be set to at a sample rate.</summary>
    /// <param name="sampleRate">The game stream's sample rate, in hertz.</param>
    public static double HighestHertzFor(int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 1000);
        return Math.Min(HighestHertz, SampleRateShare * sampleRate);
    }

    /// <summary>The same band with every value inside its range.</summary>
    /// <param name="sampleRate">The game stream's sample rate, in hertz.</param>
    public ParametricBand Held(int sampleRate) => this with
    {
        Hertz = Math.Clamp(Hertz, LowestHertz, HighestHertzFor(sampleRate)),
        Q = Math.Clamp(Q, LowestQ, HighestQ),
        Gain = Math.Clamp(Gain, -MostGain, MostGain),
    };
}
