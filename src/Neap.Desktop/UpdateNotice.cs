using System.ComponentModel;
using System.Diagnostics;
using Neap.Core;

namespace Neap.Desktop;

/// <summary>
/// Says a newer version is out to someone whose window is closed, which is
/// most of the time: the app lives in the notification area.
/// </summary>
/// <remarks>
/// <para>
/// The banner in the window says it to someone who opens the window, and
/// that may be never. A notification reaches them where they are, and tells
/// them where updating is.
/// </para>
/// <para>
/// Windows shows one from the notification area and opens Settings when it is
/// selected. A Linux desktop shows one through its own notification service;
/// there is nothing to select there, so the words say where to go.
/// </para>
/// </remarks>
internal static class UpdateNotice
{
    /// <summary>Shows the notice.</summary>
    /// <param name="title">What is available.</param>
    /// <param name="message">Where to update.</param>
    /// <param name="selected">Called, on a thread of the system's, when the notice is selected, where it can be.</param>
    public static void Show(string title, string message, Action selected)
    {
        try
        {
#if WINDOWS
            if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
            {
                WindowsBalloon.Show(title, message, selected);
                return;
            }
#endif
            if (OperatingSystem.IsLinux()) ShowOnLinux(title, message);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            // Notifications can be off or the service missing; the banner still says it.
            AppLog.Write($"could not show the update notification: {ex.Message}");
        }
    }

    /// <summary>Sends the notice to the desktop's notification service over the session bus, as the desktop's own programs do.</summary>
    private static void ShowOnLinux(string title, string message)
    {
        var start = DesktopPrograms.Command("dbus-send",
            "--session", "--dest=org.freedesktop.Notifications", "--type=method_call",
            "/org/freedesktop/Notifications", "org.freedesktop.Notifications.Notify",
            "string:" + AppInfo.Name, "uint32:0", "string:", "string:" + title, "string:" + message,
            "array:string:", "dict:string:variant:", "int32:15000");
        Process.Start(start)?.Dispose();
    }
}
