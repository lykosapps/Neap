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

    /// <summary>How many times a word that read differently is read again before the difference is believed.</summary>
    public const int Rereads = 3;

    /// <summary>Whether a section read back is what was written, reading again any word that differs.</summary>
    /// <remarks>
    /// A read now and then comes back with its last bytes zeroed: over the cable 2 words in 1,024 on one pass and
    /// none on the next (SP2Tools F320), through the dock about one in a thousand (F147). So a word that differs is
    /// read again, and the section is taken as different only if a word never reads as written.
    /// </remarks>
    /// <param name="address">Where the section starts, on a four-byte boundary.</param>
    /// <param name="written">What was written.</param>
    /// <param name="read">What the first read gave, as long as <paramref name="written"/>.</param>
    /// <param name="readWord">Reads the four bytes at an address again, or null when there is no reply.</param>
    /// <returns>The first address that never read as written, or null when every word did.</returns>
    public static int? FirstDifference(int address, ReadOnlySpan<byte> written, ReadOnlySpan<byte> read, Func<int, byte[]?> readWord)
    {
        for (int at = 0; at < written.Length; at += WordSize)
        {
            if (read.Slice(at, WordSize).SequenceEqual(written.Slice(at, WordSize))) continue;
            bool matched = false;
            for (int again = 0; again < Rereads && !matched; again++)
                matched = readWord(address + at) is { } word && word.AsSpan().SequenceEqual(written.Slice(at, WordSize));
            if (!matched) return address + at;
        }
        return null;
    }

    /// <summary>The addresses of the four-byte reads that cover a run of bytes.</summary>
    public static IEnumerable<int> Words(int address, int length)
    {
        for (int word = address & ~(WordSize - 1); word < address + length; word += WordSize)
            yield return word;
    }
}
