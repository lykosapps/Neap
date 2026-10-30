using System.Text.Json;
using System.Text.Json.Serialization;
using Neap.Core;
using Neap.Core.Audio;
using Neap.Core.Presets;
using Neap.Core.Settings;

namespace Neap.Services;

/// <summary>
/// The handful of things the app has to remember between launches.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately small. Everything about the headset lives on the headset and
/// is read back from it, so there is no local copy to drift out of date. Only
/// choices neither the headset nor Windows can answer for us are stored: which
/// applications carry chat, the keyboard shortcuts, where the window was
/// left, whether the tray notice has been shown, the two halves of noise
/// control the headset has no place for, the parametric equaliser's ten
/// bands, which preset an edited curve came from, whether to keep the Charging Dock's
/// status ring purple, and whether and when to look for a newer version and
/// whether its banner has been put away.
/// </para>
/// <para>
/// Profiles are the one deliberate exception: a full snapshot, kept here
/// rather than on the headset, so a profile survives the headset being reset
/// and does not compete with its own five equaliser slots.
/// </para>
/// <para>
/// Kept separate from the mix's volume journal, which is recovery state
/// rather than preference: losing this file costs a re-pick, losing that one
/// leaves somebody's Spotify quiet.
/// </para>
/// </remarks>
public sealed class AppSettings
{
    /// <summary>Which applications carry chat.</summary>
    /// <remarks>
    /// Having one at all is what "the mix is on" means, so there is no
    /// separate enabled flag to fall out of step with it.
    /// </remarks>
    [JsonPropertyName("chat_apps")] public List<string> ChatApps { get; set; } = new();

    /// <summary>
    /// The names the chosen chat apps show, by program, kept from when each was
    /// last found playing to the headset, so one not open now is still shown by
    /// its name rather than its program's.
    /// </summary>
    [JsonPropertyName("chat_app_names")] public Dictionary<string, string> ChatAppNames { get; set; } = new();

    /// <summary>
    /// Whether we have told them that closing the window does not stop the
    /// app. Said once, because the second time it is noise.
    /// </summary>
    [JsonPropertyName("told_about_tray")] public bool ToldAboutTray { get; set; }

    /// <summary>
    /// The product id of the transmitter the headset's settings left with,
    /// while it is unplugged, so that a restart still says so.
    /// </summary>
    [JsonPropertyName("settings_left_with")] public ushort? SettingsLeftWith { get; set; }

    /// <summary>Whether the mix can be moved by keyboard from inside a game.</summary>
    /// <remarks>
    /// Off by default: taking three key combinations away from everything else
    /// on the machine is not something to do to someone without asking.
    /// </remarks>
    [JsonPropertyName("mix_hotkeys")] public bool MixHotkeys { get; set; }

    /// <summary>The level noise cancellation comes back at, or null if it has never been used.</summary>
    /// <remarks>
    /// Transparency is noise cancellation at zero, so while it is on the
    /// headset no longer holds the level it came from.
    /// </remarks>
    [JsonPropertyName("noise_blocking")] public int? NoiseBlocking { get; set; }

    /// <summary>Whether a press of the Mode button steps through noise cancellation, transparency and off.</summary>
    /// <remarks>
    /// The headset has no such function; see <see cref="Core.Settings.ModeButton"/>.
    /// </remarks>
    [JsonPropertyName("mode_cycles_noise")] public bool ModeCyclesNoise { get; set; }

    /// <summary>Whether the headset's firmware is edited to step the Mode button through off, transparency and cancelling itself.</summary>
    /// <remarks>
    /// Off by default: stock firmware is what every headset ships with, and
    /// nothing the headset reports tells the two apart.
    /// </remarks>
    [JsonPropertyName("headset_cycles_noise")] public bool HeadsetCyclesNoise { get; set; }

    /// <summary>Whether the Firmware page, for installing and removing firmware edits, is in the menu.</summary>
    /// <remarks>Off by default: a firmware edit can stop a headset starting, so nothing about them shows until asked for.</remarks>
    [JsonPropertyName("show_firmware_edits")] public bool ShowFirmwareEdits { get; set; }

