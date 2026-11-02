using System.Buffers.Binary;

namespace Neap.Core.Firmware;

/// <summary>A serial connection to the headset's chip in download mode.</summary>
public interface IBootRomLink : IDisposable
{
    /// <summary>Sends bytes.</summary>
    void Write(ReadOnlySpan<byte> bytes);

    /// <summary>Reads exactly this many bytes, or fewer when they stop coming for the time given.</summary>
    byte[] Read(int count, TimeSpan quiet);
}

/// <summary>
/// The headset chip's download mode: the loader in its ROM that answers on a
/// USB serial port, below the firmware, and can always be reached.
/// </summary>
/// <remarks>
/// <para>
/// MediaTek's legacy loader protocol, as SP2Tools' <c>brom_stub.py</c> speaks
/// it; that is what restored a headset that would not start (F014) and wrote
/// every firmware edit since. Each byte sent is echoed, numbers are
/// big-endian, and a status is two bytes, where anything above 0xFF is an
/// error.
/// </para>
/// <para>
/// A read gives each four bytes of flash last byte first, so they are turned
/// round here and a caller sees flash in its own order.
/// </para>
/// <para>
/// The loader answers its port for about half a second after the chip powers
/// up, and for about 30 seconds after a handshake (F290), so a caller
/// handshakes the moment the port appears.
/// </para>
/// </remarks>
/// <param name="link">The serial connection.</param>
public sealed class BootRom(IBootRomLink link) : IDisposable
{
    /// <summary>The USB ids the chip shows in download mode.</summary>
    public const ushort VendorId = 0x0E8D, ProductId = 0x0003;

    /// <summary>The speed of the serial line.</summary>
    public const int BaudRate = 115200;

    private const byte Read32Command = 0xD1, SendDaCommand = 0xD7, JumpDaCommand = 0xD5;
    private const int DwordsPerRead = 64;
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(2);
    private static readonly byte[] Greeting = [0xA0, 0x0A, 0x50, 0x05];

    /// <summary>Greets the loader, and says whether it answered as it should.</summary>
    public bool Handshake()
    {
        foreach (byte said in Greeting)
        {
            link.Write([said]);
            var answer = link.Read(1, TimeSpan.FromMilliseconds(100));
            if (answer.Length != 1 || answer[0] != (byte)~said) return false;
        }
        return true;
    }

    /// <summary>Reads flash, in its own order, or null when the loader does not echo the request or the bytes do not all come.</summary>
    /// <remarks>The two statuses around the data are read and, as in SP2Tools' proven runs, not judged.</remarks>
    /// <param name="address">Where to start, on a four-byte boundary.</param>
    /// <param name="dwords">How many four-byte words.</param>
    public byte[]? Read32(int address, int dwords)
    {
        var request = new byte[9];
        request[0] = Read32Command;
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(1), address);
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(5), dwords);
        link.Write(request);
        if (!link.Read(9, Quiet).AsSpan().SequenceEqual(request)) return null;
        link.Read(2, Quiet);
        var data = link.Read(dwords * 4, Quiet);
        link.Read(2, Quiet);
        if (data.Length != dwords * 4) return null;
        for (int at = 0; at < data.Length; at += 4) data.AsSpan(at, 4).Reverse();
        return data;
    }

    /// <summary>Reads a whole section of flash, or null when any part of it does not come.</summary>
    public byte[]? ReadSector(int sector)
    {
        var section = new byte[FirmwareEdit.SectorSize];
        for (int at = 0; at < section.Length; at += DwordsPerRead * 4)
        {
            if (Read32(sector + at, DwordsPerRead) is not { } part) return null;
            part.CopyTo(section, at);
        }
        return section;
    }

    /// <summary>Loads bytes into the chip's memory, and says whether the loader took them.</summary>
    /// <remarks>
    /// The command, address, length and signature length are each echoed; the
    /// echoes are read but, as in SP2Tools' proven runs, only the two statuses
    /// decide. Whether the bytes arrived whole is shown only by what is
    /// written with them, so every write is read back.
    /// </remarks>
    /// <param name="address">Where in memory.</param>
    /// <param name="blob">The bytes.</param>
    public bool SendDa(int address, ReadOnlySpan<byte> blob)
    {
        Say([SendDaCommand]);
        Say(BigEndian(address));
        Say(BigEndian(blob.Length));
        Say(BigEndian(0));
        if (!StatusOk()) return false;
        link.Write(blob);
        link.Read(2, Quiet);
        return StatusOk();
    }

    /// <summary>Starts code loaded with <see cref="SendDa"/>.</summary>
    /// <remarks>
    /// The code runs once the loader has answered and the port goes when it
    /// restarts the chip, so the answer may be cut short; nothing in it is
    /// relied on, and the flash is read back instead.
    /// </remarks>
    /// <param name="address">Where the code starts, with the low bit set for Thumb code.</param>
    public void JumpDa(int address)
    {
        Say([JumpDaCommand]);
        Say(BigEndian(address));
        link.Read(2, TimeSpan.FromMilliseconds(600));
    }

    /// <summary>Closes the serial connection.</summary>
    public void Dispose() => link.Dispose();

    /// <summary>Sends bytes and takes in their echo.</summary>
    private void Say(byte[] bytes)
    {
        link.Write(bytes);
        link.Read(bytes.Length, Quiet);
    }

    private bool StatusOk()
    {
        var status = link.Read(2, Quiet);
        return status.Length == 2 && BinaryPrimitives.ReadUInt16BigEndian(status) <= 0xFF;
    }

    private static byte[] BigEndian(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return bytes;
    }
}
