using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Neap.Core.Presets;
using Path = Avalonia.Controls.Shapes.Path;

namespace Neap.Desktop.Controls;

/// <summary>A row in a list of presets: the name, and the preset's curve beside it.</summary>
/// <remarks>Shared by Home's preset choice and the parametric equaliser's, so a preset looks the same wherever it is chosen.</remarks>
internal static class PresetListItems
{
    private const double CurveWidth = 72, CurveHeight = 22;

    /// <summary>A row for a ten-band curve, in tenths of a decibel between a floor and a ceiling.</summary>
    /// <param name="name">What the row says.</param>
    /// <param name="tag">What choosing the row chooses.</param>
    /// <param name="levels">The curve's levels, or null for a row with no curve.</param>
    /// <param name="floor">The lowest level drawn.</param>
    /// <param name="ceiling">The highest level drawn.</param>
    public static ListBoxItem Of(string name, object tag, IReadOnlyList<int>? levels, int floor, int ceiling)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.L };
        row.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
        if (levels is not null)
        {
            var curve = new Path
            {
                Width = CurveWidth,
                Height = CurveHeight,
                Data = CurveGeometry.Of(levels, CurveWidth, CurveHeight, floor, ceiling),
                Classes = { "minicurve" },
            };
            Grid.SetColumn(curve, 1);
            row.Children.Add(curve);
        }

        var item = new ListBoxItem { Content = row, Tag = tag, Classes = { "preset" } };
        AutomationProperties.SetName(item, name);
        return item;
    }

    /// <summary>A row for parametric bands, drawn across the parametric equaliser's full range.</summary>
    /// <param name="name">What the row says.</param>
    /// <param name="tag">What choosing the row chooses.</param>
    /// <param name="bands">The ten bands, or null for a row with no curve.</param>
    public static ListBoxItem Of(string name, object tag, IReadOnlyList<ParametricBand>? bands)
    {
        const int most = (int)(ParametricBand.MostGain * 10);
        return Of(name, tag, bands is null ? null : PresetPickerLook.Curve(bands, -most, most), -most, most);
    }
}
