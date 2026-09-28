using Microsoft.UI.Dispatching;
using Neap.Core.Connection;
using Neap.Core.Presets;
using Neap.Core.Profiles;
using Neap.Core.Settings;

namespace Neap.App.Services;

/// <summary>
/// Profiles: switched from the header bar, edited from Settings. Saved on this
/// PC; see <see cref="AppSettings.StoredProfile"/>.
/// </summary>
/// <remarks>
/// <para>
/// Everything a profile holds is either a plain headset setting, read and
/// written by name through <see cref="HeadsetService"/>, or one of the three
/// things with a rule of their own: noise control goes through
/// <see cref="NoiseService"/> so a profile's write is never taken for a press
/// of the Mode button, the equaliser presets go through
/// <see cref="PresetService"/> so the baseline it tracks stays right, and
/// spatial sound goes through <see cref="SpatialAudio"/>, which is Windows'
/// setting rather than the headset's.
/// </para>
/// <para>
/// <see cref="Current"/> is read fresh by <see cref="Refresh"/> rather than
/// kept live from every headset notification: reading it touches Windows for
/// the spatial format, which is not cheap enough to do on every change. A
/// screen that wants to know whether it is current calls <see cref="Refresh"/>
/// itself, the same way <see cref="PresetService"/>'s callers ask it to load.
/// </para>
/// <para>
/// Spatial sound can also change with nothing here asking for it: from
/// Sound settings itself, or from <see cref="Controls.SpatialTile"/>. Every
/// refresh rewatches whichever endpoint it just read, the same way
/// <see cref="Controls.SpatialTile"/> does, so that catches up too rather
/// than waiting for the next unrelated headset notification.
/// </para>
/// </remarks>
public sealed class ProfileService : IDisposable
{
    private readonly HeadsetService _headset;
    private readonly DispatcherQueue _ui = DispatcherQueue.GetForCurrentThread();
    private Task? _refreshing;
    private IDisposable? _spatialWatch;
    private string? _watchedEndpoint;

    public ProfileService(HeadsetService headset) => _headset = headset;

    public void Dispose() => _spatialWatch?.Dispose();

    /// <summary>The profiles, current settings or active profile changed.</summary>
    public event Action? Changed;

    // Both read the settings file only, with nothing of the instance's own to
    // touch, but stay instance members so every caller reaches profiles the
    // same way, through AppServices.Profiles.
#pragma warning disable CA1822
    public IReadOnlyList<Profile> All => AppSettings.Current.Profiles.Select(ToProfile).ToList();

    public string? ActiveId => AppSettings.Current.ActiveProfileId;
#pragma warning restore CA1822

    public Profile? Active => All.FirstOrDefault(p => p.Id == ActiveId);

    /// <summary>The headset's settings right now, as a profile would hold them. Null until fully read.</summary>
    public ProfileSettings? Current { get; private set; }

    /// <summary>Whether the current settings differ from the active profile's.</summary>
    public bool IsEdited => Active is { } active && Current is { } live && ProfileEdits.IsEdited(active.Settings, live);

    /// <summary>Whether a profile can be switched to or saved now, and if not, why.</summary>
    public ProfileReadiness Ready => ProfileGate.Of(_headset.Status.Link, Current is not null);

    /// <summary>
    /// Re-reads the current settings. A read already under way is shared
    /// rather than started again.
    /// </summary>
    public Task Refresh() => _refreshing is { IsCompleted: false } running ? running : (_refreshing = RefreshCore());

    private async Task RefreshCore()
    {
        Current = await ReadCurrent();
        Changed?.Invoke();
    }

    private async Task<ProfileSettings?> ReadCurrent()
    {
        if (AppServices.Noise.Mode is not { } mode) return null;
        if (!_headset.TryGetNumberByKey(NoiseControl.LevelKey, out int level)) return null;
        if (!Flag("superhuman_hearing", out bool shh)) return null;
        if (!_headset.TryGetNumber("shh_preset", out int shhPreset)) return null;
        if (!_headset.TryGetNumber("shh_level", out int shhLevel)) return null;
        if (!Flag("noise_gate", out bool gate)) return null;
        if (!_headset.TryGetNumber("noise_gate_threshold", out int gateThreshold)) return null;
        if (!Flag("ai_noise_reduction", out bool ai)) return null;
        if (!_headset.TryGetNumber("mic_monitoring", out int monitoring)) return null;
        if (!_headset.TryGetNumber("auto_shutoff", out int autoShutoff)) return null;
        if (!_headset.TryGetNumber("dial_function", out int dial)) return null;
        if (!_headset.TryGetNumber("mode_button_function", out int modeFunction)) return null;

        // Both equaliser banks are read here, in the background, rather than
        // on the first switch: saving without them stores no preset, and
        // switching waits several seconds for them with nothing on screen.
        try { await EnsureBanks(); }
        catch (Exception ex)
        {
            AppLog.Write($"profiles: could not read the equalisers: {ex.Message}");
            return null;
        }

        var spatial = await SpatialAudio.Read();
        Rewatch(spatial.EndpointId);
        if (spatial.Active is not { } format) return null;

        return new ProfileSettings(mode, level, shh, shhPreset, shhLevel, gate, gateThreshold, ai,
            monitoring, AppServices.Presets.State(Bank.Game)?.Baseline?.Name,
            AppServices.Presets.State(Bank.Mic)?.Baseline?.Name, format, autoShutoff,
            new ModeChoice(modeFunction, AppServices.Noise.Cycling), dial);
    }

