using System.Globalization;
using System.Xml.Linq;

namespace Neap.Core.Tests.Resources;

/// <summary>
/// Checks that Neap's own colours keep their contrast in the dark and light
/// themes: text at least 4.5:1 against what it sits on, and the mix's two
/// fills at least 3:1 against the panel.
/// </summary>
/// <remarks>
/// <para>
/// A colour that fails is easy to miss on the theme it was not chosen in,
/// and nothing on screen says so. These tests read the theme dictionaries
/// from the app's XAML and work the ratios out, so a change of colour that
/// breaks a pairing fails the build.
/// </para>
/// <para>
/// Each pairing is checked against every stop of a gradient behind it, and
/// the tile's state line at the opacity its style gives it. High contrast
/// uses the system's own colours and is not checked here.
/// </para>
/// </remarks>
public sealed class ThemeContrastTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string[] Files =
        ["App.xaml", "Controls/Tiles.xaml", "Controls/GameChatDial.xaml", "Themes/Controls.xaml"];

    private static readonly Dictionary<string, Dictionary<string, Rgba[]>> Themes = LoadThemes();

    /// <summary>The page behind everything, which a translucent colour is laid over.</summary>
    private const string Page = "NavigationViewContentBackground";

    private static readonly double StateLine = TileStateOpacity();

    /// <summary>What is drawn on what, whether it is a tile's state line, and the least contrast it may have.</summary>
    private static readonly (string Text, string Behind, bool State, double AtLeast)[] Pairs =
    [
        ("NeapGameTextBrush", "NeapPanelBrush", false, 4.5),
        ("NeapGameTextBrush", Page, false, 4.5),
        ("NeapChatTextBrush", "NeapPanelBrush", false, 4.5),
        ("NeapChatTextBrush", Page, false, 4.5),
        ("NeapTileTextBrush", "NeapTileBrush", true, 4.5),
        ("NeapTileTextBrush", "NeapTileHoverBrush", true, 4.5),
        ("NeapTileOnTextBrush", "NeapTileOnBrush", true, 4.5),
        ("NeapTileOnTextBrush", "NeapTileOnHoverBrush", true, 4.5),
        ("NeapTileDisabledTextBrush", "NeapTileDisabledBrush", true, 4.5),
        ("NeapTileOnDisabledTextBrush", "NeapTileOnDisabledBrush", true, 4.5),
        ("NeapGameBrush", "NeapPanelBrush", false, 3),
        ("NeapChatBrush", "NeapPanelBrush", false, 3),
    ];

    private static readonly string[] ThemeNames = ["Dark", "Light"];

    public static TheoryData<string, string, string, bool, double> Pairings()
    {
        var data = new TheoryData<string, string, string, bool, double>();
        foreach (string theme in ThemeNames)
            foreach (var p in Pairs)
                data.Add(theme, p.Text, p.Behind, p.State, p.AtLeast);
        return data;
    }

    [Theory]
    [MemberData(nameof(Pairings))]
    public void ColoursKeepTheirContrast(string theme, string text, string behind, bool state, double atLeast)
    {
        var colours = Themes[theme];
        Rgba page = colours[Page].Single();
        foreach (var back in colours[behind].Select(c => c.Over(page)))
            foreach (var fore in colours[text])
            {
                // The tile's name is drawn at full strength and its state
                // line fainter, so the state line is the one to hold.
                var drawn = (state ? fore.Faded(StateLine) : fore).Over(back);
                double ratio = Rgba.Contrast(drawn, back);
                Assert.True(ratio >= atLeast,
                    $"{theme}: {text} on {behind} ({back}) is {ratio:0.00}:1, under {atLeast}:1");
            }
    }

    [Fact]
    public void EveryColourNamedHereIsInBothThemes()
    {
        var named = Pairs.SelectMany(p => new[] { p.Text, p.Behind }).Distinct().ToList();
        foreach (string theme in ThemeNames)
            Assert.All(named, n => Assert.True(Themes[theme].ContainsKey(n), $"{n} is not in the {theme} theme"));
    }

    /// <summary>Every theme dictionary's colours, by theme and key: one for a solid brush, one per stop for a gradient.</summary>
    private static Dictionary<string, Dictionary<string, Rgba[]>> LoadThemes()
    {
        var themes = new Dictionary<string, Dictionary<string, Rgba[]>>
        {
            ["Dark"] = new(StringComparer.Ordinal),
            ["Light"] = new(StringComparer.Ordinal),
        };
        foreach (string file in Files)
        {
            var dictionaries = XDocument.Load(Path.Combine(AppSource.Folder, file))
                .Descendants(Xaml + "ResourceDictionary.ThemeDictionaries")
                .Elements(Xaml + "ResourceDictionary");
            foreach (var dictionary in dictionaries)
            {
                string? theme = (string?)dictionary.Attribute(X + "Key") switch
                {
                    "Default" or "Dark" => "Dark",
                    "Light" => "Light",
                    _ => null,
                };
                if (theme is null) continue;
                foreach (var brush in dictionary.Elements())
                {
                    var stops = brush.Name.LocalName == "SolidColorBrush"
                        ? new[] { brush }
                        : brush.Descendants(Xaml + "GradientStop").ToArray();
                    var parsed = stops.Select(s => (string?)s.Attribute("Color"))
                        .Where(c => c is not null && c.StartsWith('#'))
                        .Select(c => Rgba.Parse(c!))
                        .ToArray();
                    if (parsed.Length > 0) themes[theme][(string)brush.Attribute(X + "Key")!] = parsed;
                }
            }
        }
        return themes;
    }

    private static double TileStateOpacity()
    {
        var style = XDocument.Load(Path.Combine(AppSource.Folder, "Controls/Tiles.xaml"))
            .Descendants(Xaml + "Style")
            .Single(s => (string?)s.Attribute(X + "Key") == "NeapTileStateStyle");
        var opacity = style.Elements(Xaml + "Setter")
            .SingleOrDefault(s => (string?)s.Attribute("Property") == "Opacity");
        return opacity is null ? 1 : double.Parse((string)opacity.Attribute("Value")!, CultureInfo.InvariantCulture);
    }

    /// <summary>A colour as XAML writes it, with the sums contrast needs.</summary>
    private readonly record struct Rgba(byte A, byte R, byte G, byte B)
    {
        public static Rgba Parse(string hex)
        {
            string digits = hex.TrimStart('#');
            if (digits.Length == 6) digits = "FF" + digits;
            uint value = uint.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Rgba((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
        }

        public Rgba Faded(double opacity) => this with { A = (byte)Math.Round(A * opacity) };

        /// <summary>This colour laid over an opaque one.</summary>
        public Rgba Over(Rgba under)
        {
            double a = A / 255.0;
            byte Mix(byte top, byte bottom) => (byte)Math.Round(top * a + bottom * (1 - a));
            return new Rgba(255, Mix(R, under.R), Mix(G, under.G), Mix(B, under.B));
        }

        /// <summary>The WCAG contrast ratio between two opaque colours.</summary>
        public static double Contrast(Rgba one, Rgba two)
        {
            double l1 = one.Luminance(), l2 = two.Luminance();
            return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
        }

        private double Luminance()
        {
            static double Linear(byte channel)
            {
                double c = channel / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Linear(R) + 0.7152 * Linear(G) + 0.0722 * Linear(B);
        }

        public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
    }
}
