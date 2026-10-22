using System.Globalization;
using Neap.Core;
using Neap.Core.Connection;
using Neap.Core.Firmware;

namespace Neap.Services;

/// <summary>How an install, a removal or a restore ended.</summary>
/// <param name="FailedAt">The step that failed, or null if every step finished.</param>
/// <param name="Undone">Whether a failure was undone from the backup.</param>
/// <param name="Backup">The backup made before writing, or null if none was.</param>
public sealed record EditOutcome(EditStep? FailedAt, bool Undone, string? Backup)
{
    /// <summary>Gets whether every step finished.</summary>
    public bool Done => FailedAt is null;
}

/// <summary>A backup saved before an edit was written.</summary>
/// <param name="File">Where it is.</param>
/// <param name="Taken">When it was made.</param>
/// <param name="Edit">The edit it was made for, by its id.</param>
/// <param name="FirmwareBuild">The firmware it was taken from.</param>
public sealed record FirmwareBackup(string File, DateTime Taken, string Edit, string FirmwareBuild)
{
    private const string Stamp = "yyyy-MM-dd HH.mm.ss";

    /// <summary>The file name a backup is saved under, which says what it holds.</summary>
    public static string FileName(DateTime taken, FirmwareEdit edit) =>
        $"{taken.ToString(Stamp, CultureInfo.InvariantCulture)} {edit.Id} {edit.FirmwareBuild}.bin";

    /// <summary>The backup a file holds, or null for a file not named by <see cref="FileName"/>.</summary>
    public static FirmwareBackup? Of(string file)
    {
        var parts = Path.GetFileNameWithoutExtension(file).Split(' ');
        return parts.Length == 4
            && DateTime.TryParseExact($"{parts[0]} {parts[1]}", Stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var taken)
            ? new FirmwareBackup(file, taken, parts[2], parts[3])
            : null;
    }
}

/// <summary>
/// Firmware edits on the connected headset: which fit it, which are on, and
/// installing, removing and restoring them.
/// </summary>
/// <remarks>
/// <para>
/// The rules are <see cref="FirmwareEdit"/>'s. This runs the steps in order
/// on the headset's <see cref="IFirmwareWriter"/>, undoes a failure from the
/// backup, and says each step in the app log, so a recording shows exactly
/// what was written and what was put back.
/// </para>
/// <para>
/// A headset with no writer can be looked at but not changed: its edits are
/// listed, and <see cref="CanWrite"/> is false.
/// </para>
/// </remarks>
public sealed class FirmwareEditService(HeadsetService headset, IFirmwareWriter? writer)
{
    /// <summary>The mark, a step or a backup changed.</summary>
    public event Action? Changed;

    /// <summary>Gets whether this headset's firmware can be written by this build of Neap.</summary>
    public bool CanWrite => writer is not null;

    /// <summary>Gets whether an install, a removal or a restore is under way.</summary>
    public bool Busy { get; private set; }

    /// <summary>Gets the mark last read from the headset, or null where it leaves none.</summary>
    public int? Mark { get; private set; }

    /// <summary>Gets the firmware version the headset reports, or null before it has.</summary>
    public string? Build => headset.GetText("firmware_version");

    /// <summary>Gets the edits made for the connected headset.</summary>
    public IReadOnlyList<FirmwareEdit> Edits => FirmwareEdit.For(headset.Model);

    /// <summary>Gets what Neap knows of the headset for writing.</summary>
    /// <remarks>
    /// Swarm II is not looked for yet: its program's name is not known, and
    /// nothing is written to a real headset until it is.
    /// </remarks>
    public EditReadiness Readiness => new(
        headset.Status.Link == Link.Connected,
        Build,
        headset.TryGetNumberByKey(Battery.Key, out int battery) ? battery : null,
        SwarmRunning: false);

    /// <summary>Gets the folder backups are saved in.</summary>
    public static string BackupFolder => Path.Combine(AppFolder.Path, "Firmware backups");

    /// <summary>Gets the backups saved so far, newest first.</summary>
    public static IReadOnlyList<FirmwareBackup> Backups => Directory.Exists(BackupFolder)
        ? Directory.EnumerateFiles(BackupFolder, "*.bin")
            .Select(FirmwareBackup.Of)
            .OfType<FirmwareBackup>()
            .OrderByDescending(backup => backup.Taken)
            .ToList()
        : [];

