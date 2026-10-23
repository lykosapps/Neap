using Neap.Core.Audio;
using Neap.Core.Connection;
using Neap.Core.Diagnostics;
using Neap.Core.Settings;

namespace Neap.Core.Profiles;

/// <summary>What you hear and how you're heard, saved together under one name.</summary>
/// <remarks>
/// <para>
/// Left out on purpose: every volume, since the wheel and Windows already own
/// those, lighting, and Dolby Access's own equaliser. Spatial sound itself is
/// included even though it is Windows' setting rather than the headset's,
/// because switching it with everything else is the point of a profile.
/// </para>
/// <para>
/// The game/chat mix is held only on a headset with no chat wheel, where
/// nothing on the headset owns it; see <see cref="ProfileGate.HoldsMix"/>.
/// <see cref="Mix"/> is null for a profile that holds none.
/// </para>
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
    int DialFunction,
    int? Mix = null);

/// <summary>A profile, saved under a name the person chose, with the apps that switch to it.</summary>
/// <param name="Id">What the profile is known by, whatever it is called.</param>
/// <param name="Name">The name the person gave it.</param>
/// <param name="AssignedApps">The apps that switch to it, by process name, "witcher3" for witcher3.exe.</param>
/// <param name="MicrophoneApps">
/// Of <paramref name="AssignedApps"/>, those that switch to it only while
/// they are using a microphone, rather than while they are open.
/// </param>
/// <param name="Settings">What it puts on the headset.</param>
public sealed record Profile(string Id, string Name, IReadOnlyList<string> AssignedApps,
    IReadOnlyList<string> MicrophoneApps, ProfileSettings Settings);

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

/// <summary>Whether the headset now differs from a saved profile.</summary>
public static class ProfileEdits
{
    /// <summary>
    /// Whether the live settings differ from the saved ones. An equaliser
    /// preset the profile never saved, because the equaliser had not been read
    /// when it was, is not something the person can have changed.
    /// </summary>
    /// <remarks>The same goes for a mix the profile never saved.</remarks>
    public static bool IsEdited(ProfileSettings saved, ProfileSettings live) =>
        saved with
        {
            GamePreset = saved.GamePreset ?? live.GamePreset,
            MicPreset = saved.MicPreset ?? live.MicPreset,
            Mix = saved.Mix ?? live.Mix,
        } != live;

    /// <summary>The live settings, with those the headset does not hold taken from the active profile; see <see cref="ProfileGate.Holds"/>.</summary>
    /// <remarks>
    /// A profile saved on one headset and used on another that lacks some of
    /// its settings, noise cancellation on the Atlas Air among them, would
    /// otherwise read as edited for ever, and saving over it would lose what
    /// the first headset had.
    /// </remarks>
    public static ProfileSettings Keeping(ProfileSettings live, ProfileSettings? active, HeadsetModel? model)
    {
        if (active is null) return live;
        bool Holds(string setting) => ProfileGate.Holds(model, setting);
        return live with
        {
            NoiseMode = Holds("anc") ? live.NoiseMode : active.NoiseMode,
            NoiseLevel = Holds("anc_level") ? live.NoiseLevel : active.NoiseLevel,
            SuperhumanHearing = Holds("superhuman_hearing") ? live.SuperhumanHearing : active.SuperhumanHearing,
            ShhPreset = Holds("shh_preset") ? live.ShhPreset : active.ShhPreset,
            ShhLevel = Holds("shh_level") ? live.ShhLevel : active.ShhLevel,
            NoiseGate = Holds("noise_gate") ? live.NoiseGate : active.NoiseGate,
            NoiseGateThreshold = Holds("noise_gate_threshold") ? live.NoiseGateThreshold : active.NoiseGateThreshold,
            AiNoiseReduction = Holds("ai_noise_reduction") ? live.AiNoiseReduction : active.AiNoiseReduction,
            MicMonitoring = Holds("mic_monitoring") ? live.MicMonitoring : active.MicMonitoring,
            AutoShutoff = Holds("auto_shutoff") ? live.AutoShutoff : active.AutoShutoff,
            ModeButton = Holds("mode_button_function") ? live.ModeButton : active.ModeButton,
            DialFunction = Holds("dial_function") ? live.DialFunction : active.DialFunction,
            Mix = ProfileGate.HoldsMix(model) ? live.Mix : active.Mix,
        };
    }
}

