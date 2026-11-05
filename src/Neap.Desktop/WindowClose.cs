using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Neap.Desktop;

/// <summary>Switches off, or on again, the close button on a window's title bar where the app draws it.</summary>
/// <remarks>
/// For a window that refuses to close for a while: a close button that looks
/// as if it would work, and does nothing, contradicts the screen. Where the
/// desktop draws the title bar, as on Linux, its button stays as it is and the
/// window still refuses.
/// </remarks>
internal static class WindowClose
{
    /// <summary>The name the drawn title bar gives its close button.</summary>
    private const string ButtonName = "PART_CloseButton";

    /// <summary>Switches the window's drawn close button off, or on again.</summary>
    public static void Allow(Window window, bool allowed)
    {
        // The drawn title bar sits beside the window's content, under the same root.
        Visual root = window.GetVisualAncestors().LastOrDefault() ?? window;
        var buttons = root.GetVisualDescendants().OfType<Button>().Where(button => button.Name == ButtonName).ToList();
        foreach (var button in buttons) button.IsEnabled = allowed;
        if (buttons.Count == 0) AppLog.Write($"window: no drawn close button to switch {(allowed ? "on" : "off")}");
    }
}
