using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.UI.Dispatching;
using Neap.Core.Updates;

namespace Neap.App.Services;

/// <summary>
/// Looks on GitHub for a newer version of Neap, and puts it in place of this
/// one when asked.
/// </summary>
/// <remarks>
/// <para>
/// The only thing Neap sends over a network. A check asks GitHub for its
/// latest release and sends nothing from the PC or the headset, only the
/// request, which GitHub sees as it sees any website visit; an update
/// downloads that release's zip. Neither happens in a
/// pretend run, which finds the release <see cref="Pretend.Release"/> gives,
/// if any, and installs nothing.
/// </para>
/// <para>
/// The download is checked against the SHA-256 published beside it, and the
/// Neap.exe inside against the release's version, before anything is moved.
/// The hash comes from the same release, so it catches a broken download,
/// not a release replaced by someone with the account; signing would, and
/// the download is not signed yet.
/// </para>
/// <para>
/// The work happens in a hidden folder inside Neap's own, so that putting
/// the new version in place is a set of renames on one drive
/// (<see cref="FolderSwap"/>). The version that starts next clears it.
/// </para>
/// </remarks>
public sealed class UpdateService : IDisposable
{
    private static readonly HttpClient Http = MakeClient();

    /// <summary>How long the download may stall before it is given up.</summary>
    private static readonly TimeSpan Stall = TimeSpan.FromSeconds(30);

    private static string Folder => AppContext.BaseDirectory;

    private static string Work => Path.Combine(Folder, ".update");

    private static string ExeName => Path.GetFileName(Environment.ProcessPath) ?? AppInfo.Name + ".exe";

    private readonly DispatcherQueueTimer _due;

    /// <summary>How the newer version was offered when it was found.</summary>
    private UpdateStage _offered = UpdateStage.Available;

    public UpdateStage Stage { get; private set; }

    /// <summary>The newer version found, or null if none has been.</summary>
    public Release? Newer { get; private set; }

    /// <summary>The stage the banner and the buttons follow.</summary>
    /// <remarks>
    /// The same as <see cref="Stage"/> except that a check running or failing
    /// doesn't take the offer of a newer version away; see <see cref="UpdateReminder.Shown"/>.
    /// </remarks>
    public UpdateStage Shown => Newer is null ? Stage : UpdateReminder.Shown(Stage, _offered);

    /// <summary>Whether the banner about the newer version is on show.</summary>
    public bool ReminderShown => Newer is { } release && UpdateReminder.Shows(
        Shown, release.Version, AppSettings.Current.SkippedVersion, AppSettings.Current.UpdateHiddenUntil, DateTimeOffset.Now);

    /// <summary>How much of the download has arrived, out of a hundred.</summary>
    public int Percent { get; private set; }

    /// <summary>Raised on the UI thread whenever the stage or the download's progress changes.</summary>
    public event Action? Changed;

    /// <summary>Raised when a check made on its own finds a version not announced before.</summary>
    public event Action<Release>? Found;

    /// <summary>Raised once the new version is in place, with the program to start in place of this one.</summary>
    public event Action<string>? Installed;

    /// <remarks>Create on the UI thread: its checks and events run there.</remarks>
    public UpdateService()
    {
        _due = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _due.Interval = TimeSpan.FromHours(1);
        _due.Tick += (_, _) =>
        {
            CheckIfDue();

            // A banner put away for a day comes back with nobody to ask for it.
            Changed?.Invoke();
        };
        _due.Start();

        if (!Pretend.Active) _ = Task.Run(ClearLeftovers);
        Remember();
        CheckIfDue();
    }

    /// <summary>Whether Neap checks once a day on its own.</summary>
    public bool Automatic
    {
        get => AppSettings.Current.CheckForUpdates;
        set
        {
            AppSettings.Update(s => s.CheckForUpdates = value);
            AppLog.Write(value ? "updates: checking once a day" : "updates: no longer checking on their own");
            CheckIfDue();
        }
    }

    /// <summary>Puts the banner away for a day.</summary>
    public void RemindLater()
    {
        if (Newer is null || Busy) return;
        AppSettings.Update(s => s.UpdateHiddenUntil = DateTimeOffset.Now + UpdateReminder.Hold);
        AppLog.Write("updates: the banner is put away for a day");
        Changed?.Invoke();
    }

    /// <summary>Puts the banner away until a newer version than this one is out.</summary>
    public void SkipThisVersion()
    {
        if (Newer is not { } release || Busy) return;
        string version = release.Version.ToString(3);
        AppSettings.Update(s => s.SkippedVersion = version);
        AppLog.Write($"updates: version {version} is skipped");
        Changed?.Invoke();
    }

