using System.Text;

namespace Neap.Core;

/// <summary>Writes a file so that it holds the old text or the new, never part of either.</summary>
/// <remarks>
/// The text goes to a file beside it, which is flushed to the disk and then
/// renamed over it. A crash or a power cut part way through leaves the old
/// file as it was, where writing in place leaves it empty or cut short.
/// </remarks>
public static class WholeFile
{
    /// <summary>Replaces a file's text with <paramref name="text"/>, all at once.</summary>
    /// <param name="path">The file to write.</param>
    /// <param name="text">What it holds afterwards.</param>
    /// <exception cref="IOException">The file could not be written; it is left as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">The file could not be written; it is left as it was.</exception>
    public static void Write(string path, string text)
    {
        string temporary = path + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                file.Write(Encoding.UTF8.GetBytes(text));
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}
