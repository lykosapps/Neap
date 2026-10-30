using System.Diagnostics;
using Neap.Core.Hid;

namespace Neap.Services;

/// <summary>
/// Gives this user the system's permission to use the headset, on Linux, by
/// installing <see cref="UdevRule"/> through the desktop's own password prompt.
/// </summary>
/// <remarks>
/// <para>
/// Done only when the person chooses it, and the system asks them for their
/// password as it does for anything that changes it. Nothing here is run
/// unasked, and nothing is kept running: the rule is one small file.
/// </para>
/// <para>
/// A system with no prompt to ask with says so, and the person is given the
/// commands to run themselves instead; see <see cref="UdevRule.ManualCommand"/>.
/// </para>
/// </remarks>
public static class HeadsetAccess
{
    /// <summary>Where a system keeps pkexec: in its usual place, or where NixOS puts programs that need to be setuid.</summary>
    private static readonly string[] Locations = ["/usr/bin/pkexec", "/run/wrappers/bin/pkexec"];

    /// <remarks>
    /// Found in its fixed places and not by name: a program called pkexec
    /// earlier on the person's own PATH could show a prompt of its own and keep
    /// the password it was given.
    /// </remarks>
    private static string? Pkexec => Locations.FirstOrDefault(File.Exists);

    /// <summary>Whether this system has a password prompt to ask with.</summary>
    public static bool CanAsk => OperatingSystem.IsLinux() && Pkexec is not null;

    /// <summary>Installs the rule and has the system use it.</summary>
    /// <returns>How it went, and for a failure what the system said, if it said anything.</returns>
    public static async Task<(AccessResult Result, string? Why)> Grant()
    {
        if (!CanAsk || Pkexec is not { } pkexec) return (AccessResult.Failed, null);

        var start = new ProcessStartInfo(pkexec) { RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (string argument in AccessPrompt.Arguments) start.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("the prompt did not start");
            string said = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            said = said.Trim();
            AppLog.Write(process.ExitCode == 0 ? "headset access: the password prompt ended with 0"
                : $"headset access: the password prompt ended with {process.ExitCode}: {said}");
            var result = AccessPrompt.Outcome(process.ExitCode, said);
            return (result, result == AccessResult.Failed ? said : null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            AppLog.Write($"headset access: could not ask: {ex.Message}");
            return (AccessResult.Failed, ex.Message);
        }
    }
}
