using Neap.Core;

namespace Neap.Services;

/// <summary>Whether one of Neap's windows is the one in front.</summary>
/// <remarks>
/// The readings a page polls Windows for, and the microphone meter, run only
/// while it is. Behind a game they would go on asking and listening for
/// nobody; they pick up again the moment the window is back in front.
/// </remarks>
public static class WindowPresence
{
    private static readonly ActiveWindows Windows = new();

    /// <summary>Gets whether one of Neap's windows is the one in front.</summary>
    public static bool InFront => Windows.InFront;

    /// <summary>Neap came to the front or went behind. Raised on the UI thread.</summary>
    public static event Action? Changed;

    /// <summary>Records one of Neap's windows coming forward or going behind.</summary>
    public static void Set(object window, bool active)
    {
        if (!Windows.Set(window, active)) return;
        AppLog.Write($"window: Neap is {(InFront ? "in front" : "behind")}, as the {window.GetType().Name} {(active ? "came forward" : "went behind")}");
        Changed?.Invoke();
    }
}
