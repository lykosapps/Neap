using System.Runtime.InteropServices;

namespace Neap.Desktop;

/// <summary>Whether the person has animation switched on.</summary>
/// <remarks>
/// Windows keeps it as "Show animations in Windows", and someone who has
/// turned it off, for comfort or to save the machine work, gets no movement
/// from Neap either. The toolkit cannot read that setting, and no other
/// system's is read, so elsewhere animation is on.
/// </remarks>
internal static class Motion
{
    private const uint GetClientAreaAnimation = 0x1042;

    public static bool Enabled => !OperatingSystem.IsWindows() || WindowsAnimations();

    /// <summary>Windows' setting, or on when it cannot be read.</summary>
    private static bool WindowsAnimations()
    {
        int on = 1;
        return !SystemParametersInfo(GetClientAreaAnimation, 0, ref on, 0) || on != 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref int value, uint winIni);
}
