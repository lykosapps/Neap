using Neap.Core;

namespace Neap.Services;

/// <summary>
/// A short record of what the app did and why: launches, connection changes,
/// and mix changes with what caused them.
/// </summary>
/// <remarks>
/// <para>
/// Some faults cannot be explained without it. A launch at sign-in that never
/// happened looks the same as one that died, and a mix that moves with nobody
/// touching the wheel or the dial leaves nothing to say what moved it. A
/// line per event separates those.
/// </para>
/// <para>
/// Small on purpose: plain lines, the most recent thousand, beside the app's
/// settings. Nothing about the person, only the app's own states. How the
/// file is kept, including when it cannot be written, is
/// <see cref="LogFile"/>'s.
/// </para>
/// </remarks>
public static class AppLog
{
    private static readonly object Gate = new();
    private static LogFile? _file;

    /// <remarks>
    /// Made at the first line, not before: a pretend run moves the app's
    /// folder first, and its log belongs in the folder it moved to.
    /// </remarks>
    private static LogFile File
    {
        get
        {
            lock (Gate) return _file ??= new LogFile(Path.Combine(AppFolder.Path, "app.log"));
        }
    }

    /// <summary>Record a line.</summary>
    /// <param name="what">The line, without its time stamp.</param>
    /// <param name="at">
    /// When it happened, if earlier than now: a mix change is written when it
    /// finishes but stamped when it began. The line goes where that time
    /// belongs, so the record still reads in order.
    /// </param>
    public static void Write(string what, DateTime? at = null) => File.Write(what, at ?? DateTime.Now);

    /// <summary>Every line kept, oldest first.</summary>
    /// <exception cref="IOException">The log could not be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The log could not be read.</exception>
    public static IReadOnlyList<string> Read() => File.Read();
}
