using Neap.Core.Firmware;

namespace Neap.Core.Pretend;

/// <summary>The pretend headset's flash, as far as the firmware edits touch it.</summary>
/// <remarks>
/// Each section remembers which edits it holds; a byte an edit changes reads
/// as the edit's where its section holds the edit and as factory where it does
/// not, and every other byte reads as empty flash. The pretend writer changes
/// the sections and the pretend headset answers reads from them, so the
/// Firmware page sees what it would on a headset.
/// </remarks>
public sealed class PretendFlash
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, HashSet<string>> _held = [];

    /// <summary>The edits a section holds.</summary>
    public IReadOnlySet<string> Holds(int sector)
    {
        lock (_gate) return _held.TryGetValue(sector, out var ids) ? ids.ToHashSet() : [];
    }

    /// <summary>Has a section hold exactly these edits.</summary>
    public void Write(int sector, IEnumerable<string> ids)
    {
        lock (_gate) _held[sector] = ids.ToHashSet();
    }

    /// <summary>The four bytes at an address.</summary>
    public byte[] Word(int address)
    {
        var word = new byte[FlashRead.WordSize];
        for (int i = 0; i < word.Length; i++) word[i] = Byte(address + i);
        return word;
    }

    private byte Byte(int address)
    {
        foreach (var edit in FirmwareEdit.All)
            foreach (var change in edit.Changes)
            {
                int at = address - change.Address;
                if (at < 0 || at >= change.Factory.Count) continue;
                bool held = Holds(change.Address & ~(FirmwareEdit.SectorSize - 1)).Contains(edit.Id);
                return (held ? change.Active : change.Factory)[at];
            }
        return 0xFF;
    }
}
