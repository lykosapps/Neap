using Neap.Core.Presets;
using Path = Avalonia.Controls.Shapes.Path;

namespace Neap.Desktop.Controls;

/// <summary>A preset's small curve, drawn beside its name in a list of presets.</summary>
/// <remarks>Shared by Home's preset choice and the parametric equaliser's, so a preset looks the same wherever it is chosen.</remarks>
internal static class PresetListItems
{
    private const double CurveWidth = 72, CurveHeight = 22;

    /// <summary>A preset's small curve, in tenths of a decibel between a floor and a ceiling.</summary>
    public static Path Preview(IReadOnlyList<int> levels, int floor, int ceiling) => new()
    {
        Width = CurveWidth,
        Height = CurveHeight,
        Data = CurveGeometry.Of(levels, CurveWidth, CurveHeight, floor, ceiling),
        Classes = { "minicurve" },
    };

    /// <summary>Parametric bands' small curve, drawn across the parametric equaliser's full range, or null for no bands.</summary>
    public static Path? Preview(IReadOnlyList<ParametricBand>? bands)
    {
        const int most = (int)(ParametricBand.MostGain * 10);
        return bands is null ? null : Preview(PresetPickerLook.Curve(bands, -most, most), -most, most);
    }
}
