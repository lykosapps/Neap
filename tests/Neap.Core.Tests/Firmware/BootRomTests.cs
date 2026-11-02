using System.Buffers.Binary;
using Neap.Core.Firmware;

namespace Neap.Core.Tests.Firmware;

public class BootRomTests
{
    [Fact]
    public void TheHandshakeIsAnsweredByEachByteTurnedOver() => Assert.True(new BootRom(new FakeLoader()).Handshake());

    [Fact]
    public void AnythingElseOnThePortFailsTheHandshake() => Assert.False(new BootRom(new FakeLoader { Silent = true }).Handshake());

    [Fact]
    public void AReadAsksAsSp2ToolsDoes()
    {
        var loader = new FakeLoader();
        new BootRom(loader).Read32(0x080C9000, 1);
        Assert.Equal(Convert.FromHexString("D1080C900000000001"), loader.Sent[0]);
    }

    [Fact]
    public void AReadGivesFlashInItsOwnOrder()
    {
        // The headset's first word of firmware: 40 01 07 A2 in flash, which SP2Tools' reads print as a2070140.
        var loader = new FakeLoader();
        loader.Flash[0x080C9000] = [0x40, 0x01, 0x07, 0xA2];
        Assert.Equal(new byte[] { 0x40, 0x01, 0x07, 0xA2 }, new BootRom(loader).Read32(0x080C9000, 1));
        Assert.Equal(Convert.FromHexString("A2070140"), loader.Answered.Skip(9 + 2).Take(4));
    }

    [Fact]
    public void AReadThatIsNotEchoedFails() => Assert.Null(new BootRom(new FakeLoader { Silent = true }).Read32(0x080C9000, 1));

    [Fact]
    public void ASectionIsReadInPiecesAndPutTogetherInOrder()
    {
        var loader = new FakeLoader();
        var section = Enumerable.Range(0, FirmwareEdit.SectorSize).Select(i => (byte)(i * 7)).ToArray();
        loader.Flash[0x08232000] = section;

        Assert.Equal(section, new BootRom(loader).ReadSector(0x08232000));
        Assert.Equal(16, loader.Sent.Count(command => command[0] == 0xD1));
    }

    [Fact]
    public void ASectionWithAPieceMissingIsNotGiven()
    {
        var loader = new FakeLoader { FailReadAt = 0x08232800 };
        loader.Flash[0x08232000] = new byte[FirmwareEdit.SectorSize];
        Assert.Null(new BootRom(loader).ReadSector(0x08232000));
    }

    [Fact]
    public void LoadedBytesLandWhereTheyAreSent()
    {
        var loader = new FakeLoader();
        byte[] blob = [1, 2, 3, 4, 5];
        Assert.True(new BootRom(loader).SendDa(0x04010000, blob));
        Assert.Equal(blob, loader.Memory[0x04010000]);
    }

    [Fact]
    public void ALoadTheLoaderRefusesIsSaid() => Assert.False(new BootRom(new FakeLoader { RefuseLoad = true }).SendDa(0x04010000, [1]));

    [Fact]
    public void AJumpStartsTheCodeAtItsAddress()
    {
        var loader = new FakeLoader();
        new BootRom(loader).JumpDa(0x04008001);
        Assert.Equal([0x04008001], loader.Jumps);
    }

    /// <summary>The loader as SP2Tools' tests fake it: echoes, two-byte statuses, and each four bytes of flash read last byte first.</summary>
    private sealed class FakeLoader : IBootRomLink
    {
        public Dictionary<int, byte[]> Flash { get; } = [];
        public Dictionary<int, byte[]> Memory { get; } = [];
        public List<int> Jumps { get; } = [];
        public List<byte[]> Sent { get; } = [];
        public List<byte> Answered { get; } = [];
        public bool Silent { get; init; }
        public bool RefuseLoad { get; init; }
        public int? FailReadAt { get; init; }

        private readonly Queue<byte> _out = new();
        private readonly List<byte> _in = [];
        private int? _loading;
        private int _loadAddress;

        public void Write(ReadOnlySpan<byte> bytes)
        {
            Sent.Add(bytes.ToArray());
            if (Silent) return;
            if (_loading is not null)
            {
                Memory[_loadAddress] = bytes.ToArray();
                _loading = null;
                Answer([0, 0], Status(0));
                return;
            }
            _in.AddRange(bytes.ToArray());
            Take();
        }

        public byte[] Read(int count, TimeSpan quiet)
        {
            var bytes = new List<byte>();
            while (bytes.Count < count && _out.Count > 0) bytes.Add(_out.Dequeue());
            return [.. bytes];
        }

        public void Dispose() { }

        private void Take()
        {
            byte first = _in[0];
            if (_in.Count == 1 && first is 0xA0 or 0x0A or 0x50 or 0x05)
            {
                _in.Clear();
                Answer([(byte)~first]);
            }
            else if (first == 0xD1 && _in.Count == 9)
            {
                var request = _in.ToArray();
                _in.Clear();
                int address = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(1));
                int dwords = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(5));
                Answer(request, Status(0));
                if (FailReadAt is int fail && fail >= address && fail < address + dwords * 4) return;
                var data = Bytes(address, dwords * 4);
                for (int at = 0; at < data.Length; at += 4) data.AsSpan(at, 4).Reverse();
                Answer(data, Status(0));
            }
            else if (first == 0xD7 && _in.Count is 1 or 5 or 9)
                Answer(_in.Count == 1 ? [0xD7] : [.. _in.TakeLast(4)]);
            else if (first == 0xD7 && _in.Count == 13)
            {
                var command = _in.ToArray();
                _in.Clear();
                Answer(command.AsSpan(9, 4).ToArray(), Status(RefuseLoad ? (ushort)0x1D0C : (ushort)0));
                _loadAddress = BinaryPrimitives.ReadInt32BigEndian(command.AsSpan(1));
                _loading = RefuseLoad ? null : BinaryPrimitives.ReadInt32BigEndian(command.AsSpan(5));
            }
            else if (first == 0xD5 && _in.Count == 1)
                Answer([0xD5]);
            else if (first == 0xD5 && _in.Count == 5)
            {
                var command = _in.ToArray();
                _in.Clear();
                Jumps.Add(BinaryPrimitives.ReadInt32BigEndian(command.AsSpan(1)));
                Answer(command.AsSpan(1, 4).ToArray(), Status(0));
            }
        }

        private byte[] Bytes(int address, int length)
        {
            foreach (var (start, bytes) in Flash)
                if (address >= start && address + length <= start + bytes.Length)
                    return bytes.AsSpan(address - start, length).ToArray();
            return new byte[length];
        }

        private void Answer(params byte[][] parts)
        {
            foreach (var part in parts)
                foreach (byte b in part)
                {
                    _out.Enqueue(b);
                    Answered.Add(b);
                }
        }

        private static byte[] Status(ushort value) => [(byte)(value >> 8), (byte)value];
    }
}
