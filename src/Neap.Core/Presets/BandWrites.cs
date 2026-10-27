namespace Neap.Core.Presets;

/// <summary>One thing about a band the headset can be told, by the low byte of its parameter id.</summary>
public enum BandParameter
{
    Frequency = 3,
    Q = 4,
    Gain = 5,
    Type = 7,
}

/// <summary>One change to send: a band's parameter and its new value.</summary>
/// <param name="Band">The band, counted from zero.</param>
/// <param name="Parameter">What to set.</param>
/// <param name="Value">The frequency in hertz, the Q, the gain in decibels, or the filter code.</param>
public readonly record struct BandWrite(int Band, BandParameter Parameter, double Value);

/// <summary>The game equaliser's ten bands: how their parameters are named, and which of them need sending.</summary>
public static class BandWrites
{
    public const int Bands = 10;

    private const uint GameBank = 0x0007_0100;

    /// <summary>Bit 3 of the low byte sends a change to the sound now, not only to storage.</summary>
    private const uint ToSoundNow = 0x08;

    /// <summary>The id of a game band's parameter.</summary>
    /// <param name="band">The band, counted from zero.</param>
    /// <param name="parameter">What it names.</param>
    /// <param name="toSoundNow">Whether a write takes effect at once; a read leaves it off.</param>
    public static uint Id(int band, BandParameter parameter, bool toSoundNow = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(band);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(band, Bands);
        return GameBank | (uint)(band << 4) | (toSoundNow ? ToSoundNow : 0) | (uint)parameter;
    }

    /// <summary>The writes that bring the headset from what it holds to what is wanted.</summary>
    /// <remarks>
    /// Anything unchanged is left out, as every changed write is saved in the
    /// headset. A band's filter and width go first, then its gain, then its
    /// frequency, since the headset's own notes ask for the gain before the
    /// frequency. Values go as the headset will hold them, after the band is
    /// held inside its limits and a band that is off is sent as bypass.
    /// </remarks>
    /// <param name="held">What the headset is known to hold, or null when that is not known and everything is sent.</param>
    /// <param name="wanted">The ten bands wanted.</param>
    /// <param name="sampleRate">The game stream's sample rate, in hertz.</param>
    public static IReadOnlyList<BandWrite> Plan(
        IReadOnlyList<ParametricBand>? held, IReadOnlyList<ParametricBand> wanted, int sampleRate)
    {
        if (wanted.Count != Bands) throw new ArgumentException($"expected {Bands} bands, got {wanted.Count}", nameof(wanted));
        if (held is not null && held.Count != Bands) throw new ArgumentException($"expected {Bands} bands, got {held.Count}", nameof(held));

        var writes = new List<BandWrite>();
        for (int band = 0; band < Bands; band++)
        {
            var next = wanted[band].Held(sampleRate);
            ParametricBand? was = held?[band].Held(sampleRate);
            Add(writes, band, BandParameter.Type, (double)next.WireType, was is null ? null : (double)was.Value.WireType);
            Add(writes, band, BandParameter.Q, (float)next.Q, was is null ? null : (float)was.Value.Q);
            Add(writes, band, BandParameter.Gain, (float)next.WireGain, was is null ? null : (float)was.Value.WireGain);
            Add(writes, band, BandParameter.Frequency, (float)next.Hertz, was is null ? null : (float)was.Value.Hertz);
        }

        return writes;
    }

    private static void Add(List<BandWrite> writes, int band, BandParameter parameter, double value, double? was)
    {
        if (was != value) writes.Add(new BandWrite(band, parameter, value));
    }
}
