using System.Globalization;
using Neap.Core;
using Neap.Core.Connection;
using Neap.Core.Firmware;

namespace Neap.Services;

/// <summary>How applying the edits, or restoring a backup, ended.</summary>
public enum ApplyResult
{
    /// <summary>Every section was written and read back as written.</summary>
    Done,

    /// <summary>Every section was written, but could not be read back to check it.</summary>
    NotReadBack,

    /// <summary>The person stopped while Neap was waiting for the headset in download mode, before anything was written.</summary>
    Cancelled,

    /// <summary>The person stopped while Neap was waiting to plug in again, after part of the change was written.</summary>
    StoppedPartWritten,

    /// <summary>The person stopped before a section that did not read back as written could be put back.</summary>
    StoppedBeforePutBack,

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
/// <param name="Again">Whether this plug-in comes after a write, so the cable is to be unplugged and plugged in again.</param>
public sealed record ApplyProgress(EditStep Step, int Session, int Sessions, int? Percent, bool Again = false);

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
/// listed with their state read from its flash, and <see cref="CanWrite"/> is false.
/// </para>
/// </remarks>
public sealed class FirmwareEditService(HeadsetService headset, IFirmwareWriter? writer)
{
    /// <summary>The flash read or the running state changed.</summary>
    public event Action? Changed;

    /// <summary>Gets whether this headset's firmware can be written by this build of Neap.</summary>
    public bool CanWrite => writer is not null;

    /// <summary>Gets whether applying or a restore is under way.</summary>
    public bool Busy { get; private set; }

    /// <summary>Gets the bytes last read at each place an edit changes, by address, or null where the flash could not be read.</summary>
    public IReadOnlyDictionary<int, byte[]>? Flash { get; private set; }

    /// <summary>Gets the firmware version the headset reports, or the one it last reported while it is off.</summary>
    public string? Build => Seen().Build;

    /// <summary>Gets the edits made for the headset, connected or last seen.</summary>
    public IReadOnlyList<FirmwareEdit> Edits => FirmwareEdit.For(Seen().Model);

    /// <summary>Gets the edits on now, or null where that is not known.</summary>
    public IReadOnlySet<FirmwareEdit>? Installed => FirmwareEdit.InstalledIn(Edits, Build, Flash);

    /// <summary>Gets what Neap knows of the headset before writing.</summary>
    /// <remarks>
    /// <para>
    /// The person need not have the headset on to apply: what was last seen of
    /// it stands in while it is off, for <see cref="BatteryFresh"/>, and the
    /// sections themselves are read in download mode before anything is written.
    /// </para>
    /// <para>
    /// Swarm II is not looked for yet: its program's name is not known, and
    /// nothing is written to a real headset until it is.
    /// </para>
    /// </remarks>
    public EditReadiness Readiness
    {
        get
        {
            var seen = Seen();
            bool fresh = seen.At is { } at && DateTime.UtcNow - at < BatteryFresh;
            return new(Connected || (fresh && seen.Build is not null), seen.Build, fresh ? seen.Battery : null, SwarmRunning: false);
        }
    }

    /// <summary>How long what was last seen of a headset that is off still counts.</summary>
    public static readonly TimeSpan BatteryFresh = TimeSpan.FromMinutes(30);

    private bool Connected => headset.Status.Link == Link.Connected;

    private (HeadsetModel? Model, string? Build, int? Battery, DateTime? At) _seen;

    /// <summary>The headset as it is now, remembered so it still stands once the headset is switched off.</summary>
    private (HeadsetModel? Model, string? Build, int? Battery, DateTime? At) Seen()
    {
        if (!Connected) return _seen;
        var model = headset.Model ?? _seen.Model;
        string? build = headset.GetText("firmware_version") ?? _seen.Build;
        int? level = headset.TryGetNumberByKey(Battery.Key, out int battery) ? battery : _seen.Battery;
        _seen = (model, build, level, DateTime.UtcNow);
        return _seen;
    }

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
    public EditStatus StatusOf(FirmwareEdit edit) => edit.StatusIn(Build, Flash);

