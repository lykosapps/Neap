namespace Neap.Core.Updates;

/// <summary>Puts a new AppImage in place of the one running.</summary>
/// <remarks>
/// <para>
/// The new file is given the running one's permissions, so it can be run as
/// that one could, then renamed over it. A rename within one folder replaces
/// the file whole or not at all, so there is never a half-written AppImage
/// where the old one was, and the path stays the same, so a desktop entry that
/// starts Neap at sign-in still finds it.
/// </para>
/// <para>
/// The running copy goes on reading the file it started from, which the
/// system keeps until it closes, so it is not disturbed by the rename.
/// </para>
/// </remarks>
public static class AppImageSwap
{
    /// <summary>The name the download is written under while it arrives: hidden, beside the AppImage, so the rename stays on one drive.</summary>
    public static string IncomingFor(string appImage) =>
        Path.Combine(Path.GetDirectoryName(appImage) ?? ".", "." + Path.GetFileName(appImage) + ".update");

    /// <summary>Renames <paramref name="incoming"/> over <paramref name="appImage"/>, with its permissions.</summary>
    /// <exception cref="IOException">It could not be renamed; the AppImage is as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">It could not be renamed; the AppImage is as it was.</exception>
    public static void Swap(string appImage, string incoming)
    {
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(incoming, File.GetUnixFileMode(appImage));
        File.Move(incoming, appImage, overwrite: true);
    }
}
