using System.Text;
using Neap.Core.Firmware;

namespace Neap.Services;

/// <summary>How a section's write ended, as the headset said after reading it back.</summary>
public enum WriteResult
{
    /// <summary>What was read back is what was written.</summary>
    Verified,

    /// <summary>It did not match, and the headset put stock back itself before restarting.</summary>
    Reverted,
}

/// <summary>What writes sections of a headset's firmware, in its download mode.</summary>
/// <remarks>
/// <para>
/// Pretend mode has <see cref="PretendFirmwareWriter"/>. A writer for a real
/// headset is still to come, ported from SP2Tools' flasher; until then a real
/// headset has none, and nothing is written to it.
/// </para>
/// <para>
/// A writer does what it is asked and throws when it cannot.
/// <see cref="FirmwareEditService"/> decides the order and what is written.
/// </para>
/// </remarks>
public interface IFirmwareWriter
{
    /// <summary>The mark the edits leave, one bit per edit, read while the headset is connected as usual, or null where it leaves none.</summary>
    Task<int?> ReadMark();

    /// <summary>Waits until the headset is plugged in in download mode.</summary>
    Task WaitForDownloadMode(CancellationToken cancel);

    /// <summary>Reaches the headset in download mode, and gives the firmware version on its chip.</summary>
    Task<string> Connect();

    /// <summary>Saves a section as it is now, and gives the file it went to.</summary>
    Task<string> Backup(int sector, string folder);

    /// <summary>Writes a section as stock with these edits in it.</summary>
    /// <param name="sector">The section, by address.</param>
    /// <param name="held">The edits the section holds once written; everything else in it is stock.</param>
    /// <param name="percent">Told how much is written, from 0 to 100.</param>
    Task Write(int sector, IReadOnlyList<FirmwareEdit> held, IProgress<int> percent);

    /// <summary>Has the headset read back the section it last wrote, and put stock back itself if it does not match.</summary>
    Task<WriteResult> Verify(int sector);

    /// <summary>Writes a section back as a backup holds it, and verifies it the same way.</summary>
    Task<WriteResult> Restore(string backup, IProgress<int> percent);

    /// <summary>Waits until the headset is back, connected as usual.</summary>
    Task WaitForHeadset(CancellationToken cancel);
}

/// <summary>The pretend headset's firmware: the whole flow, with a pause at each step, and nothing real written.</summary>
/// <remarks>
/// Each section remembers which edits it holds, and an edit is on once every
/// section it touches holds it, so a plan cut short shows as it would on a
/// headset. The person plugging the headset in is a short wait.
/// </remarks>
/// <param name="build">The firmware version its chip holds.</param>
/// <param name="reverts">Whether every write fails its read back, so the headset's own revert can be seen.</param>
public sealed class PretendFirmwareWriter(string build, bool reverts) : IFirmwareWriter
{
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan Plugging = TimeSpan.FromSeconds(3);

    private readonly Dictionary<int, HashSet<string>> _held = [];
    private (int Sector, IReadOnlyList<FirmwareEdit> Held)? _written;

    public Task<int?> ReadMark() => Task.FromResult<int?>(FirmwareEdit.MarkOf(
        FirmwareEdit.All.Where(edit => edit.Sectors.All(sector => Holds(sector).Contains(edit.Id)))));

    public Task WaitForDownloadMode(CancellationToken cancel) => Task.Delay(Plugging, cancel);

    public async Task<string> Connect()
    {
        await Task.Delay(Pause);
        return build;
    }

    public async Task<string> Backup(int sector, string folder)
    {
        await Task.Delay(Pause);
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, FirmwareBackup.FileName(DateTime.Now, sector, build));
        await File.WriteAllTextAsync(file, string.Join('\n', Holds(sector)), Encoding.UTF8);
        return file;
    }

    public async Task Write(int sector, IReadOnlyList<FirmwareEdit> held, IProgress<int> percent)
    {
        for (int done = 0; done <= 100; done += 10)
        {
            percent.Report(done);
            await Task.Delay(Pause / 6);
        }
        _written = (sector, held);
    }

    public async Task<WriteResult> Verify(int sector)
    {
        await Task.Delay(Pause);
        if (reverts || _written is not { } written || written.Sector != sector) return WriteResult.Reverted;
        _held[sector] = written.Held.Select(edit => edit.Id).ToHashSet();
        return WriteResult.Verified;
    }

    public async Task<WriteResult> Restore(string backup, IProgress<int> percent)
    {
        var saved = FirmwareBackup.Of(backup) ?? throw new InvalidDataException($"{Path.GetFileName(backup)} is not a backup Neap made");
        var ids = (await File.ReadAllTextAsync(backup)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        await Write(saved.Sector, FirmwareEdit.All.Where(edit => ids.Contains(edit.Id)).ToList(), percent);
        return await Verify(saved.Sector);
    }

    public Task WaitForHeadset(CancellationToken cancel) => Task.Delay(Plugging, cancel);

    private HashSet<string> Holds(int sector) => _held.TryGetValue(sector, out var ids) ? ids : [];
}
