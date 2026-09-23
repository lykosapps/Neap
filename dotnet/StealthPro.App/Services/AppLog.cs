namespace StealthPro.App.Services;

/// <summary>
/// A short record of what the app did and why: launches, connection changes,
/// and mix changes with what caused them.
///
/// <b>Kept because two bugs could not be explained without it.</b> The app
/// started with Windows for twelve logins in a row without once running, and
/// a launch that never happened looked the same as one that died. And the mix
/// moved to 76% game while nobody touched the wheel or the slider, and there
/// was nothing to say what had moved it. A line per event separates those.
///
/// Small on purpose: plain lines, the most recent thousand, beside the app's
/// settings. Nothing about the person — only the app's own states.
/// </summary>
public static class AppLog
{
    private const int KeepLines = 1000;
    private static readonly object Gate = new();

    private static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StealthProIIControl");

    private static string File => Path.Combine(Folder, "app.log");

    /// <summary>The time stamp that starts every line, "yyyy-MM-dd HH:mm:ss.f".</summary>
    private const int StampLength = 21;

    /// <summary>
    /// Record a line. <paramref name="at"/> stamps it earlier than now — a
    /// mix change is written when it finishes but stamped when it began — and
    /// it goes where that time belongs, so the record still reads in order.
    /// </summary>
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
