using System.Diagnostics;
using Neap.Core;

namespace Neap.Desktop;

/// <summary>Whether this desktop has a notification area for the app's icon to sit in.</summary>
/// <remarks>
/// <para>
/// Windows always does. A Linux desktop has one only if something there
/// listens for icons: KDE does, and GNOME does not unless an extension has
/// been added. An icon with nothing to show it is invisible, and an app that
/// hides itself into one cannot be got back, so where there is none, closing
/// the window closes the app, and a start at sign-in is minimised instead.
/// </para>
/// <para>
/// Asked of the session's message bus once, with the tool every desktop that
/// has a bus has; if it cannot be asked, there is taken to be none, which is
/// the safe answer.
/// </para>
/// </remarks>
internal static class TrayAvailability
{
    private const string Watcher = "org.kde.StatusNotifierWatcher";

    private static readonly Lazy<bool> Present = new(Ask);

    /// <summary>Whether an icon would be seen.</summary>
    public static bool Exists => Present.Value;

    private static bool Ask()
    {
        if (OperatingSystem.IsWindows()) return true;
        if (!OperatingSystem.IsLinux()) return false;

        try
        {
            var start = DesktopPrograms.Command("dbus-send",
                "--session", "--print-reply", "--dest=org.freedesktop.DBus", "/org/freedesktop/DBus",
                "org.freedesktop.DBus.NameHasOwner", "string:" + Watcher);
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;

            using var process = Process.Start(start);
            if (process is null || !process.WaitForExit(TimeSpan.FromSeconds(3))) return false;
            bool listening = process.StandardOutput.ReadToEnd().Contains("boolean true", StringComparison.Ordinal);
            AppLog.Write(listening
                ? "notification area: this desktop shows icons"
                : "notification area: this desktop shows no icons, so closing the window closes the app");
            return listening;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            AppLog.Write($"notification area: could not ask the desktop: {ex.Message}");
            return false;
        }
    }
}
