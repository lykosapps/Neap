using System.Diagnostics;

namespace Neap.Core;

/// <summary>Starting the small programs a Linux desktop supplies, such as <c>dbus-send</c> and its settings tools.</summary>
public static class DesktopPrograms
{
    /// <summary>Whether a program of this name is in one of the folders on the person's PATH.</summary>
    public static bool OnPath(string program) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => File.Exists(Path.Combine(folder, program)));

    /// <summary>How to start a program with these arguments, each passed as it is, never read by a shell.</summary>
    public static ProcessStartInfo Command(string program, params string[] arguments)
    {
        var start = new ProcessStartInfo(program) { UseShellExecute = false };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }
}
