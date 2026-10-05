namespace Neap.Core.Updates;

/// <summary>Whether the banner about a newer version is on show.</summary>
/// <remarks>
/// <para>
/// On show from the moment a newer version is found until it is installed or
/// put away. It can be put away for a day, or for good: skipping a version
/// hides it for as long as that is the newest, and a newer one is announced
/// again. Skipping never takes updating away; the Updates card in Settings
/// still offers it.
/// </para>
/// <para>
/// Putting it away applies in every stage, so someone who updates from
/// Settings after putting the banner away isn't shown it again halfway
/// through.
/// </para>
/// </remarks>
public static class UpdateReminder
{
    /// <summary>How long "remind me tomorrow" keeps the banner away.</summary>
    public static TimeSpan Hold { get; } = TimeSpan.FromDays(1);

    /// <param name="stage">Where updating has got to.</param>
    /// <param name="newer">The newer version found, or null if none has been.</param>
    /// <param name="skipped">The version someone chose to skip, or null.</param>
    /// <param name="hiddenUntil">When the banner may come back, or null if it was not put away for a day.</param>
    /// <param name="now">The time now.</param>
    public static bool Shows(UpdateStage stage, Version? newer, string? skipped, DateTimeOffset? hiddenUntil, DateTimeOffset now)
    {
        if (newer is null) return false;
        if (stage is not (UpdateStage.Available or UpdateStage.CannotUpdateHere or UpdateStage.Downloading
            or UpdateStage.Installing or UpdateStage.UpdateFailed)) return false;
        if (skipped == newer.ToString(3)) return false;
        return !Hidden(hiddenUntil, now);
    }

    /// <summary>Whether a day's hold is still in force.</summary>
    /// <remarks>
    /// A hold ending more than a day from now was made before the clock was
    /// moved back, and is treated as over rather than kept for however long
    /// the clock jumped.
    /// </remarks>
    private static bool Hidden(DateTimeOffset? until, DateTimeOffset now) =>
        until is { } end && end > now && end - now <= Hold;
}