    /// <summary>Whether the Charging Dock's status ring is turned purple again after the microphone opens.</summary>
    /// <remarks>
    /// On by default: without it the ring shows white through every call.
    /// The switch is there for anyone who would rather not have the short gap
    /// in the sound, and for telling the dock's behaviour from Neap's.
    /// </remarks>
    [JsonPropertyName("keep_ring_purple")] public bool KeepRingPurple { get; set; } = true;

    /// <summary>Whether the system's microphone mute was last set by following the headset, rather than by the person.</summary>
    /// <remarks>
    /// Kept across restarts: a mute the headset set while Neap was running
    /// is still the headset's when Neap next starts, and is undone once the
    /// arm is seen down; see <see cref="Core.Settings.Microphone.SystemMuteFor"/>.
    /// </remarks>
    [JsonPropertyName("mic_muted_by_headset")] public bool MicMutedByHeadset { get; set; }

    /// <summary>The mic monitoring level before the person last muted from the app, to put back on unmuting, or null if none is kept.</summary>
    /// <remarks>Kept across restarts, so a mute left on when Neap closed is still undone; see <see cref="Core.Settings.MonitoringWhileMuted"/>.</remarks>
    [JsonPropertyName("monitoring_before_mute")] public int? MonitoringBeforeMute { get; set; }

    /// <summary>Whether a click on the tray icon opens the mini panel rather than the window.</summary>
    /// <remarks>
    /// On by default: the panel is the quick way to the settings changed
    /// mid-game. The switch is there for anyone who would rather the icon
    /// open the window.
    /// </remarks>
    [JsonPropertyName("mini_panel")] public bool MiniPanel { get; set; } = true;

    /// <summary>The names of the controls the mini panel shows, or null while the person has not chosen; see <see cref="Core.MiniTiles"/>.</summary>
    [JsonPropertyName("mini_tiles")] public List<string>? MiniTiles { get; set; }

    /// <summary>Whether Neap asks GitHub once a day for a newer version.</summary>
    /// <remarks>
    /// On by default: an app that never says it is out of date leaves people
    /// on old bugs. The switch is there for anyone who wants Neap entirely
    /// offline.
    /// </remarks>
    [JsonPropertyName("check_for_updates")] public bool CheckForUpdates { get; set; } = true;

    /// <summary>When Neap last asked GitHub for a newer version, or null if never.</summary>
    [JsonPropertyName("checked_for_updates")] public DateTimeOffset? CheckedForUpdates { get; set; }

    /// <summary>The newer release found at the last check, as <see cref="Neap.Core.Updates.Release.ToJson"/> keeps it, or null if the last check found none.</summary>
    /// <remarks>
    /// So that a restart doesn't forget a version found earlier in the day:
    /// the next check may be hours away.
    /// </remarks>
    [JsonPropertyName("found_release")] public string? FoundRelease { get; set; }

    /// <summary>The version whose banner was put away for good, or null.</summary>
    /// <remarks>A newer version is announced again; see <see cref="Neap.Core.Updates.UpdateReminder"/>.</remarks>
    [JsonPropertyName("skipped_version")] public string? SkippedVersion { get; set; }

    /// <summary>When the update banner may come back after being put away for a day, or null.</summary>
    [JsonPropertyName("update_hidden_until")] public DateTimeOffset? UpdateHiddenUntil { get; set; }

    /// <summary>The newest version a notification has announced, so each is announced once.</summary>
    [JsonPropertyName("told_about_version")] public string? ToldAboutVersion { get; set; }

    /// <summary>Where the window was left: left, top, width and height, in physical pixels.</summary>
    /// <remarks>
    /// Null until the window has been closed once. Its size is the person's
    /// choice; where it goes back to is decided by <see cref="WindowPlacement"/>.
    /// </remarks>
    [JsonPropertyName("window")] public int[]? Window { get; set; }

