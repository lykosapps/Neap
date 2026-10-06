namespace Neap.Core.Updates;

/// <summary>Whether the version an update just put in place opened and stayed open.</summary>
public enum RestartOutcome { Started, DidNotStart }

/// <summary>
/// Decides, after an update, whether the new version started. The old version
/// stays on hand for a short while to find out, since it is the only thing
/// left to say so if the new one doesn't.
/// </summary>
/// <remarks>
/// A new version that is still running when the window ends has started,
/// however long it takes to open. One that exits inside the window, or could
/// not be launched at all, has not: it was installed and won't run, which is a
/// worse place to be than not having updated, and nothing else would say so.
/// </remarks>
public static class RestartWatch
{
    /// <summary>How long the old version watches the new one.</summary>
    public static TimeSpan Window { get; } = TimeSpan.FromSeconds(10);

    /// <param name="exitedAfter">
    /// How long after launch the new version exited, zero if it could not be
    /// launched, or null if it was still running when the window ended.
    /// </param>
    public static RestartOutcome Of(TimeSpan? exitedAfter) =>
        exitedAfter is { } after && after < Window ? RestartOutcome.DidNotStart : RestartOutcome.Started;
}
