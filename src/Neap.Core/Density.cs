namespace Neap.Core;

/// <summary>How roomy the app draws: its usual compact size, or one step up for a small screen held in the hands.</summary>
public enum DensityMode
{
    /// <summary>Body 13, rows 40, menu items 36.</summary>
    Compact,

    /// <summary>Body 14, rows 48, menu items 44, and controls at least 40 high.</summary>
    Touch,
}

/// <summary>Decides how roomy the app draws, from the size of the screen it is on.</summary>
/// <remarks>
/// <para>
/// The Steam Deck's screen is 1280 by 800, and a small laptop or a docked
/// handheld is about the same, so a screen that fits within that is treated
/// as one a finger may be using. The limits are on the screen's work area in
/// device-independent pixels, so a large screen at a high scaling counts as
/// the size it draws at.
/// </para>
/// <para>
/// The screen, not the window: a window made small on a large screen is still
/// used with a mouse, and drew its switches and sliders at a finger's size.
/// </para>
/// </remarks>
public static class Density
{
    /// <summary>The widest screen, in device-independent pixels, that is drawn at touch size.</summary>
    public const double TouchMaxWidth = 1280;

    /// <summary>The tallest screen, in device-independent pixels, that is drawn at touch size.</summary>
    public const double TouchMaxHeight = 820;

    /// <summary>Gets the size to draw at on a screen with this work area.</summary>
    public static DensityMode For(double width, double height) =>
        width <= TouchMaxWidth && height <= TouchMaxHeight ? DensityMode.Touch : DensityMode.Compact;
}