    /// <summary>What applying these edits would write.</summary>
    public EditPlan Plan(IReadOnlySet<FirmwareEdit> wanted) => FirmwareEdit.Plan(Edits, wanted, Installed);

    /// <summary>Reads the flash again at every place the headset's edits change.</summary>
    /// <remarks>A read already under way is not started twice. While the headset is off, the last read stands.</remarks>
    public async Task ReadFlash()
    {
        if (_reading) return;
        _reading = true;
        try
        {
            var read = await ReadEditedFlash();
            if (read is not null || Connected) Flash = read;
        }
        finally
        {
            _reading = false;
        }
        Changed?.Invoke();
    }

    private bool _reading;

    private static readonly TimeSpan FlashWait = TimeSpan.FromMilliseconds(500);

    /// <summary>The bytes at every place the headset's edits change, or null when any could not be read.</summary>
    private async Task<IReadOnlyDictionary<int, byte[]>?> ReadEditedFlash()
    {
        var changes = Edits.SelectMany(edit => edit.Changes).ToList();
        if (headset.Status.Link != Link.Connected || changes.Count == 0)
        {
            AppLog.Write(changes.Count == 0
                ? "firmware edits: the flash is not read, as no edits are known for this headset yet"
                : "firmware edits: the flash is not read while the headset is not connected");
            return null;
        }
        var read = await headset.Post(client =>
        {
            var bytes = new Dictionary<int, byte[]>();
            foreach (var change in changes)
            {
                if (client.ReadFlash(change.Address, change.Factory.Count, FlashWait) is not { } run) return null;
                bytes[change.Address] = run;
            }
            return bytes;
        });
        if (read is null) AppLog.Write("firmware edits: the flash could not be read");
        else AppLog.Write($"firmware edits: read {string.Join(", ", Edits.Select(edit => $"{edit.Id} {edit.StatusIn(Build, read)}"))}");
        return read;
    }

