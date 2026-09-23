using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace StealthPro.Core.Tests.Resources;

/// <summary>
/// Every key the app asks for is in its resource file, and every entry in it
/// is asked for.
///
/// A key that is not there fails without a sound: x:Uid leaves the text
/// blank, and a lookup from code comes back empty. The app is a WinUI
/// project and cannot be loaded here, so this reads its sources instead.
/// </summary>
public sealed partial class AppStringsTests
{
    private static readonly string App = Path.Combine(RepoRoot(), "dotnet", "StealthPro.App");

    private static readonly HashSet<string> Names = XDocument
        .Load(Path.Combine(App, "Strings", "en-US", "Resources.resw"))
        .Root!.Elements("data")
        .Select(d => (string)d.Attribute("name")!)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void EveryKeyInCodeIsInTheResourceFile()
    {
        var missing = CodeKeys().Where(k => !Names.Contains(k)).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void EveryUidHasTextInTheResourceFile()
    {
        var missing = Uids().Where(u => !Names.Any(n => n.StartsWith(u + ".", StringComparison.Ordinal)))
            .ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void EveryEntryInTheResourceFileIsUsed()
    {
        var used = CodeKeys().Concat(Uids()).ToHashSet(StringComparer.Ordinal);
        var unused = Names.Where(n => !used.Contains(n.Split('.')[0])).ToList();
        Assert.Empty(unused);
    }

    /// <summary>
    /// Keys are written out in full wherever they are used, never built, so
    /// any literal shaped like one is one.
    /// </summary>
    private static IEnumerable<string> CodeKeys() =>
        Sources("*.cs").SelectMany(text => KeyLiteral().Matches(text).Select(m => m.Groups[1].Value))
            .Distinct();

    private static IEnumerable<string> Uids() =>
        Sources("*.xaml").SelectMany(text => Uid().Matches(text).Select(m => m.Groups[1].Value))
            .Distinct();

    private static IEnumerable<string> Sources(string pattern) =>
        Directory.EnumerateFiles(App, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText);

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "global.json"))) return dir.FullName;
        throw new InvalidOperationException("No global.json above the test's folder.");
    }

    [GeneratedRegex("\"([A-Z][A-Za-z]*_[A-Za-z]+)\"")]
    private static partial Regex KeyLiteral();

    [GeneratedRegex("x:Uid=\"([^\"]+)\"")]
    private static partial Regex Uid();
}
