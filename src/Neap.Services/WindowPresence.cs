namespace Neap.Services;

/// <summary>Whether Neap's window is the one in front.</summary>
/// <remarks>
/// The readings a page polls Windows for, and the microphone meter, run only
/// while it is. Behind a game they would go on asking and listening for
/// nobody; they pick up again the moment the window is back in front.
/// </remarks>
public static class WindowPresence
{
    /// <summary>Gets whether Neap's window is the one in front.</summary>
    public static bool InFront { get; private set; } = true;

    /// <summary>The window came to the front or went behind. Raised on the UI thread.</summary>
    public static event Action? Changed;

    public static void Set(bool inFront)
    {
        if (inFront == InFront) return;
        InFront = inFront;
        Changed?.Invoke();
    }
}
