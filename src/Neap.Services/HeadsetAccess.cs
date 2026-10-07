using System.Diagnostics;
using Neap.Core.Hid;

namespace Neap.Services;

/// <summary>How asking the system to let this user use the headset went.</summary>
public enum AccessResult
{
    /// <summary>The rule is in place and the system has been told to use it.</summary>
    Granted,

    /// <summary>The person did not give their password.</summary>
    Cancelled,

    /// <summary>It could not be done; <see cref="HeadsetAccess.Grant"/> says why.</summary>
    Failed,
}

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
    /// <summary>The exit codes polkit's pkexec gives when the person turned the prompt away.</summary>
    private const int Dismissed = 126, NotAuthorised = 127;

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
        start.ArgumentList.Add("sh");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(UdevRule.RootCommand);
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("the prompt did not start");
            string said = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            AppLog.Write($"headset access: the password prompt ended with {process.ExitCode}");
            return process.ExitCode switch
            {
                0 => (AccessResult.Granted, null),
                Dismissed or NotAuthorised => (AccessResult.Cancelled, null),
                _ => (AccessResult.Failed, said.Trim()),
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            AppLog.Write($"headset access: could not ask: {ex.Message}");
            return (AccessResult.Failed, ex.Message);
        }
    }
}
