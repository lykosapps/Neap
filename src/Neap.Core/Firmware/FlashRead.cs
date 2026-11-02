using System.Buffers.Binary;
using Neap.Core.Protocol;

namespace Neap.Core.Firmware;

/// <summary>
/// RACE command 0x1680: reads four bytes of the headset's memory, flash
/// included, while it is connected as usual, on the cable or through the dock.
/// </summary>
/// <remarks>
/// <para>
/// It only reads. SP2Tools read the edited sections this way, through the dock
/// and twice over, and found them byte for byte as written (F293), so the
/// firmware edits' state is read from the flash itself.
/// </para>
/// <para>
/// The request is two zero bytes and the address, little-endian. The reply
/// carries a status, two bytes Neap does not use, the address again and the
/// four bytes, in the order they sit in flash.
/// </para>
/// </remarks>
public static class FlashRead
{
    /// <summary>The RACE command id.</summary>
    public const ushort CommandId = 0x1680;

    /// <summary>How many bytes one read gives.</summary>
    public const int WordSize = 4;

    /// <summary>A frame that reads the four bytes at an address.</summary>
    public static byte[] ReadFrame(int address, bool relayThroughBase)
    {
        var payload = new byte[6];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(2), address);
        return Frames.Command(CommandId, payload, relayThroughBase);
    }

    /// <summary>The four bytes in a reply for an address, as a little-endian number, or null when the report is not that reply or the read failed.</summary>
    public static uint? WordInReply(ReadOnlySpan<byte> report, int address)
    {
        var payload = Frames.CommandReply(report, CommandId);
        if (payload.Length != 11 || payload[0] != 0 || BinaryPrimitives.ReadInt32LittleEndian(payload[3..]) != address) return null;
        return BinaryPrimitives.ReadUInt32LittleEndian(payload[7..]);
    }

    /// <summary>The reply the headset gives to a read: the status, two unused bytes, the address and the four bytes.</summary>
    public static byte[] Reply(int address, ReadOnlySpan<byte> word)
    {
        var message = new byte[6 + 11];
        message[0] = Frames.RaceStart;
        message[1] = Frames.RaceReply;
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(2), 13);
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(4), CommandId);
        BinaryPrimitives.WriteInt32LittleEndian(message.AsSpan(9), address);
        word.CopyTo(message.AsSpan(13));
        return message;
    }

    /// <summary>The addresses of the four-byte reads that cover a run of bytes.</summary>
    public static IEnumerable<int> Words(int address, int length)
    {
        for (int word = address & ~(WordSize - 1); word < address + length; word += WordSize)
            yield return word;
    }
}
