using Neap.Core.Input;

namespace Neap.Services;

/// <summary>Whether the app starts when the person signs in.</summary>
internal interface IStartup
{
    /// <summary>Whether it is set to start.</summary>
    bool Enabled { get; }

    /// <summary>Keeps the setting pointing at this copy of the app, while it is on.</summary>
    void Refresh();

    /// <summary>Turns it on or off, and says whether the system now agrees.</summary>
    bool Set(bool enabled);
}

/// <summary>Starting with the system, on whichever one this is.</summary>
/// <remarks>
/// The entry passes <see cref="Flag"/>, so a sign-in can put the app in the
/// notification area rather than open a window in front of whatever somebody
/// was about to do. Every launch is written to <see cref="AppLog"/>, so a
/// sign-in says for certain whether it worked.
/// </remarks>
public static class Startup
{
    public const string Flag = "--startup";

    private static readonly IStartup? System =
        OperatingSystem.IsWindows() ? new WindowsStartup()
        : OperatingSystem.IsLinux() ? new LinuxStartup()
        : null;

    /// <summary>Whether this system can start the app when the person signs in.</summary>
    public static bool Supported => System is not null;

    /// <summary>True when this launch came from starting with the system.</summary>
    public static bool LaunchedAtLogin =>
        Environment.GetCommandLineArgs().Any(a =>
            string.Equals(a, Flag, StringComparison.OrdinalIgnoreCase));

    public static bool Enabled => System?.Enabled ?? false;

    public static void Refresh() => System?.Refresh();

    public static bool Set(bool enabled) => System?.Set(enabled) ?? false;
}

/// <summary>
/// An autostart entry in the person's own configuration folder, which every
/// desktop that follows the freedesktop standard reads at sign-in.
/// </summary>
/// <remarks>
/// It is per-user and needs no elevation. Rewritten on every launch while it
/// is on, so moving the app never leaves the desktop starting something that
/// is no longer there.
/// </remarks>
internal sealed class LinuxStartup : IStartup
{
    private static string Folder => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } config
            ? config
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
        "autostart");

    private static string EntryPath => Path.Combine(Folder, "neap.desktop");

    public bool Enabled => File.Exists(EntryPath);

    public void Refresh()
    {
        try
        {
            if (Enabled) Write();
        }
        catch (Exception ex)
        {
            // Not worth interrupting a launch for, but it would leave the
            // desktop starting a copy that may no longer be there.
            AppLog.Write($"startup: could not refresh the autostart entry: {ex.Message}");
        }
    }

    public bool Set(bool enabled)
    {
        try
        {
            if (enabled) Write();
            else if (Enabled) File.Delete(EntryPath);
            return Enabled == enabled;
        }
        catch (Exception ex)
        {
            AppLog.Write($"could not {(enabled ? "add Neap to" : "remove Neap from")} the autostart folder: {ex.Message}");
            return false;
        }
    }

    private static void Write()
    {
        string exe = Environment.ProcessPath ?? "";
        if (exe.Length == 0) throw new InvalidOperationException("no process path");

        Directory.CreateDirectory(Folder);
        File.WriteAllLines(EntryPath,
        [
            "[Desktop Entry]",
            "Type=Application",
            "Name=" + AppInfo.Name,
            "Comment=" + Strings.Format("Startup_Description", AppInfo.Name),
            "Exec=" + DesktopEntry.Exec(exe, Startup.Flag),
            "Path=" + DesktopEntry.Value(Path.GetDirectoryName(exe) ?? ""),
            "Terminal=false",
            "X-GNOME-Autostart-enabled=true",
        ]);
    }
}
