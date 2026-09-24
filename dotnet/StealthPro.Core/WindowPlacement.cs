namespace StealthPro.Core;

/// <summary>A rectangle on the desktop, in physical pixels.</summary>
public readonly record struct Box(int X, int Y, int Width, int Height);

/// <summary>Decides where the app's window opens and how big it is.</summary>
/// <remarks>
/// <para>
/// A window taller than the screen hides its own bottom edge, and one placed
/// on a monitor that has since gone opens somewhere nobody can see. So the
/// window goes back where it was left only while that place is still on a
/// screen, and never larger than the screen it is on.
/// </para>
/// <para>
/// A window left across two monitors belongs to the one holding its centre.
/// </para>
/// </remarks>
public static class WindowPlacement
{
    /// <param name="saved">Where the window was left, or null the first time.</param>
    /// <param name="savedArea">The work area of the screen holding the centre of <paramref name="saved"/>, or null when no screen does.</param>
    /// <param name="primaryArea">The work area of the main screen.</param>
    /// <param name="width">The width to open at when there is nowhere saved.</param>
    /// <param name="height">The height to open at when there is nowhere saved.</param>
    public static Box Fit(Box? saved, Box? savedArea, Box primaryArea, int width, int height)
    {
        if (saved is { } box && savedArea is { } area) return Within(box, area);

        int w = Math.Min(width, primaryArea.Width), h = Math.Min(height, primaryArea.Height);
        return new(primaryArea.X + (primaryArea.Width - w) / 2,
            primaryArea.Y + (primaryArea.Height - h) / 2, w, h);
    }

    /// <summary>The point a saved window belongs to a screen by.</summary>
    public static (int X, int Y) Centre(Box box) => (box.X + box.Width / 2, box.Y + box.Height / 2);

    private static Box Within(Box box, Box area)
    {
        int w = Math.Min(box.Width, area.Width), h = Math.Min(box.Height, area.Height);
        return new(Math.Clamp(box.X, area.X, area.X + area.Width - w),
            Math.Clamp(box.Y, area.Y, area.Y + area.Height - h), w, h);
    }
}
