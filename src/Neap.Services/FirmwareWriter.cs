using Neap.Core.Firmware;

namespace Neap.Services;

/// <summary>What reads and writes the parts of a headset's firmware that the edits change.</summary>
/// <remarks>
/// <para>
/// Pretend mode has <see cref="PretendFirmwareWriter"/>. A writer for a real
/// headset is still to come, ported from SP2Tools' flasher; until then a real
/// headset has none, and nothing is written to it.
/// </para>
/// <para>
/// A writer does what it is asked and throws when it cannot.
/// <see cref="FirmwareEditService"/> decides the order and when to undo.
/// </para>
/// </remarks>
public interface IFirmwareWriter
{
    /// <summary>The mark the edits leave, one bit per edit, or null where the headset leaves none.</summary>
    Task<int?> ReadMark();

    /// <summary>Saves what an edit changes as it is now, and gives the file it went to.</summary>
    Task<string> Backup(FirmwareEdit edit, string folder);

    /// <summary>Writes an edit, or the stock bytes it replaces.</summary>
    Task Write(FirmwareEdit edit, bool install);

    /// <summary>Whether what is on the headset now is what <see cref="Write"/> put there.</summary>
    Task<bool> ReadBack(FirmwareEdit edit, bool install);

    /// <summary>Restarts the headset, so it runs what was written.</summary>
    Task Restart();

    /// <summary>Puts back what a backup holds.</summary>
    Task Restore(string backup);
}

/// <summary>The pretend headset's firmware: the whole flow, with a pause at each step, and nothing real written.</summary>
/// <param name="fails">Whether the write fails, so the undo can be seen.</param>
public sealed class PretendFirmwareWriter(bool fails) : IFirmwareWriter
{
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(700);

    private int _mark;

    public Task<int?> ReadMark() => Task.FromResult<int?>(_mark);

    public async Task<string> Backup(FirmwareEdit edit, string folder)
    {
        await Task.Delay(Pause);
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, FirmwareBackup.FileName(DateTime.Now, edit));
        await File.WriteAllBytesAsync(file, BitConverter.GetBytes(_mark));
        return file;
    }

    public async Task Write(FirmwareEdit edit, bool install)
    {
        await Task.Delay(Pause);
        if (fails) throw new IOException("the pretend headset's write failed, as --firmware-fails asks");
        _mark = install ? _mark | 1 << edit.Bit : _mark & ~(1 << edit.Bit);
    }

    public async Task<bool> ReadBack(FirmwareEdit edit, bool install)
    {
        await Task.Delay(Pause);
        return true;
    }

    public Task Restart() => Task.Delay(Pause * 2);

    public async Task Restore(string backup)
    {
        await Task.Delay(Pause);
        _mark = BitConverter.ToInt32(await File.ReadAllBytesAsync(backup));
    }
}
