using System.Buffers.Binary;

namespace Neap.Core.Firmware;

/// <summary>
/// The small program the chip runs from memory in download mode to write
/// sections of flash: it unlocks the flash, erases and programs each section
/// from bytes loaded beside it, and restarts the chip.
/// </summary>
/// <remarks>
/// <para>
/// It is SP2Tools' <c>restore.s</c>, the program that restored a headset that
/// would not start (F014) and wrote every edit since. Neap cannot assemble, so
/// the program is held as SP2Tools' assembler built it: a fixed start that
/// unlocks the flash, a block per section, and a fixed end with the restart
/// and the routines the blocks call. A block sets the section and where its
/// bytes are, then calls the erase and program routines; the calls are
/// worked out here, because they reach across the blocks. The tests check the
/// result byte for byte against SP2Tools' builds, the proven one included.
/// </para>
/// <para>
/// It is loaded at <see cref="LoadAddress"/> and the sections' bytes at
/// <see cref="PayloadAddress"/>, one after another. Only two sections' worth
/// of memory there is proven, so a program writes at most two.
/// </para>
/// </remarks>
public static class WriteProgram
{
    /// <summary>Where the program is loaded and run from, in the chip's memory.</summary>
    public const int LoadAddress = 0x04008000;

    /// <summary>Where the sections' bytes are loaded, the first at this address and the next straight after it.</summary>
    public const int PayloadAddress = 0x04010000;

    /// <summary>Where flash starts in the chip's address space; the program works in offsets from it.</summary>
    private const int FlashBase = 0x08000000;

    private static readonly byte[] Start = Convert.FromHexString(
        "F0B540F20004CAF2070440F60005CAF2070500220023502028600120002100F0CEF8012028600320002100F0C8F800F0" +
        "52F8502028600120002100F0C0F8312028600220002100F0BAF800F044F8062028600120002100F0B2F8012028600320" +
        "002100F0ACF800F036F8062028600120002100F0A4F8312028600220002100F09EF800F028F8502028600120002100F0" +
        "96F8112028600220002100F090F800F01AF8062028600120002100F088F8982028600120002100F082F800F00CF8");

    private static readonly byte[] End = Convert.FromHexString(
        "4EF60C50CEF2000040F20401C0F2FA510160BFF34F8FFEE740F20007C0F204077F1EFDD170477F1EFDD1704700B50620" +
        "28600120002100F063F809F0FF004FEA00604FEA192101F0FF014FEA014140EA01004FEA194101F0FF014FEA012140EA" +
        "010040F0200028600420002100F048F840F20007C0F26007FFF7D5FF00BD00B540F20008062028600120002100F038F8" +
        "4FEA881606EB090606F0FF004FEA00604FEA162101F0FF014FEA014140EA01004FEA164101F0FF014FEA012140EA0100" +
        "40F0020028604FEA881707EB0A0705F10401102057F8046B41F8046B401EF9D14420002100F00CF840F20007C0F20107" +
        "FFF799FF08F10108B8F1400FC2D100BD20616161002B05D1666806F001064FEA061600E0002621684FF6E070CEF6FF70" +
        "01EA000141F0080141EA060141EA02012160216841F0040121604FF6FF76C0F20806216811F0020F01D1761EF9D14FF6" +
        "FF76C0F20806216811F0010F01D0761EF9D1216840F21400C1F2000021EA000121604FF6FF76C0F20806216811F0020F" +
        "01D0761EF9D1216821F0080121604FF6FF76C0F20806216811F0080F01D0761EF9D17047");

    // Where the routines start in the end part.
    private const int Delay = 0x18, EraseSector = 0x2C, ProgramSector = 0x7E, SfcExec = 0x100;

