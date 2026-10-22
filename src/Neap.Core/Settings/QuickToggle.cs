namespace Neap.Core.Settings;

/// <summary>What a press of a quick tile chooses, for a setting that is off or on in one of several ways.</summary>
/// <remarks>
/// As the system's quick settings do: a press turns it off, or back on the
/// way it was last on, and the tile's list chooses among the ways. Stepping
/// through them instead would hide what comes next, and take several presses
/// mid-game to reach one.
/// </remarks>
public static class QuickToggle
{
    /// <summary>The choice a press makes.</summary>
    /// <param name="current">The setting now.</param>
    /// <param name="off">The setting's off.</param>
    /// <param name="lastOn">The way it was last on.</param>
    /// <param name="offered">The choices there are now, or null when they never change.</param>
    /// <remarks>A way of being on no longer offered, such as Dolby Atmos once uninstalled, gives way to the first that is.</remarks>
    public static T Press<T>(T current, T off, T lastOn, IReadOnlyList<T>? offered = null)
        where T : struct, Enum
    {
        var same = EqualityComparer<T>.Default;
        if (!same.Equals(current, off)) return off;
        if (offered is null || offered.Contains(lastOn)) return lastOn;
        return offered.FirstOrDefault(choice => !same.Equals(choice, off), off);
    }

    /// <summary>The way it was last on, after a reading of <paramref name="seen"/>.</summary>
    public static T LastOn<T>(T seen, T off, T lastOn)
        where T : struct, Enum =>
        EqualityComparer<T>.Default.Equals(seen, off) ? lastOn : seen;
}
