using System.Globalization;
using Neap.Core;
using Neap.Core.Connection;
using Neap.Core.Firmware;

namespace Neap.Services;

/// <summary>How applying the edits, or restoring a backup, ended.</summary>
public enum ApplyResult
{
    /// <summary>Every section was written and verified, and the mark shows the edits wanted.</summary>
    Done,

    /// <summary>Every section was written and verified, but the headset leaves no mark to confirm it by.</summary>
    Unconfirmed,

    /// <summary>The person stopped while Neap was waiting for the headset in download mode.</summary>
    Cancelled,

    /// <summary>The firmware on the chip is not the build the edits are made for, so nothing was written.</summary>
    Refused,

    /// <summary>A section did not read back as written, and the headset put stock back in it itself.</summary>
    Reverted,

    /// <summary>A step failed for another reason; the app log says which.</summary>
    Failed,
}

/// <summary>Where applying has got to.</summary>
/// <param name="Step">The step under way.</param>
/// <param name="Session">Which session in download mode, from 1, or 0 before the first.</param>
/// <param name="Sessions">How many sessions the plan needs.</param>
/// <param name="Percent">How much of the section is written, during <see cref="EditStep.Write"/>.</param>
public sealed record ApplyProgress(EditStep Step, int Session, int Sessions, int? Percent);

/// <summary>How applying ended, and where.</summary>
/// <param name="Result">How it ended.</param>
/// <param name="Stopped">The step it stopped at, or null if every step finished.</param>
/// <param name="Backups">The backups made on the way.</param>
public sealed record ApplyOutcome(ApplyResult Result, EditStep? Stopped, IReadOnlyList<string> Backups);

/// <summary>A section of firmware saved before it was written.</summary>
/// <param name="File">Where it is.</param>
/// <param name="Taken">When it was made.</param>
/// <param name="Sector">The section, by address.</param>
/// <param name="FirmwareBuild">The firmware it was taken from.</param>
public sealed record FirmwareBackup(string File, DateTime Taken, int Sector, string FirmwareBuild)
{
    private const string Stamp = "yyyy-MM-dd HH.mm.ss";

    /// <summary>The file name a backup is saved under, which says what it holds.</summary>
    public static string FileName(DateTime taken, int sector, string build) =>
        string.Create(CultureInfo.InvariantCulture, $"{taken.ToString(Stamp, CultureInfo.InvariantCulture)} {sector:X8} {build}.bin");

    /// <summary>The backup a file holds, or null for a file not named by <see cref="FileName"/>.</summary>
    public static FirmwareBackup? Of(string file)
    {
        var parts = Path.GetFileNameWithoutExtension(file).Split(' ');
        return parts.Length == 4
            && DateTime.TryParseExact($"{parts[0]} {parts[1]}", Stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var taken)
            && int.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int sector)
            ? new FirmwareBackup(file, taken, sector, parts[3])
            : null;
    }
}

/// <summary>
/// Firmware edits on the connected headset: which fit it, which are on, and
/// applying the ones ticked.
/// </summary>
/// <remarks>
/// <para>
/// The rules are <see cref="FirmwareEdit"/>'s. This runs the plan on the
/// headset's <see cref="IFirmwareWriter"/> a session at a time, says each step
/// in the app log, so a recording shows exactly what was written, and stops at
/// the first step that fails. The plan's order leaves a headset that starts
/// wherever it stops.
/// </para>
/// <para>
/// A headset with no writer can be looked at but not changed: its edits are
/// listed, and <see cref="CanWrite"/> is false.
/// </para>
/// </remarks>
public sealed class FirmwareEditService(HeadsetService headset, IFirmwareWriter? writer)
{
    /// <summary>The mark or the running state changed.</summary>
    public event Action? Changed;

    /// <summary>Gets whether this headset's firmware can be written by this build of Neap.</summary>
    public bool CanWrite => writer is not null;

    /// <summary>Gets whether applying or a restore is under way.</summary>
    public bool Busy { get; private set; }

    /// <summary>Gets the mark last read from the headset, or null where it leaves none.</summary>
    public int? Mark { get; private set; }

    /// <summary>Gets the firmware version the headset reports, or null before it has.</summary>
    public string? Build => headset.GetText("firmware_version");

    /// <summary>Gets the edits made for the connected headset.</summary>
    public IReadOnlyList<FirmwareEdit> Edits => FirmwareEdit.For(headset.Model);

    /// <summary>Gets the edits on now, or null where that is not known.</summary>
    public IReadOnlySet<FirmwareEdit>? Installed => FirmwareEdit.Installed(Edits, Mark);

    /// <summary>Gets what Neap knows of the headset before writing.</summary>
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

    /// <summary>What applying these edits would write.</summary>
    public EditPlan Plan(IReadOnlySet<FirmwareEdit> wanted) => FirmwareEdit.Plan(Edits, wanted, Installed);

    /// <summary>Reads the mark again.</summary>
    public async Task ReadMark()
    {
        Mark = writer is null ? null : await writer.ReadMark();
        Changed?.Invoke();
    }

