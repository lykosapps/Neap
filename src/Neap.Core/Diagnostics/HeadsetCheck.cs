using System.Globalization;
using Neap.Core.Settings;

namespace Neap.Core.Diagnostics;

/// <summary>Something Neap does with the headset, as a person would name it.</summary>
public enum Feature
{
    Battery,
    MasterVolume,
    VoicePrompts,
    NoiseControl,
    SuperhumanHearing,
    GameEqualiser,
    Microphone,
    MicMonitoring,
    AiNoiseReduction,
    NoiseGate,
    MicrophoneEqualiser,
    ModeButton,
    LowerDial,
    ChatWheel,
    Lights,
    AutoShutOff,
    WakeOnMotion,
}

/// <summary>What a recording showed of the functions Neap has for the Stealth Pro II.</summary>
/// <param name="Answered">Whether the headset answered Neap at all.</param>
/// <param name="Found">Functions whose every value the headset reported.</param>
/// <param name="Missing">Functions with a value the headset never reported.</param>
/// <param name="Moved">Functions with a value that changed while the recording ran.</param>
public sealed record HeadsetFindings(
    bool Answered, IReadOnlyList<Feature> Found, IReadOnlyList<Feature> Missing,
    IReadOnlyList<Feature> Moved)
{
    /// <summary>The findings in one paragraph of plain English, for the report the developer reads.</summary>
    public string Summary => !Answered
        ? "The headset didn't answer Neap."
        : $"Found: {Names(Found)}. Not found: {Names(Missing)}. Changed while recording: {Names(Moved)}.";

    private static string Names(IReadOnlyList<Feature> features) =>
        features.Count == 0 ? "none" : string.Join(", ", features);
}

/// <summary>
/// Works out, from what a headset reported, which of Neap's functions it has
/// the values for.
/// </summary>
/// <remarks>
/// <para>
/// A person with another headset is told what Neap found before they send a
/// recording, so they know what to expect and the developer knows where to
/// start.
/// </para>
/// <para>
/// Found is not the same as working. A function is found when the headset
/// reports every value Neap reads it from, under the Stealth Pro II's keys.
/// Another headset can report a key with a different meaning, and whether
/// setting it works is only known by setting it, which Neap never does on a
/// headset it does not know.
/// </para>
/// </remarks>
public static class HeadsetCheck
{
    /// <summary>The registry settings each function is read from.</summary>
    public static readonly IReadOnlyDictionary<Feature, IReadOnlyList<string>> Needs =
        new Dictionary<Feature, IReadOnlyList<string>>
        {
            [Feature.Battery] = ["battery"],
            [Feature.MasterVolume] = ["master_volume"],
            [Feature.VoicePrompts] = ["voice_prompt_volume"],
            [Feature.NoiseControl] = ["anc", "anc_level"],
            [Feature.SuperhumanHearing] = ["superhuman_hearing", "shh_level"],
            [Feature.GameEqualiser] = ["eq_preset", "eq_band_1"],
            [Feature.Microphone] = ["mic_muted", "mic_volume"],
            [Feature.MicMonitoring] = ["mic_monitoring"],
            [Feature.AiNoiseReduction] = ["ai_noise_reduction"],
            [Feature.NoiseGate] = ["noise_gate", "noise_gate_threshold"],
            [Feature.MicrophoneEqualiser] = ["mic_eq_preset", "mic_eq_band_1"],
            [Feature.ModeButton] = ["mode_button_function"],
            [Feature.LowerDial] = ["dial_function"],
            [Feature.ChatWheel] = ["game_chat_mix"],
            [Feature.Lights] = ["led_brightness_1", "led_brightness_2"],
            [Feature.AutoShutOff] = ["auto_shutoff"],
            [Feature.WakeOnMotion] = ["wake_on_motion"],
        };

    /// <summary>Which functions were found, missing and moved.</summary>
    /// <param name="reported">The keys the headset reported, as lowercase hex.</param>
    /// <param name="moved">The keys whose value changed while recording, as lowercase hex.</param>
    public static HeadsetFindings Of(IReadOnlySet<string> reported, IReadOnlySet<string> moved)
    {
        var found = new List<Feature>();
        var missing = new List<Feature>();
        var changed = new List<Feature>();
        foreach (var (feature, names) in Needs)
        {
            var keys = names.Select(Hex).ToList();
            (keys.All(reported.Contains) ? found : missing).Add(feature);
            if (keys.Any(moved.Contains)) changed.Add(feature);
        }
        return new HeadsetFindings(reported.Count > 0, found, missing, changed);
    }

    private static string Hex(string name) =>
        Registry.ByName[name].Key.ToString("x", CultureInfo.InvariantCulture);
}