    private static async Task<(BankState Game, BankState Mic)> EnsureBanks() => (
        AppServices.Presets.State(Bank.Game) ?? await AppServices.Presets.Load(Bank.Game),
        AppServices.Presets.State(Bank.Mic) ?? await AppServices.Presets.Load(Bank.Mic));

    /// <summary>Keeps the watch on whichever endpoint was just read, as <see cref="Controls.SpatialTile"/> does.</summary>
    private void Rewatch(string? endpointId)
    {
        if (endpointId == _watchedEndpoint) return;
        _spatialWatch?.Dispose();
        _watchedEndpoint = endpointId;
        _spatialWatch = endpointId is null
            ? null
            : SpatialAudio.Watch(endpointId, () => _ui.TryEnqueue(() => _ = Refresh()));
    }

    private bool Flag(string name, out bool value)
    {
        bool ok = _headset.TryGetNumber(name, out int raw);
        value = raw != 0;
        return ok;
    }

    /// <summary>
    /// Applies a profile: every setting it holds, then whichever of its saved
    /// equaliser presets are still there.
    /// </summary>
    /// <returns>The names of any saved presets that could not be found, applied or not.</returns>
    public async Task<IReadOnlyList<string>> Apply(Profile profile)
    {
        if (_headset.Status.Link != Link.Connected)
            throw new HeadsetUnavailableException(Strings.Get("Profile_NeedsHeadset"));

        var s = profile.Settings;
        // Already-loaded state is reused rather than asked for again: a full
        // read costs about 1.2 seconds a bank, and switching or discarding a
        // profile should feel as immediate as choosing a preset does.
        var (game, mic) = await EnsureBanks();
        var missing = ProfileCheck.Missing(s, game.Presets.Select(p => p.Name).ToList(),
            mic.Presets.Select(p => p.Name).ToList());

        if (s.GamePreset is { } gp && game.Presets.FirstOrDefault(p => p.Name == gp) is { } gamePreset)
            await AppServices.Presets.Select(Bank.Game, gamePreset);
        if (s.MicPreset is { } mp && mic.Presets.FirstOrDefault(p => p.Name == mp) is { } micPreset)
            await AppServices.Presets.Select(Bank.Mic, micPreset);

        AppServices.Noise.Choose(s.NoiseMode, s.NoiseLevel);
        _headset.Set("superhuman_hearing", s.SuperhumanHearing ? 1 : 0);
        _headset.Set("shh_preset", s.ShhPreset);
        _headset.Set("shh_level", s.ShhLevel);
        _headset.Set("noise_gate", s.NoiseGate ? 1 : 0);
        _headset.Set("noise_gate_threshold", s.NoiseGateThreshold);
        _headset.Set("ai_noise_reduction", s.AiNoiseReduction ? 1 : 0);
        _headset.Set("mic_monitoring", s.MicMonitoring);
        _headset.Set("auto_shutoff", s.AutoShutoff);
        _headset.Set("dial_function", s.DialFunction);
        _headset.Set("mode_button_function", s.ModeButton.Function);
        AppServices.Noise.SetCycling(s.ModeButton.Cycles);
        await SpatialAudio.Apply(s.Spatial);

        AppSettings.Update(a => a.ActiveProfileId = profile.Id);
        await Refresh();
        return missing;
    }

    /// <summary>Puts the headset back to the active profile, undoing whatever changed since.</summary>
    public Task<IReadOnlyList<string>> Discard() =>
        Active is { } active ? Apply(active) : Task.FromResult<IReadOnlyList<string>>([]);

    /// <summary>Saves the current settings as a new profile, and makes it the active one.</summary>
    /// <returns>Null on success, or what went wrong.</returns>
    public string? SaveNew(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return Strings.Get("Profile_GiveName");
        if (Current is not { } settings) return Strings.Get("Profile_NotRead");
        if (AppSettings.Current.Profiles.Any(p => Same(p.Name, name)))
            return Strings.Format("Profile_NameTaken", name);

        string id = Guid.NewGuid().ToString("N");
        AppSettings.Update(a =>
        {
            a.Profiles.Add(ToStored(new Profile(id, name, [], settings)));
            a.ActiveProfileId = id;
        });
        Changed?.Invoke();
        return null;
    }

