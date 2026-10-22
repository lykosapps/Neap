using System.Buffers.Binary;
using Neap.Core.Protocol;

namespace Neap.Core.Hid;

/// <summary>
/// The transmitter's light level as the transmitter itself gives it, for a
/// headset whose settings read gives no answer for its light.
/// </summary>
/// <remarks>
/// <para>
/// The Atlas Air's transmitter has no settings of its own to read: its light
/// level is kept by the headset, which never answers for it, so Neap had no
/// reading at start. The transmitter answers RACE command 0x0006, operation 3,
/// with the level it is showing, 0 to 100 (SP2Tools F116). It is read only:
/// operation 2 of the same command sets the level and is never sent, as
/// writes go through the settings registry.
/// </para>
/// </remarks>
public static class TransmitterLight
{
    /// <summary>The RACE command id.</summary>
    public const ushort CommandId = 0x0006;

    private const byte ReadLevel = 3;

    /// <summary>The request, answered by the transmitter rather than relayed to the headset.</summary>
    public static byte[] ReadFrame => Frames.Command(CommandId, [ReadLevel], relayThroughBase: false);

    /// <summary>The level in a reply to <see cref="ReadFrame"/>, or null when the report is not one, or holds no level.</summary>
    public static int? LevelInReply(ReadOnlySpan<byte> report)
    {
        var payload = Frames.CommandReply(report, CommandId);
        if (payload.Length != 4) return null;
        uint level = BinaryPrimitives.ReadUInt32LittleEndian(payload);
        return level <= 100 ? (int)level : null;
    }
}
