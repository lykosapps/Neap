using System.Collections.Concurrent;
using System.Globalization;
using Neap.Core;
using Neap.Core.Audio;
using Neap.Core.Diagnostics;
using Neap.Core.Hid;
using Neap.Core.Protocol;

namespace Neap.Services;

/// <summary>
/// Records what the headset and Windows do while a person makes a problem
/// happen, and saves it as a text file for them to send with a bug report.
/// </summary>
/// <remarks>
/// <para>
/// It only listens. Everything the headset is asked goes through
/// <see cref="HeadsetService"/>, which reads and never writes for this, so a
/// recording cannot change a setting or fight the app for the headset.
/// </para>
/// <para>
/// Nothing leaves the PC. The file is saved where the person can find it,
/// with the serial number, radio addresses, account and PC names blanked, and
/// sending it is up to them.
/// </para>
/// <para>
/// Windows is listened to for a second at a time, so each line of the
/// timeline holds the loudest moment of that second rather than one instant.
/// Each program is named once per recording, not once per second, for the
/// reason on <see cref="Programs"/>.
/// </para>
/// </remarks>
public sealed class SessionRecorder(HeadsetService headset) : IDisposable
{
    private static readonly TimeSpan Listen = TimeSpan.FromSeconds(1);

    /// <summary>How long a group that gave no answer is waited on when asked again.</summary>
    private static readonly TimeSpan SilentWait = TimeSpan.FromSeconds(4);

    private readonly ConcurrentDictionary<uint, string> _names = new();
    private Recording? _recording;
    private Task? _began;
    private Task? _sampler;
    private CancellationTokenSource? _stopping;

    /// <summary>When the recording under way started, or null when none is.</summary>
    public DateTime? Since { get; private set; }

    /// <summary>Whether a recording is being stopped and written out.</summary>
    public bool Saving { get; private set; }

    /// <summary>Where the last recording was saved, or null when none has been since the app started.</summary>
    public string? Saved { get; private set; }

    /// <summary>Why the last recording could not be saved, or null when it was.</summary>
    public string? Trouble { get; private set; }

    /// <summary>The GitHub form to send the last recording with, filled in, or null when none was saved.</summary>
    public Uri? Issue { get; private set; }

    /// <summary>What the last recording found, when it was of a headset Neap does not know; otherwise null.</summary>
    public HeadsetFindings? Findings { get; private set; }

    /// <summary>A recording started, began saving, or finished. Raised on the UI thread.</summary>
    public event Action? Changed;

    /// <summary>Starts recording, unless a recording is already under way.</summary>
    public void Start()
    {
        if (_recording is not null) return;
        var recording = new Recording(
            $"{AppInfo.Name} {AppInfo.Version?.ToString(3)}", Environment.OSVersion.Version.ToString());
        _recording = recording;
        _names.Clear();
        _stopping = new CancellationTokenSource();
        Since = DateTime.Now;
        Saved = null;
        Trouble = null;
        Issue = null;
        Findings = null;

        headset.Heard += Hear;
        _began = Task.Run(async () => recording.Began(await Snap()));
        var stopping = _stopping.Token;
        _sampler = Task.Run(() => Sample(recording, stopping));
        AppLog.Write("recording: started");
        Changed?.Invoke();
    }

    /// <summary>Stops the recording under way and saves it; <see cref="Saved"/> or <see cref="Trouble"/> says how that went.</summary>
    public async Task Stop()
    {
        if (_recording is not { } recording || Saving) return;
        Saving = true;
        Changed?.Invoke();
        headset.Heard -= Hear;
        _stopping!.Cancel();
        try
        {
            await _sampler!;
            await _began!;
            recording.Ended(await Snap());

            IReadOnlyList<string> log;
            try { log = AppLog.Read(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log = [$"the log could not be read: {ex.Message}"];
            }

            Saved = await Save(recording.Write(log, Redaction.ForThisPc(recording.Secrets)));
            Issue = recording.Issue();
            Findings = recording.ForAnotherHeadset ? recording.Findings() : null;
            AppLog.Write($"recording: saved as {Path.GetFileName(Saved)}");
        }
        catch (Exception ex)
        {
            Trouble = ex.Message;
            AppLog.Write($"recording: could not be saved: {ex.Message}");
        }
        finally
        {
            _stopping.Dispose();
            _recording = null;
            Since = null;
            Saving = false;
            Changed?.Invoke();
        }
    }

    /// <summary>Drops a recording under way without saving it, as the app quits.</summary>
    public void Dispose()
    {
        if (_recording is null || Saving) return;
        headset.Heard -= Hear;
        _stopping?.Cancel();
        _recording = null;
        AppLog.Write("recording: the app quit while recording, so it was not saved");
    }

    private void Hear(DeviceEvent evt) => _recording?.Heard(DateTime.Now, evt);

    private void Sample(Recording recording, CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            // A real survey spends the second listening. A pretend one, or
            // one that failed, returns at once and waits it out here.
            bool listened = false;
            try
            {
                recording.Sampled(DateTime.Now, Survey());
                listened = !Pretend.Active;
            }
            catch (Exception ex)
            {
                recording.Failed(DateTime.Now, $"Sound: {ex.Message}");
            }
            if (!listened) stopping.WaitHandle.WaitOne(Listen);
        }
    }

