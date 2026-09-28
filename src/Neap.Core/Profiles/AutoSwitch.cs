namespace Neap.Core.Profiles;

/// <summary>
/// Which profile to switch to as the apps assigned to profiles start and
/// close.
/// </summary>
/// <remarks>
/// <para>
/// An app starting switches to its profile. When two are running, the one
/// that started last decides, and when it closes the other's profile comes
/// back. When the last one closes, the default profile comes back, or with
/// no default, whichever was on before the first of them started.
/// </para>
/// <para>
/// Only an app closing switches back; an app losing focus does not. A
/// profile chosen by hand while an app is running holds: closing apps then
/// changes nothing, and the next app to start switches as usual.
/// </para>
/// <para>
/// Nothing here writes anything. It says what should be on, and the caller
/// decides when the headset can take it.
/// </para>
/// </remarks>
public sealed class AutoSwitch
{
    private readonly List<string> _running = new();
    private string? _before;
    private bool _held;

    /// <summary>
    /// Takes in which programs are running now.
    /// </summary>
    /// <param name="running">Every running program, by process name.</param>
    /// <param name="profiles">The saved profiles, with the apps assigned to each.</param>
    /// <param name="defaultId">The default profile, or null if none is set.</param>
    /// <param name="activeId">The profile on now, or null if none is.</param>
    /// <returns>The profile to switch to, or null to leave things as they are.</returns>
    public string? Observe(IEnumerable<string> running, IReadOnlyList<Profile> profiles, string? defaultId, string? activeId)
    {
        var owner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
            foreach (string app in profile.AssignedApps)
                owner[app] = profile.Id;

        var assigned = running.Where(owner.ContainsKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var closed = _running.Where(app => !assigned.Contains(app)).ToList();
        // Several found at once, as when Neap starts after them, have no order
        // of their own; by name keeps it the same from one run to the next.
        var started = assigned.Where(app => !_running.Contains(app, StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (closed.Count == 0 && started.Count == 0) return null;

        foreach (string app in closed) _running.Remove(app);

        if (started.Count > 0)
        {
            if (_running.Count == 0) _before = activeId;
            _running.AddRange(started);
            _held = false;
            return owner[_running[^1]];
        }

        if (_running.Count > 0) return _held ? null : owner[_running[^1]];

        bool held = _held;
        string? before = _before;
        _held = false;
        _before = null;
        return held ? null : defaultId ?? before;
    }

    /// <summary>The person chose a profile themselves.</summary>
    /// <remarks>While an assigned app is running, their choice holds until the next app starts.</remarks>
    public void Chose()
    {
        if (_running.Count > 0) _held = true;
    }
}