    /// <summary>Changed key combinations, by name.</summary>
    /// <remarks>
    /// Only the ones actually changed are written, so the defaults stay the
    /// defaults and can be improved later without overriding a choice nobody
    /// made.
    /// </remarks>
    [JsonPropertyName("mix_hotkey_keys")]
    public Dictionary<string, int[]> MixHotkeyKeys { get; set; } = new();

    /// <summary>Whatever has been rebound, ignoring anything malformed.</summary>
    public Dictionary<MixKey, Shortcut> Shortcuts()
    {
        var found = new Dictionary<MixKey, Shortcut>();
        foreach (var (name, pair) in MixHotkeyKeys)
        {
            if (pair is not { Length: 2 }) continue;
            if (!Enum.TryParse(name, out MixKey which)) continue;
            found[which] = new Shortcut((uint)pair[0], (uint)pair[1]);
        }
        return found;
    }

    public void SetShortcut(MixKey which, Shortcut shortcut) =>
        MixHotkeyKeys[which.ToString()] = new[] { (int)shortcut.Modifiers, (int)shortcut.Key };

    public void ClearShortcuts() => MixHotkeyKeys.Clear();

    /// <summary>Whether the true parametric equaliser has been turned on from the Firmware page.</summary>
    [JsonPropertyName("parametric_on")]
    public bool ParametricOn { get; set; }

    /// <summary>Whether the equaliser is in its Parametric mode, so the saved shape is what the headset plays.</summary>
    [JsonPropertyName("parametric_active")]
    public bool ParametricActive { get; set; }

    /// <summary>The firmware the headset had when parametric was turned on, so a later update can be told; null when it is off.</summary>
    [JsonPropertyName("parametric_firmware")]
    public string? ParametricFirmware { get; set; }

    /// <summary>Whether Swarm II reset the headset's equaliser and so turned parametric off, until it is turned on or restored again.</summary>
    [JsonPropertyName("parametric_reset")]
    public bool ParametricReset { get; set; }

    /// <summary>The ten bands as the person last left them, or none before they have made any.</summary>
    /// <remarks>
    /// Kept here because the headset holds a shape only as far as its own
    /// bands go, and an update or recovery from Swarm II puts the stock bands
    /// back; this is what is sent again.
    /// </remarks>
    [JsonPropertyName("parametric_shape")]
    public List<StoredBand> ParametricShape { get; set; } = new();

    /// <summary>The ten bands' gain settings, in decibels, as the headset last reported them, so a change by another app can be told.</summary>
    [JsonPropertyName("parametric_seen_gains")]
    public List<double> ParametricSeenGains { get; set; } = new();

    /// <summary>Whether another app changed the ten-band equaliser while Parametric was in use, so Neap went back to it; said until Parametric is chosen again.</summary>
    [JsonPropertyName("parametric_taken_over")]
    public bool ParametricTakenOver { get; set; }

    /// <summary>The parametric shapes the person saved under names, in the order saved.</summary>
    /// <remarks>
    /// Not "parametric_presets": versions up to 0.3.0 store something else
    /// under that name, and a file they wrote has to load here.
    /// </remarks>
    [JsonPropertyName("parametric_shape_presets")]
    public List<StoredParametricPreset> ParametricPresets { get; set; } = new();

    /// <summary>The name of the parametric preset the shape came from, or null when it came from none.</summary>
    [JsonPropertyName("parametric_preset")]
    public string? ParametricPresetName { get; set; }

    /// <summary>Whether the parametric curve shows the headset's measured response, and the response with the bands added.</summary>
    [JsonPropertyName("parametric_show_measured")]
    public bool ParametricShowMeasured { get; set; } = true;

    /// <summary>Whether the parametric curve shows the target.</summary>
    [JsonPropertyName("parametric_show_target")]
    public bool ParametricShowTarget { get; set; } = true;

