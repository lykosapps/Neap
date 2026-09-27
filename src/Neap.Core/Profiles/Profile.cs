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

/// <summary>
/// A profile, saved under a name the person chose. <see cref="AssignedApps"/>
/// is by process name, e.g. "witcher3.exe"; switching to it automatically is
/// a later version's job, not this one's.
/// </summary>
public sealed record Profile(string Id, string Name, IReadOnlyList<string> AssignedApps, ProfileSettings Settings);

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

/// <summary>Which profile an app belongs to, kept to at most one.</summary>
public static class ProfileAssignment
{
    /// <summary>
    /// Assigns an app to a profile, taking it away from whichever profile it
    /// belonged to before: an app going to two profiles at once would leave
    /// nothing to decide between them later.
    /// </summary>
    public static IReadOnlyList<Profile> Assign(IReadOnlyList<Profile> profiles, string profileId, string app) =>
        profiles.Select(p => p.Id == profileId ? p with { AssignedApps = Added(p.AssignedApps, app) }
            : p with { AssignedApps = Removed(p.AssignedApps, app) }).ToList();

    /// <summary>Takes an app away from whichever profile holds it.</summary>
    public static IReadOnlyList<Profile> Unassign(IReadOnlyList<Profile> profiles, string app) =>
        profiles.Select(p => p with { AssignedApps = Removed(p.AssignedApps, app) }).ToList();

    private static IReadOnlyList<string> Added(IReadOnlyList<string> apps, string app) =>
        apps.Contains(app, StringComparer.OrdinalIgnoreCase) ? apps : [.. apps, app];

    private static IReadOnlyList<string> Removed(IReadOnlyList<string> apps, string app) =>
        apps.Any(a => string.Equals(a, app, StringComparison.OrdinalIgnoreCase))
            ? apps.Where(a => !string.Equals(a, app, StringComparison.OrdinalIgnoreCase)).ToList()
            : apps;
}
