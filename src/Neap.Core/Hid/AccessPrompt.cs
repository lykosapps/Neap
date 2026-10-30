namespace Neap.Core.Hid;

/// <summary>How asking the system to let this user use the headset went.</summary>
public enum AccessResult
{
    /// <summary>The rule is in place and the system has been told to use it.</summary>
    Granted,

    /// <summary>The person did not give their password.</summary>
    Cancelled,

    /// <summary>It could not be done; what the system said says why.</summary>
    Failed,
}

/// <summary>
/// What the desktop's password prompt, polkit's pkexec, is asked to run to
/// install <see cref="UdevRule"/>, and how to read its answer.
/// </summary>
public static class AccessPrompt
{
    /// <summary>The exit code pkexec gives when the person closed the prompt.</summary>
    private const int Dismissed = 126;

    /// <summary>The exit code pkexec gives when it ran nothing, and the one a shell gives for a command it cannot find.</summary>
    private const int NotRun = 127;

    /// <summary>The arguments pkexec is given: a shell, and the commands for it to run.</summary>
    /// <remarks>
    /// The shell is named by its full path: pkexec looks a bare name up on the
    /// person's own PATH, where a look-alike in a folder of theirs would run
    /// as root once the password is typed.
    /// </remarks>
    public static IReadOnlyList<string> Arguments { get; } = ["/bin/sh", "-c", UdevRule.RootCommand];

    /// <summary>How it went, from pkexec's exit code and what it wrote to its error output.</summary>
    /// <remarks>
    /// pkexec gives 127 for a password refused and for anything else that
    /// stopped it, such as a desktop with no prompt to show. Only the refusal
    /// is the person's choice; anything else is a failure, so the person is
    /// given the commands to run themselves.
    /// </remarks>
    public static AccessResult Outcome(int exitCode, string said) => exitCode switch
    {
        0 => AccessResult.Granted,
        Dismissed => AccessResult.Cancelled,
        NotRun when said.Contains("Not authorized", StringComparison.Ordinal) => AccessResult.Cancelled,
        _ => AccessResult.Failed,
    };
}
