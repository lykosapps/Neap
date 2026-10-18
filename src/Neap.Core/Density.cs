namespace Neap.Core;

/// <summary>How roomy the app draws: its usual compact size, or one step up for a small screen held in the hands.</summary>
public enum DensityMode
{
    /// <summary>Body 13, rows 40, menu items 36.</summary>
    Compact,

    /// <summary>Body 14, rows 48, menu items 44, and controls at least 40 high.</summary>
    Touch,
}

/// <summary>Decides how roomy the app draws, from the size of its window.</summary>
/// <remarks>
/// <para>
/// The Steam Deck shows the app at 1280 by 800, and a small laptop or a
/// docked handheld is about the same, so a window that fits within that is
/// treated as one a finger may be using. The window's client area loses the
/// title bar from its height, so the height limit is 820 rather than 800,
/// which keeps a 1280 by 800 screen with a title bar of its own in touch size.
/// </para>
/// <para>
/// The limits are on the window, not the screen, so a large screen with a
/// small window also gets the roomier size, and growing the window back
/// returns to compact without a restart.
/// </para>
/// </remarks>
public static class Density
{
    /// <summary>The widest window, in device-independent pixels, that is drawn at touch size.</summary>
    public const double TouchMaxWidth = 1280;

    /// <summary>The tallest window, in device-independent pixels, that is drawn at touch size.</summary>
    public const double TouchMaxHeight = 820;

    /// <summary>Gets the size to draw at for a window's client area.</summary>
    public static DensityMode For(double width, double height) =>
        width <= TouchMaxWidth && height <= TouchMaxHeight ? DensityMode.Touch : DensityMode.Compact;
}