    /// <summary>Whether an edit is on the connected headset.</summary>
    public EditStatus StatusOf(FirmwareEdit edit) => edit.StatusOn(Build, Mark);

    /// <summary>Reads the mark again.</summary>
    public async Task ReadMark()
    {
        Mark = writer is null ? null : await writer.ReadMark();
        Changed?.Invoke();
    }

    /// <summary>Installs an edit, or removes it, one step at a time.</summary>
    /// <param name="edit">The edit.</param>
    /// <param name="install">True to install it, false to put back the stock bytes.</param>
    /// <param name="starting">Told each step as it starts, so it can be shown as it happens.</param>
    public async Task<EditOutcome> Run(FirmwareEdit edit, bool install, Action<EditStep> starting)
    {
        if (writer is null) throw new InvalidOperationException("this headset's firmware cannot be written by this build");
        string doing = install ? "installing" : "removing";
        Busy = true;
        Changed?.Invoke();
        string? backup = null;
        var step = EditStep.Check;
        try
        {
            foreach (var next in FirmwareEdit.Steps)
            {
                step = next;
                starting(step);
                AppLog.Write($"firmware edit: {doing} {edit.Id}, {Words(step)}");
                switch (step)
                {
                    case EditStep.Check:
                        var blockers = edit.Blockers(Readiness);
                        if (blockers.Count > 0)
                            throw new InvalidOperationException($"not ready: {string.Join(", ", blockers)}");
                        break;
                    case EditStep.Backup:
                        backup = await writer.Backup(edit, BackupFolder);
                        AppLog.Write($"firmware edit: backed up to {Path.GetFileName(backup)}");
                        break;
                    case EditStep.Write:
                        await writer.Write(edit, install);
                        break;
                    case EditStep.ReadBack:
                        if (!await writer.ReadBack(edit, install))
                            throw new InvalidDataException("what was read back is not what was written");
                        break;
                    case EditStep.Restart:
                        await writer.Restart();
                        break;
                    case EditStep.Confirm:
                        Mark = await writer.ReadMark();
                        var wanted = install ? EditStatus.Installed : EditStatus.NotInstalled;
                        if (StatusOf(edit) != wanted)
                            throw new InvalidDataException($"the headset's mark says {StatusOf(edit)} after the restart");
                        break;
                }
            }
            AppLog.Write($"firmware edit: {edit.Id} {(install ? "installed" : "removed")} and confirmed");
            return new EditOutcome(null, false, backup);
        }
        catch (Exception ex)
        {
            AppLog.Write($"firmware edit: {doing} {edit.Id} failed while {Words(step)}: {ex.Message}");
            bool undone = FirmwareEdit.UndoAfter(step) && backup is not null && await Undo(backup);
            return new EditOutcome(step, undone, backup);
        }
        finally
        {
            Mark = await writer.ReadMark();
            Busy = false;
            Changed?.Invoke();
        }
    }

    /// <summary>Puts back what a backup holds, for a headset an edit has left unwell.</summary>
    /// <returns>Whether it was put back.</returns>
    public async Task<bool> Restore(FirmwareBackup backup)
    {
        if (writer is null) throw new InvalidOperationException("this headset's firmware cannot be written by this build");
        Busy = true;
        Changed?.Invoke();
        try
        {
            AppLog.Write($"firmware edit: restoring {Path.GetFileName(backup.File)}");
            bool done = await Undo(backup.File);
            Mark = await writer.ReadMark();
            return done;
        }
        finally
        {
            Busy = false;
            Changed?.Invoke();
        }
    }

    private async Task<bool> Undo(string backup)
    {
        try
        {
            await writer!.Restore(backup);
            await writer.Restart();
            AppLog.Write($"firmware edit: put back {Path.GetFileName(backup)}");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Write($"firmware edit: could not put back {Path.GetFileName(backup)}: {ex.Message}");
            return false;
        }
    }

    private static string Words(EditStep step) => step switch
    {
        EditStep.Check => "checking the headset",
        EditStep.Backup => "backing up",
        EditStep.Write => "writing",
        EditStep.ReadBack => "reading back",
        EditStep.Restart => "restarting the headset",
        _ => "confirming",
    };
}