    private SoundSurvey Survey() =>
        Pretend.Windows?.Survey() ?? Platform.Current.Survey(Listen, pid => _names.GetOrAdd(pid, Platform.Current.ProgramName));

    /// <summary>What the headset, its transmitters and Windows show now, with whatever could not be read said.</summary>
    private async Task<Snapshot> Snap()
    {
        var at = DateTime.Now;
        var unread = new List<string>();

        try { await headset.Refresh(); }
        catch (Exception ex) { unread.Add($"headset values: {ex.Message}"); }
        var values = headset.Values.ToDictionary(p => p.Key, p => p.Value.Clone());

        // A group that gave no answer is asked once more, and given longer: on a
        // headset Neap doesn't know, a group it keeps elsewhere would otherwise
        // read as functions it doesn't have.
        if (values.Count > 0)
            foreach (string group in HeadsetCheck.Silent(values.Keys))
            {
                try
                {
                    var again = await headset.Post(client => client.ReadCategory(group, SilentWait));
                    foreach (var (key, value) in again) values[key] = value.Clone();
                    if (again.Count == 0) unread.Add($"{group}: no answer, asked twice");
                }
                catch (Exception ex) { unread.Add($"{group}: {ex.Message}"); }
            }

        IReadOnlyList<Transmitter> slots = [];
        try { slots = await headset.Post(client => Transmitters.ReadAll(client)); }
        catch (Exception ex) { unread.Add($"transmitter slots: {ex.Message}"); }

        IReadOnlyList<HidDeviceInfo> plugged = [];
        try { plugged = Pretend.Active ? AppServices.Devices.Candidates() : SystemDevices.List(usagePage: null); }
        catch (Exception ex) { unread.Add($"plugged-in devices: {ex.Message}"); }

        SoundSurvey? sound = null;
        try { sound = await Task.Run(Survey); }
        catch (Exception ex) { unread.Add($"Sound: {ex.Message}"); }

        return new Snapshot(at, headset.Status.Summary, plugged, slots, values, sound, unread);
    }

    /// <summary>Writes the file under a name of its own, in Downloads, or in the pretend folder on a pretend run.</summary>
    private static async Task<string> Save(string text)
    {
        string folder = Pretend.Active ? AppFolder.Path : Platform.Current.Downloads();
        Directory.CreateDirectory(folder);
        string name = string.Create(CultureInfo.InvariantCulture, $"Neap recording {DateTime.Now:yyyy-MM-dd HH.mm.ss}");
        for (int copy = 1; ; copy++)
        {
            string path = Path.Combine(folder, copy == 1 ? $"{name}.txt" : $"{name} ({copy}).txt");
            try
            {
                await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                await using var writer = new StreamWriter(file);
                await writer.WriteAsync(text);
                return path;
            }
            catch (IOException) when (File.Exists(path) && copy < 100)
            {
                // Taken: try the next name.
            }
        }
    }

    /// <summary>
    /// Opens the GitHub form for the last recording, filled in, then File
    /// Explorer with the file selected.
    /// </summary>
    /// <remarks>
    /// Explorer opens second so it lands in front of the browser, with the
    /// file ready to drag into the form.
    /// </remarks>
    public async Task Report()
    {
        if (Saved is not { } saved || Issue is not { } issue) return;
        try { await Platform.Current.Open(issue); }
        catch (Exception ex) { AppLog.Write($"recording: could not open the report form: {ex.Message}"); }
        try { Platform.Current.Reveal(saved); }
        catch (Exception ex) { AppLog.Write($"recording: could not show {Path.GetFileName(saved)}: {ex.Message}"); }
    }
}
