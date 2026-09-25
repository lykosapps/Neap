using System.Text.Json;
using System.Text.Json.Serialization;
using Neap.Core;
using Neap.Core.Presets;

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
/// behind a preset, of which its slot holds only the result, and which
/// preset an edited curve came from.
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

    /// <summary>
    /// The parametric adjustments behind each preset saved from the
    /// parametric equaliser, by bank and then by preset name, each as
    /// frequency, gain and width.
    /// </summary>
    /// <remarks>
    /// The headset's slot holds only the ten gains they fit to, so a preset
    /// still plays as made without Neap. See <see cref="ParametricEq.Matches"/>
    /// for how a preset changed elsewhere since is recognised.
    /// </remarks>
    [JsonPropertyName("parametric_presets")]
    public Dictionary<string, Dictionary<string, int[][]>> ParametricPresets { get; set; } = new();

    /// <summary>
    /// The curve each bank was left on: the preset it was an edit of and, if
    /// it was shaped parametrically, its adjustments.
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
    }

    /// <summary>The preset a bank was left on an edit of, and its adjustments, if any and well formed.</summary>
    public (string? Preset, IReadOnlyList<Adjustment>? Adjustments) LeftOnFor(Bank bank)
    {
        if (!EqualiserLeftOn.TryGetValue(bank.ToString(), out var left)) return (null, null);
        var adjustments = left.Adjustments is { } stored && stored.All(a => a is { Length: 3 })
            ? stored.Select(a => new Adjustment(a[0], a[1], a[2])).ToList()
            : null;
        return (left.Preset, adjustments);
    }

    /// <summary>Records where a bank was left.</summary>
    public void SetLeftOn(Bank bank, string? preset, IReadOnlyList<Adjustment>? adjustments) =>
        EqualiserLeftOn[bank.ToString()] = new LeftOn
        {
            Preset = preset,
            Adjustments = adjustments?.Select(a => new[] { a.Frequency, a.Gain, a.Width }).ToArray(),
        };

    /// <summary>A preset's stored adjustments, or null if it has none or they are malformed.</summary>
    public IReadOnlyList<Adjustment>? Adjustments(Bank bank, string name)
    {
        if (!ParametricPresets.TryGetValue(bank.ToString(), out var presets)
            || !presets.TryGetValue(name, out var stored)
            || stored.Any(a => a is not { Length: 3 })) return null;
        return stored.Select(a => new Adjustment(a[0], a[1], a[2])).ToList();
    }

    /// <summary>Stores a preset's adjustments, or forgets them when given null.</summary>
    public void SetAdjustments(Bank bank, string name, IReadOnlyList<Adjustment>? adjustments)
    {
        string key = bank.ToString();
        if (adjustments is null)
        {
            if (ParametricPresets.TryGetValue(key, out var presets)) presets.Remove(name);
            return;
        }
        if (!ParametricPresets.TryGetValue(key, out var bankPresets))
            ParametricPresets[key] = bankPresets = new();
        bankPresets[name] = adjustments.Select(a => new[] { a.Frequency, a.Gain, a.Width }).ToArray();
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