/// <summary>Whether a profile can be switched to or saved right now, and if not, why.</summary>
public enum ProfileReadiness
{
    Ready,

    /// <summary>The headset's settings are out of reach, for whatever reason the header gives: nothing can be written to it or read from it.</summary>
    HeadsetNotAnswering,

    /// <summary>The headset answers, but its settings have not all been read yet.</summary>
    Reading,
}

/// <summary>What makes a profile switchable.</summary>
public static class ProfileGate
{
    public static ProfileReadiness Of(Link link, bool settingsRead) => link switch
    {
        Link.Connected when settingsRead => ProfileReadiness.Ready,
        Link.Connected => ProfileReadiness.Reading,
        _ => ProfileReadiness.HeadsetNotAnswering,
    };

    /// <summary>Whether a profile reads and writes a setting on a headset: one it has and Neap can read.</summary>
    /// <remarks>
    /// A profile is ready once every setting it holds has been read, so one
    /// that waited on a setting the headset lacks would never be ready. A
    /// setting it does not hold is left alone when a profile is switched to.
    /// </remarks>
    public static bool Holds(HeadsetModel? model, string setting) =>
        HeadsetModels.Shows(model, setting) && !HeadsetModels.Unread(model, setting);

    /// <summary>Whether a profile holds the game/chat mix on a headset: one known to have no chat wheel.</summary>
    /// <remarks>
    /// On a headset with a chat wheel the wheel owns the mix, and a profile
    /// moving it would leave the wheel saying one thing and the mix another.
    /// </remarks>
    public static bool HoldsMix(HeadsetModel? model) =>
        model is not null && !model.Features.Contains(Feature.ChatWheel);
}

/// <summary>How an app is named in a profile: the way the running one is, by its process name.</summary>
public static class ProgramName
{
    /// <summary>The process name of a program picked as a file, "witcher3" for a file called witcher3.exe.</summary>
    public static string Of(string path) => Path.GetFileNameWithoutExtension(path);
}

/// <summary>Which profile an app belongs to, kept to at most one, and when it switches to it.</summary>
public static class ProfileAssignment
{
    /// <summary>
    /// Assigns an app to a profile, taking it away from whichever profile it
    /// belonged to before: an app going to two profiles at once would leave
    /// nothing to decide between them later.
    /// </summary>
    /// <remarks>An app moved from another profile switches while it is open, whatever it did there.</remarks>
    public static IReadOnlyList<Profile> Assign(IReadOnlyList<Profile> profiles, string profileId, string app) =>
        profiles.Select(p => p.Id == profileId ? p with { AssignedApps = Added(p.AssignedApps, app) } : Without(p, app)).ToList();

    /// <summary>Takes an app away from whichever profile holds it.</summary>
    public static IReadOnlyList<Profile> Unassign(IReadOnlyList<Profile> profiles, string app) =>
        profiles.Select(p => Without(p, app)).ToList();

    /// <summary>
    /// Sets whether an assigned app switches to its profile only while it is
    /// using a microphone, or while it is open. An app no profile holds is
    /// left alone.
    /// </summary>
    public static IReadOnlyList<Profile> SwitchOnMicrophone(IReadOnlyList<Profile> profiles, string app, bool onMicrophone) =>
        profiles.Select(p => p.AssignedApps.FirstOrDefault(a => string.Equals(a, app, StringComparison.OrdinalIgnoreCase)) is not { } assigned ? p
            : p with { MicrophoneApps = onMicrophone ? Added(p.MicrophoneApps, assigned) : Removed(p.MicrophoneApps, assigned) }).ToList();

    private static Profile Without(Profile profile, string app) =>
        profile with { AssignedApps = Removed(profile.AssignedApps, app), MicrophoneApps = Removed(profile.MicrophoneApps, app) };

    private static IReadOnlyList<string> Added(IReadOnlyList<string> apps, string app) =>
        apps.Contains(app, StringComparer.OrdinalIgnoreCase) ? apps : [.. apps, app];

    private static IReadOnlyList<string> Removed(IReadOnlyList<string> apps, string app) =>
        apps.Any(a => string.Equals(a, app, StringComparison.OrdinalIgnoreCase))
            ? apps.Where(a => !string.Equals(a, app, StringComparison.OrdinalIgnoreCase)).ToList()
            : apps;
}
