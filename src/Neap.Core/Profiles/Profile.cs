using Neap.Core.Audio;
using Neap.Core.Settings;

namespace Neap.Core.Profiles;

/// <summary>What you hear and how you're heard, saved together under one name.</summary>
/// <remarks>
/// Left out on purpose: every volume, since the wheel and Windows already own
/// those, the game/chat mix, lighting, and Dolby Access's own equaliser.
/// Spatial sound itself is included even though it is Windows' setting rather
/// than the headset's, because switching it with everything else is the point
/// of a profile.
/// </remarks>
public sealed record ProfileSettings(
    NoiseMode NoiseMode,
    int NoiseLevel,
    bool SuperhumanHearing,
    int ShhPreset,
    int ShhLevel,
    bool NoiseGate,
    int NoiseGateThreshold,
    bool AiNoiseReduction,
    int MicMonitoring,
    string? GamePreset,
    string? MicPreset,
    SpatialFormat Spatial,
    int AutoShutoff,
    ModeChoice ModeButton,
    int DialFunction);

/// <summary>A profile, saved under a name the person chose.</summary>
public sealed record Profile(string Id, string Name, ProfileSettings Settings);

/// <summary>Whether a profile's saved equaliser presets are still there to apply.</summary>
public static class ProfileCheck
{
    /// <summary>
    /// The names of the profile's saved presets that are no longer among the
    /// headset's presets, so applying it can say which of those it could not
    /// bring along, rather than skip them without a word.
    /// </summary>
    public static IReadOnlyList<string> Missing(ProfileSettings settings,
        IReadOnlyCollection<string> gamePresets, IReadOnlyCollection<string> micPresets)
    {
        var missing = new List<string>();
        if (settings.GamePreset is { } game && !gamePresets.Contains(game)) missing.Add(game);
        if (settings.MicPreset is { } mic && !micPresets.Contains(mic)) missing.Add(mic);
        return missing;
    }
}
