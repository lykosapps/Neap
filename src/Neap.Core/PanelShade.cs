namespace Neap.Core;

/// <summary>Whether a Linux desktop's panel, where the notification-area icon sits, is light.</summary>
public static class PanelShade
{
    /// <summary>Whether the panel is light, from the session's XDG_CURRENT_DESKTOP and whether the desktop's theme is light.</summary>
    /// <remarks>
    /// A Linux panel does not say its shade, so the desktop's light or dark
    /// theme stands in for it, as a light theme usually comes with a light
    /// panel. GNOME's top bar, Ubuntu's included, is dark in both themes, so
    /// there the panel is taken to be dark.
    /// </remarks>
    public static bool IsLight(string? currentDesktop, bool lightTheme) =>
        lightTheme && !(currentDesktop ?? "").Split(':').Contains("GNOME", StringComparer.OrdinalIgnoreCase);
}
