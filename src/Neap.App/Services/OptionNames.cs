using Neap.Core.Settings;

namespace Neap.App.Services;

/// <summary>The words shown for a setting's options, from the resource file.</summary>
/// <remarks>
/// The registry names every option in English for the probe, the pretend
/// headset and the log. What a person reads comes from here instead, so it
/// can be translated. An option with no entry shows the registry's name and
/// leaves a line in the log, since it is a gap to fill.
/// </remarks>
public static class OptionNames
{
    private static readonly Dictionary<(string Setting, int Value), string> Keys = new()
    {
        [("auto_shutoff", 0)] = "Shutoff_Off",
        [("auto_shutoff", 1)] = "Shutoff_FiveMinutes",
        [("auto_shutoff", 2)] = "Shutoff_TenMinutes",
        [("auto_shutoff", 3)] = "Shutoff_TwentyMinutes",
        [("auto_shutoff", 4)] = "Shutoff_ThirtyMinutes",
        [("dial_function", 1)] = "Dial_MicMonitoring",
        [("dial_function", 2)] = "Dial_Mix",
        [("dial_function", 3)] = "Dial_Bass",
        [("dial_function", 4)] = "Dial_Treble",
        [("dial_function", 5)] = "Dial_NoiseGate",
        [("mode_button_function", 0)] = "Mode_Noise",
        [("mode_button_function", 1)] = "Mode_NextPreset",
        [("mode_button_function", 2)] = "Mode_NoiseGate",
        [("shh_preset", 0)] = "Shh_Legacy",
        [("shh_preset", 1)] = "Shh_Footsteps",
        [("shh_preset", 2)] = "Shh_Gunshots",
    };

    public static string For(SettingKey key, int value)
    {
        if (Keys.TryGetValue((key.Name, value), out string? name)) return Strings.Get(name);
        AppLog.Write($"no resource for option {value} of {key.Name}; showing the registry's name");
        return key.Options?.GetValueOrDefault(value) ?? value.ToString(System.Globalization.CultureInfo.CurrentCulture);
    }
}
