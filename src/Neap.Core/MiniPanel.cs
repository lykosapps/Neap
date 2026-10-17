namespace Neap.Core;

/// <summary>Decides where the mini panel opens and whether a click on the tray icon opens it.</summary>
/// <remarks>
/// <para>
/// The panel opens beside the notification area, as the system's own volume
/// flyout does. A desktop does not say where its icons are, but it does say
/// where its taskbar is: the work area is the screen less the bars, so the
/// side a bar takes shows as a gap between the two. The panel goes in the
/// corner next to that bar, on the side the icons are on: the right, except
/// beside a bar on the left edge. With no bar to find, as with one that hides
/// itself or a desktop that reports none, it goes bottom right.
/// </para>
/// <para>
/// Clicking the icon while the panel is open first takes focus from the
/// panel, which closes it, and then reports the click. Opening again on that
/// click would make the icon unable to close the panel, so a click just after
/// a close is that close's own.
/// </para>
/// </remarks>
public static class MiniPanel
{
    /// <summary>How soon after the panel closed for losing focus a click on the icon is taken as the one that did it.</summary>
    public static readonly TimeSpan ClickSettle = TimeSpan.FromMilliseconds(400);

    /// <param name="screen">The whole screen the notification area is on.</param>
    /// <param name="work">The screen less its taskbars.</param>
    /// <param name="width">The panel's width, in pixels.</param>
    /// <param name="height">The panel's height, in pixels.</param>
    /// <param name="margin">The gap kept to the work area's edges, in pixels.</param>
    public static Box Place(Box screen, Box work, int width, int height, int margin)
    {
        int w = Math.Min(width, work.Width - 2 * margin), h = Math.Min(height, work.Height - 2 * margin);
        bool top = work.Y > screen.Y && work.Y + work.Height == screen.Y + screen.Height;
        bool left = work.X > screen.X && work.X + work.Width == screen.X + screen.Width;
        int x = left ? work.X + margin : work.X + work.Width - w - margin;
        int y = top ? work.Y + margin : work.Y + work.Height - h - margin;
        return new(x, y, w, h);
    }

    /// <summary>Whether a click on the icon opens the panel, rather than closing it or finishing the close that began it.</summary>
    /// <param name="isOpen">Whether the panel is open now.</param>
    /// <param name="sinceClosed">How long ago it last closed.</param>
    public static bool Opens(bool isOpen, TimeSpan sinceClosed) => !isOpen && sinceClosed >= ClickSettle;
}
