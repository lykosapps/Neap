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
/// settings. It holds the app's own states, the names the person gives
/// profiles, presets and the curve files they load, the names of the
/// programs playing to the headset where the mix needs them, and the paths
/// that error messages carry, which can include the profile folder. It stays
/// on the PC, and a recording that includes it blanks the account, the
/// profile folder and the PC's name
/// (<see cref="Neap.Core.Diagnostics.Redaction"/>). How the file is kept,
/// including when it cannot be written, is <see cref="LogFile"/>'s.
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
