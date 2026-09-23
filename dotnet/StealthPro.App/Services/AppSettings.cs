using System.Text.Json;
using System.Text.Json.Serialization;

namespace StealthPro.App.Services;

/// <summary>
/// The handful of things the app has to remember between launches.
///
/// Deliberately small. Everything about the headset lives on the headset and
/// is read back from it, so there is no local copy to drift out of date. Only
/// choices Windows cannot answer for us are stored — which applications carry
/// chat, and whether the mix was on.
///
/// Kept separate from the mix's volume journal, which is recovery state
/// rather than preference: losing this file costs a re-pick, losing that one
/// leaves somebody's Spotify quiet.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Which applications carry chat. Having one at all is what "the mix is
    /// on" means, so there is no separate enabled flag to fall out of step
    /// with it.
    /// </summary>
    [JsonPropertyName("chat_apps")] public List<string> ChatApps { get; set; } = new();

    /// <summary>
    /// Whether we have told them that closing the window does not stop the
    /// app. Said once, because the second time it is noise.
    /// </summary>
    [JsonPropertyName("told_about_tray")] public bool ToldAboutTray { get; set; }

    /// <summary>
    /// Whether the mix can be moved by keyboard from inside a game. Off by
    /// default: taking three key combinations away from everything else on
    /// the machine is not something to do to someone without asking.
    /// </summary>
    [JsonPropertyName("mix_hotkeys")] public bool MixHotkeys { get; set; }

    /// <summary>
    /// Changed key combinations, by name. Only the ones actually changed are
    /// written, so the defaults stay the defaults and can be improved later
    /// without overriding a choice nobody made.
    /// </summary>
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

    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StealthProIIControl", "app-settings.json");

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
