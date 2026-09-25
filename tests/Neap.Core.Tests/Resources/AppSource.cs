namespace Neap.Core.Tests.Resources;

/// <summary>Where the app's sources are, for the tests that read them.</summary>
/// <remarks>
/// The app is a WinUI project and cannot be loaded here, so tests that check
/// it read its files instead.
/// </remarks>
internal static class AppSource
{
    public static string Folder { get; } = Path.Combine(RepoRoot(), "src", "Neap.App");

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "global.json"))) return dir.FullName;
        throw new InvalidOperationException("No global.json above the test's folder.");
    }
}
