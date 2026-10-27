using Neap.Core.Presets;

namespace Neap.Core.Audio;

/// <summary>
/// RACE command 0x000A for the game equaliser's bands: sets or reads one
/// band's frequency, width, gain or filter.
/// </summary>
/// <remarks>
/// A change is sent to the sound at once and saved by the headset. The
/// frequency, width and gain are 32-bit floats; the filter is an integer
/// code. A reply is accepted only inside the firmware's own ranges, so a
/// reading that is not one is a failure and not a value.
/// </remarks>
public static class BandFrames
{
    private const double LowestHertz = 15;
    private const double HighestHertz = 48000;
    private const double MostGain = 20;
    private const int HighestType = 10;

    /// <summary>A frame that sets one band's parameter.</summary>
    /// <param name="write">The band, the parameter and its value.</param>
    /// <param name="relayThroughBase">True for a dock or transmitter, false on the wired headset.</param>
    public static byte[] SetFrame(BandWrite write, bool relayThroughBase)
    {
        uint raw = write.Parameter == BandParameter.Type
            ? (uint)write.Value
            : BitConverter.SingleToUInt32Bits((float)write.Value);
        return DspParameter.SetFrame(BandWrites.Id(write.Band, write.Parameter), raw, relayThroughBase);
    }

    /// <summary>A frame that reads one band's parameter.</summary>
    /// <param name="band">The band, counted from zero.</param>
    /// <param name="parameter">What to read.</param>
    /// <param name="relayThroughBase">True for a dock or transmitter, false on the wired headset.</param>
    public static byte[] ReadFrame(int band, BandParameter parameter, bool relayThroughBase) =>
        DspParameter.ReadFrame(BandWrites.Id(band, parameter, toSoundNow: false), relayThroughBase);

    /// <summary>
    /// The value in a reply to <see cref="ReadFrame"/>, or null when the report
    /// is not a reply for that band and parameter or the value is out of range.
    /// </summary>
    /// <param name="report">The input report.</param>
    /// <param name="band">The band, counted from zero.</param>
    /// <param name="parameter">What was read.</param>
    public static double? ValueInReply(ReadOnlySpan<byte> report, int band, BandParameter parameter)
    {
        if (DspParameter.RawInReply(report, BandWrites.Id(band, parameter, toSoundNow: false)) is not { } raw) return null;
        if (parameter == BandParameter.Type) return raw <= HighestType ? raw : null;

        double value = BitConverter.UInt32BitsToSingle(raw);
        bool inRange = parameter switch
        {
            BandParameter.Frequency => value is >= LowestHertz and <= HighestHertz,
            BandParameter.Q => value is >= ParametricBand.LowestQ and <= ParametricBand.HighestQ,
            BandParameter.Gain => Math.Abs(value) <= MostGain,
            _ => false,
        };
        return inRange ? value : null;
    }
}
