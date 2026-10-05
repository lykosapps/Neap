namespace Neap.Core.Updates;

/// <summary>What the download bar under the banner's message shows.</summary>
public enum UpdateProgress
{
    /// <summary>No bar.</summary>
    None,

    /// <summary>A bar filled to the download's percentage.</summary>
    Percent,

    /// <summary>A bar with no end, for work that has no percentage.</summary>
    Unknown,
}

/// <summary>What the update banner offers at each stage and at each width, decided once.</summary>
/// <remarks>
/// While an update runs the banner has nothing to ask: Update and restart
/// stays on screen, off, and What's new and the ways to put the banner away
/// are not offered, since neither would change anything. Below
/// <see cref="CompactBelow"/> the banner keeps to one row by moving What's new
/// and the ways to put it away into one menu.
/// </remarks>
/// <param name="ActEnabled">Whether the main button can be pressed.</param>
/// <param name="Notes">Whether what's new is offered.</param>
/// <param name="PutAway">Whether the ways to put the banner away are offered.</param>
/// <param name="Progress">What the bar under the message shows.</param>
public readonly record struct UpdateBarLook(bool ActEnabled, bool Notes, bool PutAway, UpdateProgress Progress)
{
    /// <summary>The width, in effective pixels, below which the banner keeps to one row by using a menu.</summary>
    public const double CompactBelow = 720;

    public static UpdateBarLook Of(UpdateStage stage)
    {
        var look = UpdateLook.Of(stage);
        var progress = stage switch
        {
            UpdateStage.Downloading => UpdateProgress.Percent,
            UpdateStage.Installing => UpdateProgress.Unknown,
            _ => UpdateProgress.None,
        };
        return new(ActEnabled: !look.Busy, Notes: look.Notes && !look.Busy, PutAway: !look.Busy, progress);
    }

    /// <summary>Whether a banner this wide uses the menu in place of separate buttons.</summary>
    public static bool Compact(double width) => width < CompactBelow;
}
