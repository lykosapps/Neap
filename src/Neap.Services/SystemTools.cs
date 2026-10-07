using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Neap.Core;
using Neap.Core.Audio;
using Neap.Core.Audio.Pulse;

namespace Neap.Services;

/// <summary>Where sound is going, from whichever sound system this is.</summary>
public sealed class SystemRoutes : IRouteSource
{
    public static SystemRoutes Instance { get; } = new();

    private SystemRoutes() { }

    public RouteLook Look()
    {
        if (OperatingSystem.IsWindows())
            return new RouteLook(
                Routing.Default(output: true),
                Routing.Default(output: true, communications: true),
                Routing.Default(output: false),
                Routing.Default(output: false, communications: true),
                Routing.Cable());
        if (OperatingSystem.IsLinux())
        {
            // One default each way: the sound server has no second pair for calls.
            var output = PulseRouting.Default(output: true);
            var input = PulseRouting.Default(output: false);
            return new RouteLook(output, output, input, input, PulseRouting.Cable());
        }
        throw new PlatformNotSupportedException($"{Environment.OSVersion.Platform} has no sound system to ask");
    }

    public List<string> Belonging(string product, bool output) =>
        OperatingSystem.IsWindows() ? Routing.Belonging(product, output)
        : OperatingSystem.IsLinux() ? PulseRouting.Belonging(product, output)
        : [];

    /// <remarks>Nothing here is told when a device moves, so <see cref="AudioRoute"/> polls.</remarks>
    public IDisposable? Watch(Action moved) => null;
}

/// <summary>The few things the app asks the operating system to do that are the same on any system but done differently.</summary>
public static class SystemTools
{
    /// <summary>The process name of the program behind a process id; empty when unknown.</summary>
    public static string ProgramName(uint pid)
    {
        if (OperatingSystem.IsWindows()) return Programs.NameOf(pid);
        if (!OperatingSystem.IsLinux() || pid == 0) return "";
        try
        {
            // The kernel's short name for it, cut to fifteen characters.
            return File.ReadAllText(string.Create(CultureInfo.InvariantCulture, $"/proc/{pid}/comm")).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ""; }
    }

    /// <summary>Opens the system's own sound settings, where the default output and microphone are chosen.</summary>
    /// <remarks>
    /// The app does not change the default device itself: that is too
    /// unreliable to do on anyone's behalf. Linux has no one settings app,
    /// so this tries the ones desktops ship, in order.
    /// </remarks>
    /// <exception cref="PlatformNotSupportedException">No sound settings could be found to open.</exception>
    public static void OpenSoundSettings()
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true })?.Dispose();
            return;
        }
        string[][] known =
        [
            ["kcmshell6", "kcm_pulseaudio"], ["kcmshell5", "kcm_pulseaudio"],
            ["gnome-control-center", "sound"], ["pavucontrol"],
        ];
        foreach (var command in known)
            if (DesktopPrograms.OnPath(command[0]))
            {
                Process.Start(DesktopPrograms.Command(command[0], command[1..]))?.Dispose();
                return;
            }
        throw new PlatformNotSupportedException("no sound settings were found to open");
    }

    /// <summary>Opens a link in the default browser.</summary>
    public static Task Open(Uri link)
    {
        Process.Start(new ProcessStartInfo(link.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Shows a file in the file manager: selected where the system can, otherwise its folder.</summary>
    public static void Reveal(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"")?.Dispose();
            return;
        }
        // The freedesktop file-manager protocol can select a file, but only
        // through a session bus call; opening the folder is what every
        // desktop does the same.
        try
        {
            Process.Start(new ProcessStartInfo("xdg-open")
            {
                ArgumentList = { Path.GetDirectoryName(path) ?? "." },
                UseShellExecute = false,
            })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // A system with no way to open a folder from a program: the
            // file is where it was saved, and the screen says where.
            AppLog.Write($"could not show {path} in a file manager: {ex.Message}");
        }
    }

    /// <summary>The person's Downloads folder, wherever they have moved it.</summary>
    public static string Downloads()
    {
        if (OperatingSystem.IsWindows())
        {
            var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            if (SHGetKnownFolderPath(in id, 0, IntPtr.Zero, out string? path) == 0 && !string.IsNullOrEmpty(path)) return path;
            AppLog.Write("recording: the system did not say where Downloads is, so the usual place is used");
        }
        else if (OperatingSystem.IsLinux())
        {
            // XDG_DOWNLOAD_DIR in the user's own settings, as the desktop keeps it.
            string settings = Path.Combine(
                Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } config
                    ? config : Path.Combine(Home(), ".config"),
                "user-dirs.dirs");
            try
            {
                foreach (string line in File.ReadLines(settings))
                    if (line.StartsWith("XDG_DOWNLOAD_DIR=", StringComparison.Ordinal))
                        return line["XDG_DOWNLOAD_DIR=".Length..].Trim('"').Replace("$HOME", Home(), StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return Path.Combine(Home(), "Downloads");
    }

    private static string Home() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(in Guid id, uint flags, IntPtr token, out string? path);
}