    /// <summary>Saves the current settings back over the active profile.</summary>
    public void SaveOverActive()
    {
        if (Active is not { } active || Current is not { } settings) return;
        AppSettings.Update(a =>
        {
            int index = a.Profiles.FindIndex(p => p.Id == active.Id);
            if (index >= 0) a.Profiles[index] = ToStored(active with { Settings = settings });
        });
        Changed?.Invoke();
    }

    /// <summary>Renames a saved profile.</summary>
    /// <returns>Null on success, or what went wrong.</returns>
    public string? Rename(string id, string name)
    {
        name = name.Trim();
        if (name.Length == 0) return Strings.Get("Profile_GiveName");
        if (AppSettings.Current.Profiles.Any(p => p.Id != id && Same(p.Name, name)))
            return Strings.Format("Profile_NameTaken", name);

        AppSettings.Update(a =>
        {
            var stored = a.Profiles.FirstOrDefault(p => p.Id == id);
            if (stored is not null) stored.Name = name;
        });
        Changed?.Invoke();
        return null;
    }

    /// <summary>Deletes a saved profile. Deleting the active one leaves none active.</summary>
    public void Delete(string id)
    {
        AppSettings.Update(a =>
        {
            a.Profiles.RemoveAll(p => p.Id == id);
            if (a.ActiveProfileId == id) a.ActiveProfileId = null;
        });
        Changed?.Invoke();
    }

    /// <summary>Assigns an app to a profile, taking it away from whichever profile had it.</summary>
    /// <param name="profileId">The profile.</param>
    /// <param name="app">The app's process name.</param>
    /// <param name="display">What a person would call it, kept for when it is not running to say so.</param>
    public void Assign(string profileId, string app, string display)
    {
        AppSettings.Update(a => a.AppNames[app] = display);
        Persist(ProfileAssignment.Assign(All, profileId, app));
        Changed?.Invoke();
    }

    /// <summary>What a person would call an assigned app, or its process name if it was never seen running.</summary>
#pragma warning disable CA1822
    public string DisplayName(string app) => AppSettings.Current.AppNames.GetValueOrDefault(app, app);
#pragma warning restore CA1822

    /// <summary>Takes an app away from whichever profile it is assigned to.</summary>
    public void Unassign(string app)
    {
        Persist(ProfileAssignment.Unassign(All, app));
        Changed?.Invoke();
    }

    private static void Persist(IReadOnlyList<Profile> profiles) => AppSettings.Update(a =>
    {
        foreach (var profile in profiles)
            if (a.Profiles.FirstOrDefault(p => p.Id == profile.Id) is { } stored)
                stored.AssignedApps = profile.AssignedApps.ToList();
    });

    private static bool Same(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static Profile ToProfile(AppSettings.StoredProfile s) => new(s.Id, s.Name, s.AssignedApps, new ProfileSettings(
        s.NoiseMode, s.NoiseLevel, s.SuperhumanHearing, s.ShhPreset, s.ShhLevel,
        s.NoiseGate, s.NoiseGateThreshold, s.AiNoiseReduction, s.MicMonitoring,
        s.GamePreset, s.MicPreset, s.Spatial, s.AutoShutoff,
        new ModeChoice(s.ModeButtonFunction, s.ModeButtonCycles), s.DialFunction));

    private static AppSettings.StoredProfile ToStored(Profile profile) => new()
    {
        Id = profile.Id,
        Name = profile.Name,
        AssignedApps = profile.AssignedApps.ToList(),
        NoiseMode = profile.Settings.NoiseMode,
        NoiseLevel = profile.Settings.NoiseLevel,
        SuperhumanHearing = profile.Settings.SuperhumanHearing,
        ShhPreset = profile.Settings.ShhPreset,
        ShhLevel = profile.Settings.ShhLevel,
        NoiseGate = profile.Settings.NoiseGate,
        NoiseGateThreshold = profile.Settings.NoiseGateThreshold,
        AiNoiseReduction = profile.Settings.AiNoiseReduction,
        MicMonitoring = profile.Settings.MicMonitoring,
        GamePreset = profile.Settings.GamePreset,
        MicPreset = profile.Settings.MicPreset,
        Spatial = profile.Settings.Spatial,
        AutoShutoff = profile.Settings.AutoShutoff,
        ModeButtonFunction = profile.Settings.ModeButton.Function,
        ModeButtonCycles = profile.Settings.ModeButton.Cycles,
        DialFunction = profile.Settings.DialFunction,
    };
}
