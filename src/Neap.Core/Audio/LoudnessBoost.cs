namespace Neap.Core.Audio;

/// <summary>
/// The headset's DSP playback input level: how many decibels it applies to
/// everything it plays, before its equaliser and Superhuman Hearing.
/// </summary>
/// <remarks>
/// <para>
/// The Stealth Pro II ships this level at -12 dB, which is the ceiling people
/// hit when the sound is "too quiet at maximum". Raising it lifts every volume
/// position by the same number of decibels. The level is a single float the
/// firmware clamps to [-96, +12] dB, saved on the headset and kept across
/// power cycles.
/// </para>
/// <para>
/// It is not a <c>set_kvp</c> write but RACE command 0x000A, whose reply
/// arrives on the input report rather than as a settings notification; see
/// <see cref="DspParameter"/>. This is the one parameter Neap ever sets with it.
/// </para>
/// </remarks>
public static class LoudnessBoost
{
    /// <summary>The RACE command id that reads and sets the level.</summary>
    public const ushort CommandId = DspParameter.CommandId;

    /// <summary>The parameter the command addresses: NVKey 0x5008, the playback input level.</summary>
    internal const uint InputLevelParam = 0x00040000;

    /// <summary>The level the headset ships with, in decibels.</summary>
    public const float StockDb = -12f;

    /// <summary>The lowest level the firmware accepts.</summary>
    public const float MinDb = -96f;

    /// <summary>The highest level the firmware accepts.</summary>
    public const float MaxDb = 12f;

    /// <summary>The largest boost above stock the controls offer, and the step they take.</summary>
    /// <remarks>
    /// +18 dB above the stock -12 reaches +6 dB, the loudest the controls offer;
    /// the firmware itself allows up to +12 dB, left beyond the cap.
    /// Raising the level while the equaliser or Superhuman Hearing is also
    /// boosting can clip.
    /// </remarks>
    public const float MaxBoostDb = 18f;

    /// <summary>The step the boost controls take, in decibels.</summary>
    public const float StepDb = 3f;

    /// <summary>A frame that reads the current level.</summary>
    public static byte[] ReadFrame(bool relayThroughBase) => DspParameter.ReadFrame(InputLevelParam, relayThroughBase);

    /// <summary>A frame that sets the level, held to the firmware's range.</summary>
    public static byte[] SetFrame(float db, bool relayThroughBase) =>
        DspParameter.SetFrame(InputLevelParam, ClampLevel(db), relayThroughBase);

    /// <summary>
    /// The level in a reply to <see cref="ReadFrame"/>, or null when the report
    /// is not a valid reply for this command and parameter.
    /// </summary>
    public static float? LevelInReply(ReadOnlySpan<byte> report) =>
        DspParameter.LevelInReply(report, InputLevelParam) is float db && db >= MinDb && db <= MaxDb ? db : null;

    /// <summary>A level held to the firmware's range.</summary>
    public static float ClampLevel(float db) => Math.Clamp(db, MinDb, MaxDb);

    /// <summary>The level for a boost of <paramref name="boostDb"/> above stock.</summary>
    public static float LevelForBoost(float boostDb) => ClampLevel(StockDb + boostDb);

    /// <summary>How far a level sits above stock, in decibels.</summary>
    public static float BoostOf(float db) => db - StockDb;
}

/// <summary>The headset's loudness level as the app holds it between reads.</summary>
/// <param name="Available">Whether the headset answered with a valid level; false hides the feature.</param>
/// <param name="LevelDb">The current level, in decibels; meaningful only when <paramref name="Available"/>.</param>
public sealed record LoudnessReading(bool Available, float LevelDb)
{
    /// <summary>How far the level sits above stock, in decibels.</summary>
    public float BoostDb => LoudnessBoost.BoostOf(LevelDb);
}
