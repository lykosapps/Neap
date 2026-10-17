using System.Buffers.Binary;
using Neap.Core.Protocol;

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
/// This is the one command Neap sends that is not a <c>set_kvp</c> write: it is
/// a RACE command (id 0x000A) carrying a binary payload, and its reply arrives
/// on the input report rather than as a settings notification. The single
/// parameter it addresses is fixed here, so no other command can be sent — the
/// guarantee the settings registry gives <c>set_kvp</c> writes.
/// </para>
/// </remarks>
public static class LoudnessBoost
{
    /// <summary>The RACE command id that reads and sets the level.</summary>
    public const ushort CommandId = 0x000A;

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
    /// +12 dB above the stock -12 reaches 0 dB, the loudest the controls offer;
    /// the firmware itself allows a further 12 dB, left beyond the soft cap.
    /// Raising the level while the equaliser or Superhuman Hearing is also
    /// boosting can clip.
    /// </remarks>
    public const float MaxBoostDb = 12f;

    /// <summary>The step the boost controls take, in decibels.</summary>
    public const float StepDb = 3f;

    private const ushort OpSet = 0;
    private const ushort OpRead = 1;

    /// <summary>A frame that reads the current level.</summary>
    public static byte[] ReadFrame(bool relayThroughBase) =>
        Frames.Command(CommandId, Payload(OpRead, 0f), relayThroughBase);

    /// <summary>A frame that sets the level, held to the firmware's range.</summary>
    public static byte[] SetFrame(float db, bool relayThroughBase) =>
        Frames.Command(CommandId, Payload(OpSet, ClampLevel(db)), relayThroughBase);

    /// <summary>
    /// The level in a reply to <see cref="ReadFrame"/>, or null when the report
    /// is not a valid reply for this command and parameter.
    /// </summary>
    public static float? LevelInReply(ReadOnlySpan<byte> report)
    {
        var payload = Frames.CommandReply(report, CommandId);
        if (payload.Length != 8) return null;
        if (BinaryPrimitives.ReadUInt32BigEndian(payload) != InputLevelParam) return null;
        float db = BinaryPrimitives.ReadSingleBigEndian(payload[4..]);
        return float.IsFinite(db) && db >= MinDb && db <= MaxDb ? db : null;
    }

    /// <summary>A level held to the firmware's range.</summary>
    public static float ClampLevel(float db) => Math.Clamp(db, MinDb, MaxDb);

    /// <summary>The level for a boost of <paramref name="boostDb"/> above stock.</summary>
    public static float LevelForBoost(float boostDb) => ClampLevel(StockDb + boostDb);

    /// <summary>How far a level sits above stock, in decibels.</summary>
    public static float BoostOf(float db) => db - StockDb;

    private static byte[] Payload(ushort op, float db)
    {
        var payload = new byte[10];
        BinaryPrimitives.WriteUInt16BigEndian(payload, op);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(2), InputLevelParam);
        BinaryPrimitives.WriteSingleBigEndian(payload.AsSpan(6), db);
        return payload;
    }
}

/// <summary>The headset's loudness level as the app holds it between reads.</summary>
/// <param name="Available">Whether the headset answered with a valid level; false hides the feature.</param>
/// <param name="LevelDb">The current level, in decibels; meaningful only when <paramref name="Available"/>.</param>
public sealed record LoudnessReading(bool Available, float LevelDb)
{
    /// <summary>How far the level sits above stock, in decibels.</summary>
    public float BoostDb => LoudnessBoost.BoostOf(LevelDb);
}
