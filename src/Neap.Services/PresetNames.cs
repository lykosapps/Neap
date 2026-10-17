using Neap.Core.Presets;

namespace Neap.Services;

/// <summary>The words shown for the headset's own presets, from the resource file.</summary>
/// <remarks>
/// The preset store names each factory preset in English, as the headset
/// does, for the probe and the log. What a person reads comes from here, so
/// it can be translated. A preset with no entry shows the headset's name and
/// leaves a line in the log, since it is a gap to fill.
/// </remarks>
public static class PresetNames
{
    private static readonly Dictionary<(Bank Bank, int Id), string> Keys = new()
    {
        [(Bank.Game, 1)] = "Preset_SignatureSound",
        [(Bank.Game, 2)] = "Preset_BassBoost",
        [(Bank.Game, 3)] = "Preset_BassTrebleBoost",
        [(Bank.Game, 4)] = "Preset_VocalBoost",
        [(Bank.Mic, 1)] = "Preset_SignatureSound",
        [(Bank.Mic, 2)] = "Preset_MicFull",
        [(Bank.Mic, 3)] = "Preset_Clarity",
        [(Bank.Mic, 4)] = "Preset_Smooth",
    };

    /// <summary>The preset with the name a person reads.</summary>
    public static Preset Shown(Preset preset)
    {
        if (Keys.TryGetValue((preset.Bank, preset.Id), out string? key)) return preset with { Name = Strings.Get(key) };
        AppLog.Write($"no resource for the {preset.Bank} preset {preset.Id}; showing the headset's name");
        return preset;
    }
}
