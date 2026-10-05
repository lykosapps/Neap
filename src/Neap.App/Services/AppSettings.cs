using System.Text.Json;
using System.Text.Json.Serialization;
using Neap.Core;
using Neap.Core.Audio;
using Neap.Core.Presets;
using Neap.Core.Settings;

namespace Neap.App.Services;

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
/// control the headset has no place for, the parametric adjustments
/// behind a preset, of which its slot holds only the result, which
/// preset an edited curve came from, whether to keep the Charging Dock's
/// status ring purple, and whether and when to look for a newer version.
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

    /// <summary>Whether the Charging Dock's status ring is turned purple again after the microphone opens.</summary>
    /// <remarks>
    /// On by default: without it the ring shows white through every call.
    /// The switch is there for anyone who would rather not have the short gap
    /// in the sound, and for telling the dock's behaviour from Neap's.
    /// </remarks>
    [JsonPropertyName("keep_ring_purple")] public bool KeepRingPurple { get; set; } = true;

    /// <summary>Whether Neap asks GitHub once a day for a newer version.</summary>
    /// <remarks>
    /// On by default: an app that never says it is out of date leaves people
    /// on old bugs. The switch is there for anyone who wants Neap entirely
    /// offline.
    /// </remarks>
    [JsonPropertyName("check_for_updates")] public bool CheckForUpdates { get; set; } = true;

    /// <summary>When Neap last asked GitHub for a newer version, or null if never.</summary>
    [JsonPropertyName("checked_for_updates")] public DateTimeOffset? CheckedForUpdates { get; set; }

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

    /// <summary>
    /// The parametric adjustments behind each preset saved from the
    /// parametric equaliser, by bank and then by preset name, each as
    /// frequency, gain and width.
    /// </summary>
    /// <remarks>
    /// The headset's slot holds only the ten gains they fit to, so a preset
    /// still plays as made without Neap.
    /// </remarks>
    [JsonPropertyName("parametric_presets")]
    public Dictionary<string, Dictionary<string, int[][]>> ParametricPresets { get; set; } = new();

    /// <summary>
    /// The ten gains each preset in <see cref="ParametricPresets"/> was saved
    /// with, by bank and then by preset name.
    /// </summary>
    /// <remarks>
    /// A preset whose slot no longer holds these has been changed elsewhere
    /// since, and its adjustments no longer describe it. A preset stored
    /// without them is fitted again.
    /// </remarks>
    [JsonPropertyName("parametric_bands")]
    public Dictionary<string, Dictionary<string, int[]>> ParametricBands { get; set; } = new();

    /// <summary>
    /// The curve each bank was left on: the preset it was an edit of and, if
    /// it was shaped parametrically, its adjustments and the gains they came to.
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
        [JsonPropertyName("adjustments")] public int[][]? Adjustments { get; set; }
        [JsonPropertyName("bands")] public int[]? Bands { get; set; }
    }

    /// <summary>The preset a bank was left on an edit of, and its shape, if it had one and it is well formed.</summary>
    public (string? Preset, ParametricShape? Shape) LeftOnFor(Bank bank)
    {
        if (!EqualiserLeftOn.TryGetValue(bank.ToString(), out var left)) return (null, null);
        return (left.Preset, Shape(left.Adjustments, left.Bands));
    }

    /// <summary>Records where a bank was left.</summary>
    public void SetLeftOn(Bank bank, string? preset, ParametricShape? shape) =>
        EqualiserLeftOn[bank.ToString()] = new LeftOn
        {
            Preset = preset,
            Adjustments = shape is null ? null : Stored(shape.Adjustments),
            Bands = shape?.Bands.ToArray(),
        };

    /// <summary>A preset's stored shape, or null if it has none or it is malformed.</summary>
    public ParametricShape? Shape(Bank bank, string name)
    {
        string key = bank.ToString();
        if (!ParametricPresets.TryGetValue(key, out var presets) || !presets.TryGetValue(name, out var stored))
            return null;
        return Shape(stored, ParametricBands.GetValueOrDefault(key)?.GetValueOrDefault(name));
    }

    /// <summary>Stores a preset's shape, or forgets it when given null.</summary>
    public void SetShape(Bank bank, string name, ParametricShape? shape)
    {
        string key = bank.ToString();
        if (shape is null)
        {
            ParametricPresets.GetValueOrDefault(key)?.Remove(name);
            ParametricBands.GetValueOrDefault(key)?.Remove(name);
            return;
        }
        if (!ParametricPresets.TryGetValue(key, out var adjustments))
            ParametricPresets[key] = adjustments = new();
        if (!ParametricBands.TryGetValue(key, out var bands))
            ParametricBands[key] = bands = new();
        adjustments[name] = Stored(shape.Adjustments);
        bands[name] = shape.Bands.ToArray();
    }

    private static int[][] Stored(IReadOnlyList<Adjustment> adjustments) =>
        adjustments.Select(a => new[] { a.Frequency, a.Gain, a.Width }).ToArray();

    /// <summary>A shape from its stored form, or null if there is none or it is malformed.</summary>
    private static ParametricShape? Shape(int[][]? stored, int[]? bands)
    {
        if (stored is null || stored.Any(a => a is not { Length: 3 })) return null;
        var adjustments = stored.Select(a => new Adjustment(a[0], a[1], a[2])).ToList();
        return new ParametricShape(adjustments, bands ?? ParametricEq.Fit(adjustments));
    }

    /// <summary>Profiles saved on this PC, in the order they were made.</summary>
    [JsonPropertyName("profiles")] public List<StoredProfile> Profiles { get; set; } = new();

    /// <summary>The id of the profile the headset was last set to match, or null if none has been applied.</summary>
    [JsonPropertyName("active_profile")] public string? ActiveProfileId { get; set; }

    /// <summary>The id of the profile for the desktop, which comes back when the last app with a profile of its own closes; null if none is set.</summary>
    [JsonPropertyName("default_profile")] public string? DefaultProfileId { get; set; }

    /// <summary>What an app assigned to a profile is called, by process name, so it is named when it is not running.</summary>
    [JsonPropertyName("app_names")] public Dictionary<string, string> AppNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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
        [JsonPropertyName("assigned_apps")] public List<string> AssignedApps { get; set; } = new();
    }

    private static readonly string Path = System.IO.Path.Combine(AppFolder.Path, "app-settings.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static readonly object Gate = new();
    private static AppSettings? _current;

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

    private static AppSettings Load()
    {
        if (!File.Exists(Path)) return new AppSettings();
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path)) ?? new(); }
        catch (Exception ex)
        {
            AppLog.Write($"could not read the app's settings, so it starts from its defaults: {ex.Message}");
            return new AppSettings();
        }
    }

    private static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(settings, Indented));
        }
        catch (Exception ex)
        {
            // A preference that cannot be saved is not worth failing over.
            AppLog.Write($"could not save the app's settings: {ex.Message}");
        }
    }
}
