using System.Runtime.InteropServices;

namespace Neap.Services;

/// <summary>Whether something is full-screen: a game, a video or a presentation.</summary>
/// <remarks>
/// Windows' own answer, the one it uses to hold notifications back during a
/// game. It covers borderless full-screen games as well as exclusive ones.
/// </remarks>
public static class FullScreen
{
    private const int Busy = 2, Direct3D = 3, Presenting = 4;

    /// <summary>Whether a full-screen app is running now; false when Windows can't say.</summary>
    public static bool Now() =>
        SHQueryUserNotificationState(out int state) == 0 && state is Busy or Direct3D or Presenting;

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
