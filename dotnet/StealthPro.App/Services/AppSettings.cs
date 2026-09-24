using System.Text.Json;
using System.Text.Json.Serialization;
using StealthPro.Core;

namespace StealthPro.App.Services;

/// <summary>
/// The handful of things the app has to remember between launches.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately small. Everything about the headset lives on the headset and
/// is read back from it, so there is no local copy to drift out of date. Only
/// choices neither the headset nor Windows can answer for us are stored: which
/// applications carry chat, the keyboard shortcuts, where the window was
/// left, whether the tray notice has been shown, and the two halves of noise
/// control the headset has no place for.
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
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path)) ?? new(); }
        catch { return new AppSettings(); }
    }

    private static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(settings, Indented));
        }
        catch { /* a preference we cannot save is not worth failing over */ }
    }
}
