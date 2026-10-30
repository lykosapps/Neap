using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Neap.Desktop;

/// <summary>Opens a practice run's window behind every other one, without taking the keyboard.</summary>
/// <remarks>
/// Not activating a window is not enough: Windows still shows a new window
/// above the others, so a script drawing pages would cover what somebody is
/// working on. Once it is open, the window is moved to the bottom of the
/// stack. Elsewhere there is no call for that, and the window only opens
/// without the keyboard.
/// </remarks>
internal static class WindowBehind
{
    private static readonly IntPtr Bottom = new(1);
    private const uint NoSize = 0x0001;
    private const uint NoMove = 0x0002;
    private const uint NoActivate = 0x0010;

    /// <summary>Keeps the window behind the others when the run asked for it with <c>--behind</c>.</summary>
    public static void Keep(Window window)
    {
        if (!Pretend.Behind) return;
        window.ShowActivated = false;
        if (!OperatingSystem.IsWindows()) return;
        window.Opened += (_, _) =>
        {
            if (window.TryGetPlatformHandle() is not { } handle) AppLog.Write("behind: the window has no handle, so it opened in front");
            else if (!SetWindowPos(handle.Handle, Bottom, 0, 0, 0, 0, NoSize | NoMove | NoActivate))
                AppLog.Write($"behind: could not move the window to the back: error {Marshal.GetLastWin32Error()}");
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
