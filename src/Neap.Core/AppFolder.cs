namespace Neap.Core;

/// <summary>Where the app keeps its settings, log and volume journal.</summary>
public static class AppFolder
{
    private static readonly string LocalData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string Path { get; private set; } = System.IO.Path.Combine(LocalData, "Neap");

    /// <summary>
    /// Keeps everything in a folder of its own inside the app's, for a copy
    /// that must leave the real one's settings, log and journal alone.
    /// </summary>
    /// <remarks>Call before anything reads or writes the folder.</remarks>
    public static void Separate(string name) => Path = System.IO.Path.Combine(Path, name);

    /// <summary>Whether a name for <see cref="Separate"/> is a folder directly inside the app's.</summary>
    /// <remarks>
    /// <c>.</c> would be the app's own folder, <c>..</c> or a path with a
    /// separator another one, and a full path replaces the app's entirely.
    /// Windows drops a name's trailing dots and spaces, so one made only of
    /// them, such as <c>...</c>, is the app's own folder there too.
    /// </remarks>
    public static bool IsFolderName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.TrimEnd('.', ' ').Length > 0
        && name.IndexOfAny(['/', '\\', ':']) < 0
        && name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) < 0;

    /// <summary>
    /// Moves the folder kept under the app's earlier name, the first time.
    /// </summary>
    /// <remarks>
    /// Call before anything reads or writes the folder. The volume journal is
    /// in it, and losing that would lose the record of other applications'
    /// volumes after a crash.
    /// </remarks>
    /// <exception cref="IOException">The folder could not be moved.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder could not be moved.</exception>
    public static void MoveFromEarlierName()
    {
        string earlier = System.IO.Path.Combine(LocalData, "StealthProIIControl");
        if (Directory.Exists(earlier) && !Directory.Exists(Path)) Directory.Move(earlier, Path);
    }
}