    /// <summary>The measurements the person loaded, kept beside the built-in one.</summary>
    [JsonPropertyName("parametric_measurements")]
    public List<StoredCurve> ParametricMeasurements { get; set; } = [];

    /// <summary>The name of the loaded measurement in use, or null for a built-in one.</summary>
    [JsonPropertyName("parametric_measurement_name")]
    public string? ParametricMeasurementName { get; set; }

    /// <summary>Whether the built-in measurement in use is the one with noise cancellation on.</summary>
    [JsonPropertyName("parametric_measured_anc_on")]
    public bool ParametricMeasuredAncOn { get; set; }

    /// <summary>The targets the person loaded, kept beside the built-in one.</summary>
    [JsonPropertyName("parametric_targets")]
    public List<StoredCurve> ParametricTargets { get; set; } = [];

    /// <summary>The name of the loaded target in use, or null for the built-in one.</summary>
    [JsonPropertyName("parametric_target_name")]
    public string? ParametricTargetName { get; set; }

    /// <summary>A frequency response loaded from a file: its name and its text, read again each time it is shown.</summary>
    public sealed class StoredCurve
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("text")] public string Text { get; set; } = "";
    }

    /// <summary>A parametric preset as stored. <see cref="ParametricService"/> is the only reader and writer.</summary>
    public sealed class StoredParametricPreset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("bands")] public List<StoredBand> Bands { get; set; } = new();
    }

    /// <summary>
    /// The preset each bank was left on an edit of.
    /// </summary>
    /// <remarks>
    /// The headset forgets which preset an edit came from, so after a restart
    /// an edited curve would otherwise be nobody's.
    /// </remarks>
    [JsonPropertyName("equaliser_left_on")]
    public Dictionary<string, LeftOn> EqualiserLeftOn { get; set; } = new();

    /// <summary>Where one bank was left.</summary>
    public sealed class LeftOn
    {
        [JsonPropertyName("preset")] public string? Preset { get; set; }
    }

    /// <summary>The preset a bank was left on an edit of.</summary>
    public string? LeftOnFor(Bank bank) =>
        EqualiserLeftOn.TryGetValue(bank.ToString(), out var left) ? left.Preset : null;

    /// <summary>Records where a bank was left.</summary>
    public void SetLeftOn(Bank bank, string? preset) =>
        EqualiserLeftOn[bank.ToString()] = new LeftOn { Preset = preset };

    /// <summary>Profiles saved on this PC, in the order they were made.</summary>
    [JsonPropertyName("profiles")] public List<StoredProfile> Profiles { get; set; } = new();

    /// <summary>The id of the profile the headset was last set to match, or null if none has been applied.</summary>
    [JsonPropertyName("active_profile")] public string? ActiveProfileId { get; set; }

    /// <summary>The id of the profile for the desktop, which comes back when the last app with a profile of its own closes; null if none is set.</summary>
    [JsonPropertyName("default_profile")] public string? DefaultProfileId { get; set; }

    /// <summary>What an app assigned to a profile is called, by process name, so it is named when it is not running.</summary>
    /// <remarks>Filled in place when read, so it keeps ignoring case after a restart.</remarks>
    [JsonPropertyName("app_names"), JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, string> AppNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A profile's fields as stored. <see cref="ProfileService"/> is the only
    /// reader and writer, and maps every field to and from <see cref="Neap.Core.Profiles.ProfileSettings"/>.
    /// </summary>
    public sealed class StoredProfile
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("noise_mode")] public NoiseMode NoiseMode { get; set; }
        [JsonPropertyName("noise_level")] public int NoiseLevel { get; set; }
        [JsonPropertyName("shh")] public bool SuperhumanHearing { get; set; }
        [JsonPropertyName("shh_preset")] public int ShhPreset { get; set; }
        [JsonPropertyName("shh_level")] public int ShhLevel { get; set; }
        [JsonPropertyName("noise_gate")] public bool NoiseGate { get; set; }
        [JsonPropertyName("noise_gate_threshold")] public int NoiseGateThreshold { get; set; }
        [JsonPropertyName("ai_noise_reduction")] public bool AiNoiseReduction { get; set; }
        [JsonPropertyName("mic_monitoring")] public int MicMonitoring { get; set; }
        [JsonPropertyName("game_preset")] public string? GamePreset { get; set; }
        [JsonPropertyName("mic_preset")] public string? MicPreset { get; set; }
        [JsonPropertyName("spatial")] public SpatialFormat Spatial { get; set; }
        [JsonPropertyName("auto_shutoff")] public int AutoShutoff { get; set; }
        [JsonPropertyName("mode_button_function")] public int ModeButtonFunction { get; set; }
        [JsonPropertyName("mode_button_cycles")] public bool ModeButtonCycles { get; set; }
        [JsonPropertyName("dial_function")] public int DialFunction { get; set; }
        [JsonPropertyName("mix")] public int? Mix { get; set; }
        [JsonPropertyName("game_shape")] public List<StoredBand>? GameShape { get; set; }
        [JsonPropertyName("assigned_apps")] public List<string> AssignedApps { get; set; } = new();
        [JsonPropertyName("microphone_apps")] public List<string> MicrophoneApps { get; set; } = new();
    }

    private static readonly string Path = System.IO.Path.Combine(AppFolder.Path, "app-settings.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>How many times the file is tried before it is taken as unreadable for this run.</summary>
    private const int ReadAttempts = 5;

    private static readonly object Gate = new();
    private static AppSettings? _current;

    /// <summary>Whether the file is there but could not be opened, so nothing is saved over it this run.</summary>
    private static bool _held;

    public static AppSettings Current
    {
        get
        {
            lock (Gate) return _current ??= Load();
        }
    }

    public static void Update(Action<AppSettings> change)
    {
        lock (Gate)
        {
            var settings = _current ??= Load();
            change(settings);
            Save(settings);
        }
    }

    /// <remarks>
    /// <para>
    /// A file another program holds for a moment, such as a virus scanner or a
    /// backup, is tried again. One still held is left alone for the whole run
    /// rather than saved over, as there is nothing wrong with it.
    /// </para>
    /// <para>
    /// A file that opens but can't be read is copied aside before the defaults
    /// are used, as the next change saves over it.
    /// </para>
    /// <para>
    /// Either way the defaults leave the update check off: it may have been
    /// turned off, and asking GitHub is the one thing Neap does over the
    /// network.
    /// </para>
    /// </remarks>
    private static AppSettings Load()
    {
        if (!File.Exists(Path)) return new AppSettings();
        string text;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                text = File.ReadAllText(Path);
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt < ReadAttempts)
                {
                    Thread.Sleep(200);
                    continue;
                }
                AppLog.Write($"could not open the app's settings, so it starts from its defaults and saves nothing over them until it is restarted: {ex.Message}");
                _held = true;
                return new AppSettings { CheckForUpdates = false };
            }
        }

        try { return JsonSerializer.Deserialize<AppSettings>(text) ?? new(); }
        catch (Exception ex)
        {
            AppLog.Write($"could not read the app's settings, so it starts from its defaults with the update check off: {ex.Message}");
            string kept = Path + ".unreadable";
            try
            {
                File.Copy(Path, kept, overwrite: true);
                AppLog.Write($"the settings that could not be read are kept in {kept}");
            }
            catch (Exception copyEx) when (copyEx is IOException or UnauthorizedAccessException)
            {
                AppLog.Write($"could not keep the settings that could not be read: {copyEx.Message}");
            }
            return new AppSettings { CheckForUpdates = false };
        }
    }

    private static void Save(AppSettings settings)
    {
        // The log says so once, when the file is found held.
        if (_held) return;
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            WholeFile.Write(Path, JsonSerializer.Serialize(settings, Indented));
        }
        catch (Exception ex)
        {
            // A preference that cannot be saved is not worth failing over.
            AppLog.Write($"could not save the app's settings: {ex.Message}");
        }
    }
}
