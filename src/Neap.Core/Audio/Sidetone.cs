namespace Neap.Core.Audio;

/// <summary>
/// The gain the headset stores for mic monitoring, read back to see what a
/// change to the monitoring level did.
/// </summary>
/// <remarks>
/// <para>
/// On the Atlas Air the monitoring level is stored as a gain in decibels,
/// -30 + 0.3 per step from 1 to 100, and -96 when off (SP2Tools F120). A
/// write to the level is accepted even where the headset has nowhere to keep
/// it, and then changes nothing, so the level alone cannot show whether it
/// took. Reading the stored gain back does.
/// </para>
/// <para>
/// It is read only: <see cref="DspParameter"/>'s set is never sent for this
/// parameter. The reading is what the headset stores, not what its sound
/// processing is playing, so it shows that the level was taken, not how loud
/// monitoring sounds.
/// </para>
/// </remarks>
public static class Sidetone
{
    /// <summary>The parameter: NVKey 0x5030, the sidetone gain.</summary>
    internal const uint GainParam = 0x03020000;

    /// <summary>The quietest gain the headset stores: off.</summary>
    public const float OffDb = -96f;

    /// <summary>The loudest gain the stage takes.</summary>
    public const float MaxDb = 12f;

    /// <summary>A frame that reads the stored gain.</summary>
    public static byte[] ReadFrame(bool relayThroughBase) => DspParameter.ReadFrame(GainParam, relayThroughBase);

    /// <summary>The gain in a reply to <see cref="ReadFrame"/>, or null when the report is not one.</summary>
    public static float? GainInReply(ReadOnlySpan<byte> report) =>
        DspParameter.LevelInReply(report, GainParam) is float db && db >= OffDb && db <= MaxDb ? db : null;

    /// <summary>The gain the headset stores for a monitoring level, from 0 to 100.</summary>
    public static float GainFor(int level) => level <= 0 ? OffDb : -30f + 0.3f * Math.Min(level, 100);
}
