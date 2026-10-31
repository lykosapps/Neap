namespace Neap.Core.Input;

/// <summary>
/// Writes the values of a freedesktop desktop entry, the file a Linux desktop
/// reads to start a program at sign-in, so a path with an awkward character in
/// it still starts the program it names.
/// </summary>
/// <remarks>
/// An <c>Exec</c> value is read twice: first as a string, in which a backslash
/// is written as two, then as a command line, in which a program with a
/// reserved character is quoted and the double quote, backtick, dollar sign
/// and backslash are each escaped by a backslash. A percent sign starts a field
/// code and is written as two. A literal backslash in a quoted argument
/// therefore takes four. See the Desktop Entry Specification, "The Exec key".
/// </remarks>
public static class DesktopEntry
{
    /// <summary>The file to start the app from at sign-in: the AppImage if the app is running from one, otherwise the program itself.</summary>
    /// <remarks>
    /// An AppImage unpacks itself to a folder that is new on every run, so the
    /// path the running program reports would be gone by the next sign-in.
    /// </remarks>
    /// <param name="appImage">The value of the <c>APPIMAGE</c> variable, if it is set.</param>
    /// <param name="appDir">The value of the <c>APPDIR</c> variable, if it is set.</param>
    /// <param name="processPath">Where the running program says it is.</param>
    public static string StartedFrom(string? appImage, string? appDir, string processPath) =>
        AppImageOf(appImage, appDir, processPath) ?? processPath;

    /// <summary>The AppImage the running program was started from, or null when it was not started from one.</summary>
    /// <remarks>
    /// The AppImage's runtime says where the file is, in <c>APPIMAGE</c>, and
    /// where it has unpacked itself, in <c>APPDIR</c>. Every program started
    /// from inside another AppImage app, such as from a terminal in an editor
    /// shipped as one, inherits that app's two, so they are Neap's only when
    /// the running program is inside <c>APPDIR</c>.
    /// </remarks>
    /// <param name="appImage">The value of the <c>APPIMAGE</c> variable, if it is set.</param>
    /// <param name="appDir">The value of the <c>APPDIR</c> variable, if it is set.</param>
    /// <param name="processPath">Where the running program says it is.</param>
    public static string? AppImageOf(string? appImage, string? appDir, string processPath) =>
        appImage is { Length: > 0 } && appDir is { Length: > 0 }
        && processPath.StartsWith(appDir.TrimEnd('/') + "/", StringComparison.Ordinal)
            ? appImage : null;

    /// <summary>The <c>Exec</c> value that runs a program with these arguments, which must need no quoting.</summary>
    /// <exception cref="FormatException">The path has a line break, which a value cannot hold.</exception>
    public static string Exec(string program, params string[] arguments) =>
        Quoted(program) + string.Concat(arguments.Select(argument => " " + argument));

    /// <summary>A plain string value, such as a <c>Path</c>.</summary>
    /// <exception cref="FormatException">The text has a line break, which a value cannot hold.</exception>
    public static string Value(string text)
    {
        if (text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal))
            throw new FormatException("a desktop entry's value cannot hold a line break");
        return text.Replace("\\", "\\\\", StringComparison.Ordinal);
    }

    private static string Quoted(string argument)
    {
        // The command line's escapes first, then the string's, which doubles every backslash they added.
        var line = new System.Text.StringBuilder();
        foreach (char c in argument)
        {
            if (c is '"' or '`' or '$' or '\\') line.Append('\\').Append(c);
            else if (c == '%') line.Append("%%");
            else line.Append(c);
        }
        return "\"" + Value(line.ToString()) + "\"";
    }
}