    /// <summary>Writes what it takes to have exactly these edits on, one step at a time.</summary>
    /// <param name="wanted">The edits ticked. None is restoring stock.</param>
    /// <param name="report">Told each step as it starts, and how far the write has got.</param>
    /// <param name="cancel">Stops a wait for the headset in download mode; nothing else can be stopped part way.</param>
    public async Task<ApplyOutcome> Apply(IReadOnlySet<FirmwareEdit> wanted, Action<ApplyProgress> report, CancellationToken cancel)
    {
        if (writer is null) throw new InvalidOperationException("this headset's firmware cannot be written by this build");
        // With the headset off, its edits as last read may be out of date, so every section the edits touch is read
        // in download mode and only those that differ are written.
        var plan = Connected ? Plan(wanted) : FirmwareEdit.Plan(Edits, wanted, null);
        var sessions = FirmwareEdit.Sessions(plan);
        string build = Build ?? "unknown";
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
                // The chip stays held after a session that wrote nothing; after a write it has restarted.
                if (!writer.Held) await PlugIn(say, session, cancel);
                say(EditStep.Backup, session, null);
                var before = new List<(int Sector, byte[] Bytes)>();
                var after = new List<(int Sector, byte[] Bytes)>();
                foreach (int sector in sectors)
                {
                    byte[] held = await writer.Read(sector);
                    Backed(SaveBackup(sector, held, build));
                    if (FirmwareEdit.Compose(sector, held, wanted, writer.FactorySections) is not { } section)
                        return Stop(ApplyResult.Refused, Hex($"section {sector:X8} holds something other than the factory firmware and Neap's edits"));
                    before.Add((sector, held));
                    after.Add((sector, section));
                }
                if (after.Zip(before).All(pair => pair.First.Bytes.AsSpan().SequenceEqual(pair.Second.Bytes)))
                {
                    AppLog.Write($"firmware edits: session {session} already holds what is wanted; nothing written");
                    Known(before);
                    continue;
                }

                if (await WriteAndCheck(after, say, session)) continue;
                AppLog.Write($"firmware edits: session {session} did not read back as written; putting back what was there");
                _puttingBack = true;
                await PlugIn(say, session, cancel);
                return await WriteAndCheck(before, say, session)
                    ? Stop(ApplyResult.Reverted, "a section did not read back as written; what was there before is back")
                    : Stop(ApplyResult.Failed, "a section did not read back as written, and putting back what was there did not read back either");
            }
            return Stop(ApplyResult.Done, "every section written and read back");
        });
    }

    /// <summary>Writes a section back as a backup holds it, for a headset an edit has left unwell.</summary>
    public async Task<ApplyOutcome> Restore(FirmwareBackup backup, Action<ApplyProgress> report, CancellationToken cancel)
    {
        if (writer is null) throw new InvalidOperationException("this headset's firmware cannot be written by this build");
        AppLog.Write($"firmware edits: restoring {Path.GetFileName(backup.File)}");

        return await Run(1, report, cancel: cancel, steps: async say =>
        {
            byte[] saved = await File.ReadAllBytesAsync(backup.File, cancel);
            if (saved.Length != FirmwareEdit.SectorSize) return Stop(ApplyResult.Refused, $"{Path.GetFileName(backup.File)} is not a whole section");
            await PlugIn(say, 1, cancel);
            say(EditStep.Backup, 1, null);
            Backed(SaveBackup(backup.Sector, await writer.Read(backup.Sector), Build ?? "unknown"));
            return await WriteAndCheck([(backup.Sector, saved)], say, 1)
                ? Stop(ApplyResult.Done, "restored and read back")
                : Stop(ApplyResult.Failed, "the backup did not read back as written");
        });
    }

    /// <summary>Asks for the headset in download mode: plugged in the first time, plugged in again after a write.</summary>
    private async Task PlugIn(Action<EditStep, int, int?> say, int session, CancellationToken cancel)
    {
        _again = _written;
        say(EditStep.Plug, session, null);
        await writer!.WaitForDownloadMode(cancel);
        _again = false;
        say(EditStep.Connect, session, null);
    }

    /// <summary>Writes sections and reads them back, and says whether every one reads back as written.</summary>
    /// <remarks>
    /// The write program restarts the chip into the firmware with the cable still in. Each section is read back
    /// whole over the cable, which answers flash reads even while the switched-off headset shows as not connected
    /// (SP2Tools F318, F319). What is read becomes the page's edit states.
    /// </remarks>
    /// <exception cref="NotReadBackException">The sections were written but could not be read back.</exception>
    private async Task<bool> WriteAndCheck(IReadOnlyList<(int Sector, byte[] Bytes)> sections, Action<EditStep, int, int?> say, int session)
    {
        // What the page showed of these sections is no longer known until they are read back.
        Flash = null;
        Changed?.Invoke();
        say(EditStep.Write, session, 0);
        _written = true;
        await writer!.Write(sections, new Reporter(done => say(EditStep.Write, session, done)));
        say(EditStep.Verify, session, null);
        var read = new List<(int Sector, byte[] Bytes)>();
        foreach (var (sector, bytes) in sections)
            read.Add((sector, await ReadBack(sector, bytes.Length) ?? throw new NotReadBackException(Hex($"written, but section {sector:X8} could not be read back"))));
        Known(read);
        foreach (var ((sector, wantedBytes), (_, got)) in sections.Zip(read))
        {
            if (got.AsSpan().SequenceEqual(wantedBytes))
            {
                AppLog.Write(Hex($"firmware edits: section {sector:X8} reads back as written"));
                continue;
            }
            AppLog.Write(Hex($"firmware edits: section {sector:X8} does not read back as written"));
            return false;
        }
        return true;
    }

    /// <summary>Takes whole sections as read as what the headset now holds, for the page's edit states.</summary>
    private void Known(IEnumerable<(int Sector, byte[] Bytes)> sections)
    {
        foreach (var (sector, bytes) in sections) _sections[sector] = bytes;
        Flash = FirmwareEdit.FlashFrom(Edits, _sections, _flashBefore);
        Changed?.Invoke();
    }

    private static readonly TimeSpan ReadBackWait = TimeSpan.FromMinutes(2);

    /// <summary>Reads a section over the cable once the firmware is up, or null if it cannot within two minutes.</summary>
    /// <remarks>
    /// After the write program restarts it, the switched-off headset comes up on the cable and then shows as not
    /// connected, as it is off, yet it still answers flash reads there (3 November 2026), and the connection stays
    /// open while it shows as not connected. So the read is tried whatever the connection says, until it answers.
    /// </remarks>
    private async Task<byte[]?> ReadBack(int sector, int length)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed < ReadBackWait)
        {
            try
            {
                if (await headset.Post(client => client.ReadFlash(sector, length, FlashWait)) is { } read) return read;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                AppLog.Write($"firmware edits: reading back {sector:X8} not yet: {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        return null;
    }

    /// <summary>The sections were written, but could not be read back to check them.</summary>
    private sealed class NotReadBackException(string message) : IOException(message);

    /// <summary>Saves a section as it was read, and gives the file.</summary>
    private static string SaveBackup(int sector, byte[] held, string build)
    {
        Directory.CreateDirectory(BackupFolder);
        string file = Path.Combine(BackupFolder, FirmwareBackup.FileName(DateTime.Now, sector, build));
        File.WriteAllBytes(file, held);
        return file;
    }

    private static string Hex(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private List<string> _backups = [];
    private EditStep? _at;
    private bool _written;
    private bool _again;
    private bool _puttingBack;
    private readonly Dictionary<int, byte[]> _sections = [];
    private IReadOnlyDictionary<int, byte[]>? _flashBefore;

    private void Backed(string file)
    {
        _backups.Add(file);
        AppLog.Write($"firmware edits: backed up to {Path.GetFileName(file)}");
    }

    private ApplyOutcome Stop(ApplyResult result, string why)
    {
        AppLog.Write($"firmware edits: {result} at {_at}: {why}");
        return new ApplyOutcome(result, result is ApplyResult.Done ? null : _at, _backups);
    }

    /// <summary>Runs the steps with the page told of each, and ends a cancel or a fault as an outcome rather than a throw.</summary>
    private async Task<ApplyOutcome> Run(int sessions, Action<ApplyProgress> report,
        Func<Action<EditStep, int, int?>, Task<ApplyOutcome>> steps, CancellationToken cancel)
    {
        Busy = true;
        _backups = [];
        _at = null;
        _written = false;
        _again = false;
        _puttingBack = false;
        _sections.Clear();
        _flashBefore = Flash;
        Changed?.Invoke();
        try
        {
            return await steps((step, session, percent) =>
            {
                if (_at != step) AppLog.Write(string.Create(CultureInfo.InvariantCulture, $"firmware edits: {step}, session {session} of {sessions}"));
                _at = step;
                report(new ApplyProgress(step, session, sessions, percent, _again));
            });
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested && _at == EditStep.Plug)
        {
            // Stopped while waiting to plug in again: something is already written, and while putting a section
            // back, what is on the headset is not what was wanted.
            return _puttingBack
                ? Stop(ApplyResult.StoppedBeforePutBack, "stopped before a section that did not read back could be put back")
                : _written
                    ? Stop(ApplyResult.StoppedPartWritten, "stopped while waiting to plug in again; part of the change is written")
                    : Stop(ApplyResult.Cancelled, "stopped while waiting for the headset in download mode");
        }
        catch (NotReadBackException ex)
        {
            return Stop(ApplyResult.NotReadBack, ex.Message);
        }
        catch (Exception ex)
        {
            AppLog.Write($"firmware edits: {ex}");
            return Stop(ApplyResult.Failed, ex.Message);
        }
        finally
        {
            writer?.LetGo();
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
