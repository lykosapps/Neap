using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Neap.Core.Tests.Resources;

/// <summary>
/// Checks that every key the app asks for is in its resource file, and every
/// entry in it is asked for.
/// </summary>
/// <remarks>
/// A missing key fails silently: x:Uid leaves the text blank, and a lookup
/// from code comes back empty. The app is a WinUI project and cannot be
/// loaded here, so these tests read its sources instead.
/// </remarks>
public sealed partial class AppStringsTests
{
    private static readonly string App = Path.Combine(RepoRoot(), "src", "Neap.App");

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

    /// <summary>Every resource key used in the app's C# sources.</summary>
    /// <remarks>
    /// Keys are written out in full wherever they are used, never built, so
    /// any literal shaped like one is one.
    /// </remarks>
    private static IEnumerable<string> CodeKeys() =>
        Sources("*.cs").SelectMany(text => KeyLiteral().Matches(text).Select(m => m.Groups[1].Value))
            .Distinct();

    private static IEnumerable<string> Uids() =>
        Sources("*.xaml").SelectMany(text => Uid().Matches(text).Select(m => m.Groups[1].Value))
            .Distinct();

    private static IEnumerable<string> Sources(string pattern) =>
        Directory.EnumerateFiles(App, pattern, SearchOption.AllDirectories)
            .Where(f => !Under(f, "obj") && !Under(f, "bin"))
            .Select(File.ReadAllText);

    private static bool Under(string file, string folder) =>
        file.Contains($"{Path.DirectorySeparatorChar}{folder}{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal);

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
