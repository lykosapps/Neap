using System.Runtime.InteropServices;

namespace Neap.Services;

/// <summary>Hands the memory a hidden app is not using back to Windows.</summary>
/// <remarks>
/// <para>
/// With the window closed the app lives in the notification area, and the
/// number Task Manager shows beside it is what it has resident. A pass of the
/// pages it drew while the window was open leaves them resident long after
/// nobody is looking; asking Windows to take back what is not in use leaves
/// the app at a fraction of that, and Windows brings back what is needed
/// without being asked.
/// </para>
/// <para>
/// Once is not enough: the headset reader keeps working while hidden, and the
/// pages it touches come straight back, so it is asked again every minute
/// for as long as the window stays closed.
/// </para>
/// </remarks>
public static class WorkingSet
{
    /// <summary>How often a hidden app gives memory back.</summary>
    public static readonly TimeSpan HiddenEvery = TimeSpan.FromSeconds(60);

    private static bool _said;

    /// <summary>Asks Windows to take back the memory not in use. Does nothing on another system.</summary>
    public static void Trim()
    {
        if (!OperatingSystem.IsWindows()) return;
        if (SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1) || _said) return;

        // Said once: a refusal every minute would fill the log with the same line.
        _said = true;
        AppLog.Write($"memory: Windows would not take back what a hidden app is not using (error {Marshal.GetLastPInvokeError()})");
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint minimum, nint maximum);
}
