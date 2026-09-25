namespace Neap.Core.Audio;

/// <summary>
/// Turns what the microphone hears into a meter: the loudest sample in a
/// buffer, and how many of a meter's bars that lights.
/// </summary>
/// <remarks>
/// The meter is in decibels, from <see cref="Floor"/> to full scale. Sound
/// is heard on that scale, and on a straight line an ordinary speaking voice
/// sits in the bottom few bars and reads as a microphone barely working.
/// </remarks>
public static class MicLevel
{
    /// <summary>The quietest level the meter shows, in decibels below full scale.</summary>
    public const double Floor = -60;

    /// <summary>The loudest sample in a buffer of 32-bit float samples, from 0 to 1.</summary>
    /// <remarks>A partial sample at the end of the buffer is ignored.</remarks>
    public static float PeakOf(ReadOnlySpan<byte> floats)
    {
        float peak = 0;
        for (int i = 0; i + 4 <= floats.Length; i += 4)
        {
            float sample = Math.Abs(BitConverter.ToSingle(floats.Slice(i, 4)));
            if (sample > peak) peak = sample;
        }
        return Math.Min(peak, 1f);
    }

    /// <summary>How many of a meter's bars a peak lights.</summary>
    public static int Lit(float peak, int bars)
    {
        if (peak <= 0 || bars <= 0) return 0;
        double db = 20 * Math.Log10(Math.Min(peak, 1f));
        double share = (db - Floor) / -Floor;
        return (int)Math.Round(Math.Clamp(share, 0, 1) * bars, MidpointRounding.AwayFromZero);
    }

    /// <summary>The bars to show next, falling back one bar at a time rather than dropping at once.</summary>
    /// <remarks>
    /// Speech is a string of short peaks. Shown as they arrive the meter
    /// flickers to nothing between syllables; falling a bar per reading
    /// keeps a voice reading as a voice.
    /// </remarks>
    public static int Fall(int shown, int heard) => Math.Max(heard, shown - 1);
}