    /// <summary>Writes what it takes to have exactly these edits on, one step at a time.</summary>
    /// <param name="wanted">The edits ticked. None is restoring stock.</param>
    /// <param name="report">Told each step as it starts, and how far the write has got.</param>
    /// <param name="cancel">Stops the wait for the headset in download mode; nothing else can be stopped part way.</param>
    public async Task<ApplyOutcome> Apply(IReadOnlySet<FirmwareEdit> wanted, Action<ApplyProgress> report, CancellationToken cancel)
    {
        if (writer is null) throw new InvalidOperationException("this headset's firmware cannot be written by this build");
        var plan = Plan(wanted);
        var sessions = FirmwareEdit.Sessions(plan);
        AppLog.Write(string.Create(CultureInfo.InvariantCulture,
            $"firmware edits: applying [{string.Join(", ", wanted.Select(edit => edit.Id))}], {plan.Sectors.Count} sections in {sessions.Count} sessions: {string.Join(", ", plan.Sectors.Select(sector => sector.ToString("X8", CultureInfo.InvariantCulture)))}"));

        return await Run(sessions.Count, report, cancel: cancel, steps: async say =>
        {
            say(EditStep.Check, 0, null);
            var blockers = FirmwareEdit.Blockers(plan.Changing, Readiness);
            if (blockers.Count > 0) return Stop(ApplyResult.Failed, $"not ready: {string.Join(", ", blockers)}");

            int session = 0;
            foreach (var sectors in sessions)
            {
                session++;
                say(EditStep.Plug, session, null);
                await writer.WaitForDownloadMode(cancel);
                say(EditStep.Connect, session, null);
                string chip = await writer.Connect();
                if (plan.Changing.Any(edit => edit.FirmwareBuild != chip))
                    return Stop(ApplyResult.Refused, $"the chip holds firmware {chip}, which the edits are not made for");
                foreach (int sector in sectors)
                {
                    say(EditStep.Backup, session, null);
                    Backed(await writer.Backup(sector, BackupFolder));
                    say(EditStep.Write, session, 0);
                    await writer.Write(sector, FirmwareEdit.In(sector, wanted), new Reporter(done => say(EditStep.Write, session, done)));
                    say(EditStep.Verify, session, null);
                    if (await writer.Verify(sector) == WriteResult.Reverted)
                        return Stop(ApplyResult.Reverted, string.Create(CultureInfo.InvariantCulture, $"section {sector:X8} did not read back as written, and the headset put stock back"));
                }
                say(EditStep.Reconnect, session, null);
                await writer.WaitForHeadset(CancellationToken.None);
            }

            say(EditStep.Confirm, session, null);
            Mark = await writer.ReadMark();
            if (Mark is null) return Stop(ApplyResult.Unconfirmed, "written and verified; the headset leaves no mark to confirm it by");
            if (Installed is { } on && !on.SetEquals(wanted))
                return Stop(ApplyResult.Failed, $"the mark shows [{string.Join(", ", on.Select(edit => edit.Id))}] after the restart");
            return Stop(ApplyResult.Done, "every section written, verified and confirmed");
        });
    }

    /// <summary>Writes a section back as a backup holds it, for a headset an edit has left unwell.</summary>
    public async Task<ApplyOutcome> Restore(FirmwareBackup backup, Action<ApplyProgress> report, CancellationToken cancel)
    {
        if (writer is null) throw new InvalidOperationException("this headset's firmware cannot be written by this build");
        AppLog.Write($"firmware edits: restoring {Path.GetFileName(backup.File)}");

        return await Run(1, report, cancel: cancel, steps: async say =>
        {
            say(EditStep.Plug, 1, null);
            await writer.WaitForDownloadMode(cancel);
            say(EditStep.Connect, 1, null);
            string chip = await writer.Connect();
            if (chip != backup.FirmwareBuild)
                return Stop(ApplyResult.Refused, $"the chip holds firmware {chip}, and the backup was taken from {backup.FirmwareBuild}");
            say(EditStep.Write, 1, 0);
            var result = await writer.Restore(backup.File, new Reporter(done => say(EditStep.Write, 1, done)));
            say(EditStep.Verify, 1, null);
            if (result == WriteResult.Reverted) return Stop(ApplyResult.Reverted, "the backup did not read back as written, and the headset put stock back");
            say(EditStep.Reconnect, 1, null);
            await writer.WaitForHeadset(CancellationToken.None);
            say(EditStep.Confirm, 1, null);
            Mark = await writer.ReadMark();
            return Stop(Mark is null ? ApplyResult.Unconfirmed : ApplyResult.Done, "restored");
        });
    }

    private List<string> _backups = [];
    private EditStep? _at;

    private void Backed(string file)
    {
        _backups.Add(file);
        AppLog.Write($"firmware edits: backed up to {Path.GetFileName(file)}");
    }

    private ApplyOutcome Stop(ApplyResult result, string why)
    {
        AppLog.Write($"firmware edits: {result} at {_at}: {why}");
        return new ApplyOutcome(result, result is ApplyResult.Done or ApplyResult.Unconfirmed ? null : _at, _backups);
    }

    /// <summary>Runs the steps with the page told of each, and ends a cancel or a fault as an outcome rather than a throw.</summary>
    private async Task<ApplyOutcome> Run(int sessions, Action<ApplyProgress> report,
        Func<Action<EditStep, int, int?>, Task<ApplyOutcome>> steps, CancellationToken cancel)
    {
        Busy = true;
        _backups = [];
        _at = null;
        Changed?.Invoke();
        try
        {
            return await steps((step, session, percent) =>
            {
                if (_at != step) AppLog.Write(string.Create(CultureInfo.InvariantCulture, $"firmware edits: {step}, session {session} of {sessions}"));
                _at = step;
                report(new ApplyProgress(step, session, sessions, percent));
            });
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested && _at == EditStep.Plug)
        {
            return Stop(ApplyResult.Cancelled, "stopped while waiting for the headset in download mode");
        }
        catch (Exception ex)
        {
            return Stop(ApplyResult.Failed, ex.Message);
        }
        finally
        {
            Busy = false;
            Changed?.Invoke();
        }
    }

    /// <summary>Tells of progress as it happens, on the writer's own thread, so a late report never lands after the next step.</summary>
    private sealed class Reporter(Action<int> said) : IProgress<int>
    {
        public void Report(int value) => said(value);
    }
}
