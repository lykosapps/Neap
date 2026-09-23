namespace StealthPro.Core;

/// <summary>Where the app keeps its settings, log and volume journal.</summary>
public static class AppFolder
{
    private static readonly string LocalData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string Path { get; } = System.IO.Path.Combine(LocalData, "Neap");

    /// <summary>
    /// Moves the folder kept under the app's earlier name, the first time.
    /// </summary>
    /// <remarks>
    /// Call before anything reads or writes the folder. The volume journal is
    /// in it, and losing that would lose the record of other applications'
    /// volumes after a crash.
    /// </remarks>
    public static void MoveFromEarlierName()
    {
        string earlier = System.IO.Path.Combine(LocalData, "StealthProIIControl");
        try
        {
            if (Directory.Exists(earlier) && !Directory.Exists(Path)) Directory.Move(earlier, Path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
