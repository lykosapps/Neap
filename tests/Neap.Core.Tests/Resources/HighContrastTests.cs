using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Neap.Core.Tests.Resources;

/// <summary>Checks that high contrast gives every one of Neap's colours a system colour.</summary>
/// <remarks>
/// A colour high contrast leaves out keeps the dark or light theme's value,
/// so it is drawn in Neap's own tint among the person's system colours, and
/// nothing on screen says so.
/// </remarks>
public sealed partial class HighContrastTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void EveryThemeColourHasAHighContrastValue()
    {
        var themed = XDocument.Load(Path.Combine(AppSource.Folder, "Themes", "Tokens.axaml"))
            .Descendants().Where(e => e.Name.LocalName == "SolidColorBrush")
            .Select(e => (string?)e.Attribute(X + "Key")).OfType<string>().ToHashSet();
        var set = SetBrush().Matches(File.ReadAllText(Path.Combine(AppSource.Folder, "HighContrast.cs")))
            .Select(m => m.Groups[1].Value).ToHashSet();

        Assert.NotEmpty(themed);
        Assert.Empty(themed.Except(set).Order());
    }

    [GeneratedRegex("\"(Neap\\w+Brush)\"")]
    private static partial Regex SetBrush();
}
