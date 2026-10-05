namespace Neap.Core.Updates;

/// <summary>Puts a new version's files in place of the running version's.</summary>
/// <remarks>
/// <para>
/// Windows lets a running program's files be renamed but not overwritten, so
/// each file being replaced is moved aside first, and the new one moved into
/// its place. Both moves stay on one drive, so each is a rename that either
/// happens or doesn't, and nothing is copied while the app is half replaced.
/// </para>
/// <para>
/// Files the new version doesn't have are left alone, as unzipping over the
/// folder by hand would leave them.
/// </para>
/// <para>
/// If any move fails, every move already made is undone, so the folder is
/// left as the running version, never as a mix of the two.
/// </para>
/// </remarks>
public static class FolderSwap
{
    /// <summary>Moves every file in <paramref name="incoming"/> into <paramref name="target"/>.</summary>
    /// <param name="target">The app's folder.</param>
    /// <param name="incoming">The new version, unzipped on the same drive.</param>
    /// <param name="replaced">Where the files being replaced are moved to, on the same drive.</param>
    /// <exception cref="IOException">A file could not be moved; the folder is as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">A file could not be moved; the folder is as it was.</exception>
    public static void Swap(string target, string incoming, string replaced)
    {
        var done = new Stack<(string From, string To)>();
        try
        {
            var files = Directory.EnumerateFiles(incoming, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal).ToList();
            foreach (string file in files)
            {
                string relative = Path.GetRelativePath(incoming, file);
                string into = Path.Combine(target, relative);
                if (File.Exists(into)) Move(into, Path.Combine(replaced, relative), done);
                Move(file, into, done);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var stuck = Undo(done);
            if (stuck.Count == 0) throw;
            throw new IOException(
                $"{ex.Message} Putting the folder back then failed for {stuck.Count} files, first {stuck[0]}.", ex);
        }
    }

    private static void Move(string from, string to, Stack<(string, string)> done)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Move(from, to);
        done.Push((from, to));
    }

    /// <summary>Undoes the moves made, newest first, and gives the files that could not be put back.</summary>
    private static List<string> Undo(Stack<(string From, string To)> done)
    {
        var stuck = new List<string>();
        while (done.TryPop(out var move))
        {
            try { File.Move(move.To, move.From); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { stuck.Add(move.From); }
        }
        return stuck;
    }
}
