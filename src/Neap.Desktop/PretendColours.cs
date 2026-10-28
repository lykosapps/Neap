using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Neap.Desktop;

/// <summary>
/// Gives a pretend run's test script the colours of the text at a point on the
/// screen: the text's own colour and what is behind it, as drawn.
/// </summary>
/// <remarks>
/// <para>
/// The accessibility check measures contrast from a picture, and small text
/// never shows its true colour there, as its edges are blended into what is
/// behind it. When a picture reads short, the check asks this instead.
/// </para>
/// <para>
/// What is behind the text is every background between it and the first
/// solid one, laid over each other, and the text's colour is laid over that,
/// each at its own opacity and its parents'. Only plain colours are read; text
/// in or over a gradient gives no answer, and the picture's reading stands.
/// </para>
/// </remarks>
internal static class PretendColours
{
    public static JsonNode? At(int x, int y) => Dispatcher.UIThread.Invoke(() =>
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return null;
        var point = new PixelPoint(x, y);
        var text = desktop.Windows
            .SelectMany(window => window.GetVisualDescendants().OfType<TextBlock>())
            .Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && Screen(t).Contains(point))
            .OrderBy(t => t.Bounds.Width * t.Bounds.Height)
            .FirstOrDefault();
        if (text is null || Behind(text) is not { } back || text.Foreground is not ISolidColorBrush fore) return null;

        var drawn = Over(fore.Color, fore.Opacity * Opacity(text), back);
        return new JsonObject { ["fore"] = Hex(drawn), ["back"] = Hex(back) };
    });

    private static PixelRect Screen(Visual visual)
    {
        var from = visual.PointToScreen(default);
        var to = visual.PointToScreen(new Point(visual.Bounds.Width, visual.Bounds.Height));
        return new PixelRect(from, to);
    }

    /// <summary>The colour behind a piece of text, or null when a gradient or picture is part of it.</summary>
    private static Color? Behind(Visual text)
    {
        var layers = new List<(Color Colour, double Opacity)>();
        foreach (var visual in text.GetVisualAncestors())
        {
            IBrush? brush = visual switch
            {
                Border border => border.Background,
                Panel panel => panel.Background,
                ContentPresenter presenter => presenter.Background,
                TopLevel top => top.Background,
                _ => null,
            };
            if (brush is null) continue;
            if (brush is not ISolidColorBrush solid) return null;
            double opacity = solid.Opacity * Opacity(visual);
            if (solid.Color.A == 0 || opacity == 0) continue;
            layers.Add((solid.Color, opacity));
            if (solid.Color.A == 255 && opacity >= 1) break;
        }

        if (layers.Count == 0) return null;
        var colour = layers[^1].Colour;
        for (int i = layers.Count - 2; i >= 0; i--) colour = Over(layers[i].Colour, layers[i].Opacity, colour);
        return colour;
    }

    /// <summary>The opacity a visual is drawn at: its own and every parent's.</summary>
    private static double Opacity(Visual visual)
    {
        double opacity = visual.Opacity;
        foreach (var parent in visual.GetVisualAncestors()) opacity *= parent.Opacity;
        return opacity;
    }

    private static Color Over(Color top, double opacity, Color below)
    {
        double a = top.A / 255.0 * opacity;
        byte Mix(byte t, byte b) => (byte)Math.Round(t * a + b * (1 - a));
        return Color.FromRgb(Mix(top.R, below.R), Mix(top.G, below.G), Mix(top.B, below.B));
    }

    private static string Hex(Color colour) => string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");
}
