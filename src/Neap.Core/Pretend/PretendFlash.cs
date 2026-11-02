using System.Security.Cryptography;
using Neap.Core.Firmware;

namespace Neap.Core.Pretend;

/// <summary>The pretend headset's flash, as far as the firmware edits touch it.</summary>
/// <remarks>
/// <para>
/// It holds whole sections, so the pretend run is written, read back and
/// checked as a headset is. Neap carries no factory firmware, so the pretend
/// factory sections are made up: filler, with each edit's factory bytes in
/// their places, and empty flash for the section of new code. Their hashes
/// stand in for <see cref="FirmwareEdit.FactorySections"/>.
/// </para>
/// <para>
/// The pretend writer changes the sections and the pretend headset answers
/// flash reads from them, so the Firmware page sees what it would on a headset.
/// </para>
/// </remarks>
public sealed class PretendFlash
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, byte[]> _sections;

    public PretendFlash()
    {
        _sections = FirmwareEdit.FactorySections.Keys.ToDictionary(sector => sector, Factory);
        FactorySections = _sections.ToDictionary(pair => pair.Key, pair => Convert.ToHexString(SHA256.HashData(pair.Value)));
    }

    /// <summary>The SHA-256 of each pretend factory section.</summary>
    public IReadOnlyDictionary<int, string> FactorySections { get; }

    /// <summary>A whole section as it is now.</summary>
    public byte[] Read(int sector)
    {
        lock (_gate) return _sections.TryGetValue(sector, out var bytes) ? bytes.ToArray() : Empty();
    }

    /// <summary>Has a section hold these bytes.</summary>
    public void Write(int sector, ReadOnlySpan<byte> bytes)
    {
        lock (_gate) _sections[sector] = bytes.ToArray();
    }

    /// <summary>The four bytes at an address.</summary>
    public byte[] Word(int address)
    {
        int sector = address & ~(FirmwareEdit.SectorSize - 1);
        return Read(sector).AsSpan(address - sector, FlashRead.WordSize).ToArray();
    }

    private static byte[] Factory(int sector)
    {
        bool code = FirmwareEdit.All.SelectMany(edit => edit.Changes).Any(change => change.Address == sector && change.Factory.All(b => b == 0xFF));
        var bytes = code ? Empty() : Enumerable.Range(0, FirmwareEdit.SectorSize).Select(i => (byte)(i * 31 + (sector >> 12))).ToArray();
        foreach (var change in FirmwareEdit.All.SelectMany(edit => edit.Changes).Where(change => (change.Address & ~(FirmwareEdit.SectorSize - 1)) == sector))
            change.Factory.ToArray().CopyTo(bytes, change.Address - sector);
        return bytes;
    }

    private static byte[] Empty() => Enumerable.Repeat((byte)0xFF, FirmwareEdit.SectorSize).ToArray();
}
