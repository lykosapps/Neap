namespace StealthPro.App.Services;

/// <summary>
/// A short record of what the app did and why: launches, connection changes,
/// and mix changes with what caused them.
/// </summary>
/// <remarks>
/// <para>
/// Some faults cannot be explained without it. A launch at sign-in that never
/// happened looks the same as one that died, and a mix that moves with nobody
/// touching the wheel or the slider leaves nothing to say what moved it. A
/// line per event separates those.
/// </para>
/// <para>
/// Small on purpose: plain lines, the most recent thousand, beside the app's
/// settings. Nothing about the person, only the app's own states.
/// </para>
/// </remarks>
public static class AppLog
{
    private const int KeepLines = 1000;
    private static readonly object Gate = new();

    private static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StealthProIIControl");

    private static string File => Path.Combine(Folder, "app.log");

    /// <summary>Length of the time stamp that starts every line, "yyyy-MM-dd HH:mm:ss.f".</summary>
    private const int StampLength = 21;

    /// <summary>Record a line.</summary>
    /// <param name="what">The line, without its time stamp.</param>
    /// <param name="at">
    /// When it happened, if earlier than now: a mix change is written when it
    /// finishes but stamped when it began. The line goes where that time
    /// belongs, so the record still reads in order.
    /// </param>
    public static void Write(string what, DateTime? at = null)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                string line = $"{(at ?? DateTime.Now):yyyy-MM-dd HH:mm:ss.f}  {what}";
                var lines = System.IO.File.Exists(File)
                    ? System.IO.File.ReadAllLines(File).ToList()
                    : new List<string>();
                int i = lines.Count;
                while (i > 0 && string.CompareOrdinal(lines[i - 1], 0, line, 0, StampLength) > 0) i--;
                lines.Insert(i, line);
                if (lines.Count > KeepLines) lines.RemoveRange(0, lines.Count - KeepLines);
                System.IO.File.WriteAllLines(File, lines);
            }
        }
        catch { }
    }
}
