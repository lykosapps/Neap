using System.Diagnostics;
using Neap.Core.Profiles;

namespace Neap.Services;

/// <summary>
/// Switches profiles as the apps assigned to them start and close, or start
/// and stop using a microphone.
/// </summary>
/// <remarks>
/// <para>
/// Which profile should be on is <see cref="AutoSwitch"/>'s rule. This looks at
/// the running programs, and those using a microphone, every couple of
/// seconds, from the notification area as much as the window, and puts the
/// rule's answer on the headset once it can.
/// </para>
/// <para>
/// It waits rather than lose anything: while the profile that is on has
/// unsaved changes, or the headset is off or still being read, the switch is
/// held and <see cref="Waiting"/> says so, and it happens as soon as that
/// clears. A switch never asks anything, since a game may be in front, and a
/// saved preset it cannot find is only logged, for the same reason.
/// </para>
/// <para>
/// Polling rather than Windows' process events: those need administrator
/// rights, and a program is matched by name the same way whether it was
/// running when Neap started or started since. Neither list is asked for
/// while no profile has an app that needs it.
/// </para>
/// </remarks>
public sealed class AutoSwitchService : IDisposable
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(2);

    private readonly ProfileService _profiles;
    private readonly HeadsetService _headset;
    private readonly Func<IReadOnlyCollection<string>> _programs;
    private readonly Func<IReadOnlyCollection<string>> _onMicrophone;
    private readonly AutoSwitch _rule = new();
    private readonly IDisposable _timer;
    private string? _waiting;
    private bool _looking;
    private bool _applying;
    private string _lastFault = "";

    /// <param name="profiles">The profiles to switch between.</param>
    /// <param name="headset">The headset, whose state decides when a switch can happen.</param>
    /// <param name="programs">Every running program, by process name; called off the UI thread.</param>
    /// <param name="onMicrophone">Every program using a microphone, by process name; called off the UI thread.</param>
    public AutoSwitchService(ProfileService profiles, HeadsetService headset,
        Func<IReadOnlyCollection<string>> programs, Func<IReadOnlyCollection<string>> onMicrophone)
    {
        _profiles = profiles;
        _headset = headset;
        _programs = programs;
        _onMicrophone = onMicrophone;
        _profiles.Changed += OnProfilesChanged;
        _profiles.Chosen += OnChosen;
        _headset.StatusChanged += OnStatus;
        _timer = Platform.Current.Ui.Every(Every, () => _ = Look());
    }

    /// <summary>A switch started waiting, or stopped.</summary>
    public event Action? Changed;

    /// <summary>The profile an app starting or closing asked for, while it waits to go on; null when nothing is waiting.</summary>
    public Profile? Waiting => _waiting is { } id ? _profiles.All.FirstOrDefault(p => p.Id == id) : null;

    /// <summary>Every running program on this PC, by process name.</summary>
    public static IReadOnlyCollection<string> RunningPrograms()
    {
        var processes = Process.GetProcesses();
        try { return processes.Select(p => p.ProcessName).ToList(); }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private async Task Look()
    {
        if (_looking) return;
        _looking = true;
        try
        {
            var profiles = _profiles.All;
            IReadOnlyCollection<string> running = profiles.Any(p => p.AssignedApps.Count > p.MicrophoneApps.Count)
                ? await Task.Run(_programs)
                : [];
            IReadOnlyCollection<string> onMicrophone = profiles.Any(p => p.MicrophoneApps.Count > 0)
                ? await Task.Run(_onMicrophone)
                : [];
            if (_rule.Observe(running, onMicrophone, profiles, _profiles.DefaultId, _profiles.ActiveId) is not { } target) return;

            _waiting = target;
            string name = profiles.FirstOrDefault(p => p.Id == target)?.Name ?? target;
            AppLog.Write($"profiles: an app with a profile started or stopped, so {name} is next");
            Changed?.Invoke();
            await TryApply(read: true);
        }
        catch (Exception ex)
        {
            // Once per fault, not every two seconds.
            string line = $"profiles: could not look at the running programs or the microphones: {ex.Message}";
            if (line != _lastFault) AppLog.Write(line);
            _lastFault = line;
        }
        finally { _looking = false; }
    }

    /// <param name="read">
    /// Read the headset first, and decide once that is done. Unsaved changes
    /// are judged against what the headset holds now, which the window may
    /// not have read if Neap is only in the notification area.
    /// </param>
    private async Task TryApply(bool read = false)
    {
        if (_applying || _waiting is not { } id) return;

        if (id == _profiles.ActiveId || _profiles.All.FirstOrDefault(p => p.Id == id) is not { } target)
        {
            _waiting = null;
            Changed?.Invoke();
            return;
        }
        if (read)
        {
            // The read says it changed when done, which comes back here.
            _ = _profiles.Refresh();
            return;
        }
        if (_profiles.IsEdited || _profiles.Ready != ProfileReadiness.Ready) return;

        _applying = true;
        try
        {
            var missing = await _profiles.ApplyAutomatically(target);
            AppLog.Write($"profiles: switched to {target.Name} on its own");
            if (missing.Count > 0)
                AppLog.Write($"profiles: {target.Name}'s saved presets are no longer on the headset: {string.Join(", ", missing)}");
            if (_waiting == id) _waiting = null;
        }
        catch (HeadsetUnavailableException ex)
        {
            AppLog.Write($"profiles: {target.Name} waits for the headset: {ex.Message}");
        }
        finally
        {
            _applying = false;
            Changed?.Invoke();
        }

        // Another app may have started or closed while that one went on.
        if (_waiting is { } next && next != id) await TryApply();
    }

    private async void OnProfilesChanged() => await TryApply();

    private async void OnStatus(Neap.Core.Connection.HeadsetStatus status) => await TryApply(read: true);

    private void OnChosen()
    {
        _rule.Chose();
        if (_waiting is null) return;
        _waiting = null;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _timer.Dispose();
        _profiles.Changed -= OnProfilesChanged;
        _profiles.Chosen -= OnChosen;
        _headset.StatusChanged -= OnStatus;
    }
}