    private bool Busy => Stage is UpdateStage.Checking or UpdateStage.Downloading or UpdateStage.Installing;

    /// <summary>
    /// Picks up what the last check found, so Settings says so from the start
    /// rather than staying blank until the next check, which may be hours away.
    /// </summary>
    /// <remarks>
    /// A newer version found is offered again if it is still newer. A check
    /// made with nothing newer found leaves Neap up to date. A pretend run
    /// keeps none, so each starts from what its flags say.
    /// </remarks>
    private void Remember()
    {
        if (Pretend.Active) return;
        if (AppSettings.Current.FoundRelease is not { } kept)
        {
            if (AppSettings.Current.CheckedForUpdates is not null) Stage = UpdateStage.UpToDate;
            return;
        }

        try
        {
            var release = Release.Parse(kept);
            if (AppInfo.Version is { } running && release.IsNewerThan(running))
            {
                Newer = release;
                _offered = Stage = CanWriteHere() ? UpdateStage.Available : UpdateStage.CannotUpdateHere;
                AppLog.Write($"updates: version {release.Version.ToString(3)} is still available");
                return;
            }
        }
        catch (FormatException ex)
        {
            AppLog.Write($"updates: could not read the version found earlier: {ex.Message}");
        }
        AppSettings.Update(s => s.FoundRelease = null);
        Stage = UpdateStage.UpToDate;
    }

    private void CheckIfDue()
    {
        // A pretend run asks every time, so a script sees the same thing on each launch.
        if (Automatic && !Busy && (Pretend.Active || UpdateSchedule.Due(AppSettings.Current.CheckedForUpdates, DateTimeOffset.Now)))
            _ = Check(onItsOwn: true);
    }

    /// <summary>Asks GitHub for its latest release.</summary>
    /// <param name="onItsOwn">Whether the check was made by the schedule rather than by somebody pressing Check.</param>
    public async Task Check(bool onItsOwn = false)
    {
        if (Busy) return;
        Set(UpdateStage.Checking);
        try
        {
            var latest = Pretend.Active ? Pretend.Release : Release.Parse(await Http.GetStringAsync(Release.Latest));
            AppSettings.Update(s => s.CheckedForUpdates = DateTimeOffset.Now);
            if (latest is null || AppInfo.Version is not { } running || !latest.IsNewerThan(running))
            {
                Newer = null;
                if (!Pretend.Active) AppSettings.Update(s => s.FoundRelease = null);
                AppLog.Write("updates: up to date");
                Set(UpdateStage.UpToDate);
                return;
            }

            Newer = latest;
            if (!Pretend.Active) AppSettings.Update(s => s.FoundRelease = latest.ToJson());
            string version = latest.Version.ToString(3);
            bool here = Pretend.Active || CanWriteHere();
            AppLog.Write(here ? $"updates: version {version} is available"
                : $"updates: version {version} is available, but this folder cannot be written to");
            _offered = here ? UpdateStage.Available : UpdateStage.CannotUpdateHere;
            Set(_offered);

            if (onItsOwn && !Pretend.Active && AppSettings.Current.ToldAboutVersion != version
                && AppSettings.Current.SkippedVersion != version)
            {
                AppSettings.Update(s => s.ToldAboutVersion = version);
                Found?.Invoke(latest);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or FormatException)
        {
            AppLog.Write($"updates: could not check: {ex.Message}");
            Set(UpdateStage.CheckFailed);
        }
    }

    /// <summary>Downloads the newer version, checks it and puts it in place, then raises <see cref="Installed"/>.</summary>
    public async Task Update()
    {
        if (Newer is not { } release || Busy) return;
        string version = release.Version.ToString(3);
        if (Pretend.Active)
        {
            AppLog.Write($"pretend: version {version} would download and install now");
            await Rehearse();
            return;
        }

        try
        {
            Percent = 0;
            Set(UpdateStage.Downloading);
            Directory.CreateDirectory(Work);
            File.SetAttributes(Work, File.GetAttributes(Work) | FileAttributes.Hidden);

            string expected = release.HashFrom(await Http.GetStringAsync(release.Hash));
            string zip = Path.Combine(Work, release.ZipName);
            string hash = await Download(release.Zip, zip);
            if (hash != expected)
                throw new InvalidDataException($"the download's SHA-256 {hash} is not the published {expected}");

            Set(UpdateStage.Installing);
            await Task.Run(() => Install(release, zip));
            AppLog.Write($"updates: version {version} is in place, restarting into it");
            Installed?.Invoke(Path.Combine(Folder, ExeName));
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or FormatException
            or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"updates: could not update to {version}, so this version stays: {ex.Message}");
            Set(UpdateStage.UpdateFailed);
            await Task.Run(ClearDownload);
        }
    }