    // The calls in the start part, by where they are, and the routine each calls.
    private static readonly (int At, int Routine)[] StartCalls =
    [
        (0x1E, SfcExec), (0x2A, SfcExec), (0x2E, Delay), (0x3A, SfcExec), (0x46, SfcExec), (0x4A, Delay),
        (0x56, SfcExec), (0x62, SfcExec), (0x66, Delay), (0x72, SfcExec), (0x7E, SfcExec), (0x82, Delay),
        (0x8E, SfcExec), (0x9A, SfcExec), (0x9E, Delay), (0xAA, SfcExec), (0xB6, SfcExec), (0xBA, Delay),
    ];

    /// <summary>The program that writes these sections, each from its bytes at <see cref="PayloadAddress"/> in turn.</summary>
    /// <param name="sectors">The sections, by flash address, in the order written: one or two.</param>
    public static byte[] For(IReadOnlyList<int> sectors)
    {
        if (sectors.Count is < 1 or > FirmwareEdit.SectorsPerSession)
            throw new ArgumentException($"a program writes one or two sections, not {sectors.Count}", nameof(sectors));
        return Build(sectors.Select((sector, i) => (sector, (int?)(PayloadAddress + i * FirmwareEdit.SectorSize))).ToList());
    }

    /// <summary>The program for these blocks: a section, and where its bytes are, or null for a section only erased.</summary>
    internal static byte[] Build(IReadOnlyList<(int Sector, int? Source)> blocks)
    {
        var program = new List<byte>(Start);
        foreach (var (sector, source) in blocks)
        {
            if (sector % FirmwareEdit.SectorSize != 0 || sector < FlashBase)
                throw new ArgumentException($"0x{sector:X8} is not the start of a section of flash", nameof(blocks));
            program.AddRange(Move(9, sector - FlashBase));
            if (source is int from) program.AddRange(Move(10, from));
            program.AddRange([0, 0, 0, 0]);
            if (source is not null) program.AddRange([0, 0, 0, 0]);
        }
        int end = program.Count;
        program.AddRange(End);
        var bytes = program.ToArray();

        foreach (var (at, routine) in StartCalls) Call(bytes, at, end + routine);
        int place = Start.Length;
        foreach (var (_, source) in blocks)
        {
            place += source is null ? 8 : 16;
            Call(bytes, place, end + EraseSector);
            place += 4;
            if (source is null) continue;
            Call(bytes, place, end + ProgramSector);
            place += 4;
        }
        return bytes;
    }

    /// <summary>MOVW then MOVT: a register set to a 32-bit value, as the Thumb-2 instructions encode it.</summary>
    private static byte[] Move(int register, int value)
    {
        var bytes = new byte[8];
        Wide(bytes.AsSpan(0), 0xF240, register, value & 0xFFFF);
        Wide(bytes.AsSpan(4), 0xF2C0, register, (value >> 16) & 0xFFFF);
        return bytes;
    }

    private static void Wide(Span<byte> into, int opcode, int register, int imm16)
    {
        int first = opcode | ((imm16 >> 11) & 1) << 10 | (imm16 >> 12) & 0xF;
        int second = ((imm16 >> 8) & 7) << 12 | register << 8 | imm16 & 0xFF;
        BinaryPrimitives.WriteUInt16LittleEndian(into, (ushort)first);
        BinaryPrimitives.WriteUInt16LittleEndian(into[2..], (ushort)second);
    }

    /// <summary>BL: a call from one place in the program to another, as the Thumb-2 instruction encodes it.</summary>
    private static void Call(byte[] program, int at, int target)
    {
        int offset = target - (at + 4);
        int s = offset < 0 ? 1 : 0;
        int i1 = (offset >> 23) & 1, i2 = (offset >> 22) & 1;
        int j1 = (~(i1 ^ s)) & 1, j2 = (~(i2 ^ s)) & 1;
        int first = 0xF000 | s << 10 | (offset >> 12) & 0x3FF;
        int second = 0xD000 | j1 << 13 | j2 << 11 | (offset >> 1) & 0x7FF;
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(at), (ushort)first);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(at + 2), (ushort)second);
    }
}
