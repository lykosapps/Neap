namespace Neap.Core.Updates;

/// <summary>When Neap asks GitHub whether there is a newer version.</summary>
/// <remarks>
/// Once a day at most, whether the app was started today or has sat in the
/// notification area for a week. A last check dated after now means the
/// clock was moved back, and is treated as no check at all rather than one
/// that holds off the next for however long the clock jumped.
/// </remarks>
public static class UpdateSchedule
{
    public static TimeSpan Interval { get; } = TimeSpan.FromDays(1);

    /// <summary>Whether a check is due.</summary>
    /// <param name="last">When the last check was made, or null if never.</param>
    /// <param name="now">The time now.</param>
    public static bool Due(DateTimeOffset? last, DateTimeOffset now) =>
        last is not { } at || at > now || now - at >= Interval;
}