    /// <summary>Steps through downloading and installing without doing either, then offers the update again.</summary>
    /// <remarks>
    /// A pretend run installs nothing, but a screen walk still has to see
    /// the banner and the card while an update runs, so each stage lasts a
    /// few seconds.
    /// </remarks>
    private async Task Rehearse()
    {
        Percent = 0;
        Set(UpdateStage.Downloading);
        for (int step = 1; step <= 20; step++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            Percent = step * 5;
            Changed?.Invoke();
        }

        Set(UpdateStage.Installing);
        await Task.Delay(TimeSpan.FromSeconds(3));
        Set(_offered);
    }

    /// <summary>Downloads a file, reporting progress, and gives its SHA-256 in lower case.</summary>
    private async Task<string> Download(Uri from, string to)
    {
        using var response = await Http.GetAsync(from, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        long length = response.Content.Headers.ContentLength ?? 0;

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (var source = await response.Content.ReadAsStreamAsync())
        await using (var file = File.Create(to))
        {
            var buffer = new byte[81920];
            long arrived = 0;
            int read;
            while ((read = await ReadWithin(source, buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read));
                sha.AppendData(buffer, 0, read);
                arrived += read;
                int percent = length > 0 ? (int)(arrived * 100 / length) : 0;
                if (percent == Percent) continue;
                Percent = percent;
                Changed?.Invoke();
            }
        }
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    /// <remarks>The client's own timeout ends once the download starts, so a stall is caught here.</remarks>
    private static async Task<int> ReadWithin(Stream source, byte[] buffer)
    {
        using var stalled = new CancellationTokenSource(Stall);
        return await source.ReadAsync(buffer, stalled.Token);
    }

    /// <summary>Unzips the download, checks it is the version expected, and puts it in place of this one.</summary>
    private static void Install(Release release, string zip)
    {
        string incoming = Path.Combine(Work, "new"), replaced = Path.Combine(Work, "old");
        foreach (string folder in new[] { incoming, replaced })
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);

        // Refuses any entry that would land outside the folder.
        ZipFile.ExtractToDirectory(zip, incoming);
        File.Delete(zip);

        string exe = Path.Combine(incoming, ExeName);
        if (!File.Exists(exe)) throw new InvalidDataException($"the download has no {ExeName}");
        var info = FileVersionInfo.GetVersionInfo(exe);
        var found = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
        if (found != release.Version)
            throw new InvalidDataException($"the download's {ExeName} is version {found}, not {release.Version}");

        FolderSwap.Swap(Folder, incoming, replaced);
    }

    /// <summary>Whether Neap can write to its own folder, which updating needs.</summary>
    private static bool CanWriteHere()
    {
        try
        {
            using var probe = new FileStream(Path.Combine(Folder, ".update-probe"), FileMode.Create,
                FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Deletes the work folder an update left behind.</summary>
    /// <remarks>
    /// After an update, the version that was replaced may still be closing,
    /// and its files can't be deleted until it has, so this tries for a while.
    /// </remarks>
    private static async Task ClearLeftovers()
    {
        for (int attempt = 1; Directory.Exists(Work); attempt++)
        {
            try
            {
                Directory.Delete(Work, recursive: true);
                AppLog.Write("updates: cleared what the last update left");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 10)
                {
                    AppLog.Write($"updates: could not clear what the last update left: {ex.Message}");
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }

    /// <summary>Deletes the download and its unzipped copy after an update that failed.</summary>
    /// <remarks>
    /// The files moved aside are kept: if putting them back failed, they are
    /// the only copy of the version still running.
    /// </remarks>
    private static void ClearDownload()
    {
        try
        {
            string incoming = Path.Combine(Work, "new");
            if (Directory.Exists(incoming)) Directory.Delete(incoming, recursive: true);
            foreach (string zip in Directory.EnumerateFiles(Work, "*.zip")) File.Delete(zip);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"updates: could not clear the download after it failed: {ex.Message}");
        }
    }

    private void Set(UpdateStage stage)
    {
        Stage = stage;
        Changed?.Invoke();
    }

    /// <remarks>GitHub refuses a request without a user agent; Neap's name is all it is given.</remarks>
    private static HttpClient MakeClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.Name);
        return client;
    }

    public void Dispose() => _due.Stop();
}
