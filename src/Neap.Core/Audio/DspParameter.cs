using System.Buffers.Binary;
using Neap.Core.Protocol;

namespace Neap.Core.Audio;

/// <summary>
/// RACE command 0x000A: reads or sets one of the headset's stored sound
/// processing parameters, by its id, as a level in decibels.
/// </summary>
/// <remarks>
/// Each parameter Neap uses has a class of its own that names its id
/// (<see cref="LoudnessBoost"/>, <see cref="Sidetone"/>), so no other
/// parameter is ever sent, the guarantee the settings registry gives
/// <c>set_kvp</c> writes.
/// </remarks>
internal static class DspParameter
{
    /// <summary>The RACE command id.</summary>
    public const ushort CommandId = 0x000A;

    private const ushort SetOp = 0;
    private const ushort ReadOp = 1;

    /// <summary>A frame that reads a parameter's stored level.</summary>
    public static byte[] ReadFrame(uint parameter, bool relayThroughBase) =>
        Frames.Command(CommandId, Payload(ReadOp, parameter, 0f), relayThroughBase);

    /// <summary>A frame that sets a parameter's level, which the headset keeps.</summary>
    public static byte[] SetFrame(uint parameter, float db, bool relayThroughBase) =>
        Frames.Command(CommandId, Payload(SetOp, parameter, db), relayThroughBase);

    /// <summary>A frame that sets a parameter to a raw 32-bit value, for one that is not a float.</summary>
    public static byte[] SetFrame(uint parameter, uint raw, bool relayThroughBase) =>
        Frames.Command(CommandId, Payload(SetOp, parameter, raw), relayThroughBase);

    /// <summary>The level in a reply for a parameter, or null when the report is not one.</summary>
    public static float? LevelInReply(ReadOnlySpan<byte> report, uint parameter)
    {
        if (RawInReply(report, parameter) is not { } raw) return null;
        float db = BitConverter.UInt32BitsToSingle(raw);
        return float.IsFinite(db) ? db : null;
    }

    /// <summary>The raw 32-bit value in a reply for a parameter, or null when the report is not one.</summary>
    public static uint? RawInReply(ReadOnlySpan<byte> report, uint parameter)
    {
        var payload = Frames.CommandReply(report, CommandId);
        if (payload.Length != 8 || BinaryPrimitives.ReadUInt32BigEndian(payload) != parameter) return null;
        return BinaryPrimitives.ReadUInt32BigEndian(payload[4..]);
    }

    private static byte[] Payload(ushort op, uint parameter, float db) =>
        Payload(op, parameter, BitConverter.SingleToUInt32Bits(db));

    private static byte[] Payload(ushort op, uint parameter, uint raw)
    {
        var payload = new byte[10];
        BinaryPrimitives.WriteUInt16BigEndian(payload, op);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(2), parameter);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(6), raw);
        return payload;
    }
}
