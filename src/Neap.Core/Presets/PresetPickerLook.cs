namespace Neap.Core.Presets;

/// <summary>Which presets a quick preset picker offers.</summary>
public enum PickerList
{
    /// <summary>The ten-band presets the headset keeps.</summary>
    Bands,

    /// <summary>The ten-band presets, then the parametric presets Neap keeps, each under its own heading.</summary>
    BandsAndParametric,
}

/// <summary>What the game equaliser's quick preset picker, on Home and in the mini panel, names and offers.</summary>
/// <remarks>
/// Every preset is one choice away, whichever mode is in use: choosing one
/// of the other kind moves to its mode first, so a ten-band preset's gains
/// never land on the parametric bands.
/// </remarks>
public static class PresetPickerLook
{
    /// <summary>The number of points the small curve beside a parametric preset is drawn through.</summary>
    public const int CurvePoints = 24;

    /// <summary>Which presets the picker lists.</summary>
    /// <param name="parametricOffered">Neap offers the parametric equaliser.</param>
    /// <param name="hasParametricBands">The headset connected has parametric bands.</param>
    public static PickerList List(bool parametricOffered, bool hasParametricBands) =>
        parametricOffered && hasParametricBands ? PickerList.BandsAndParametric : PickerList.Bands;

    /// <summary>How the parametric shape in use is named: its preset, its preset as edited, or no preset.</summary>
    /// <param name="shape">The shape in use.</param>
    /// <param name="baseline">The preset it came from, or null.</param>
    public static PresetShown Parametric(ParametricShape shape, ParametricPreset? baseline) =>
        PresetLabel.Of(baseline?.Name ?? string.Empty, ParametricPresets.Actions(shape, baseline).Edited);

    /// <summary>
    /// A parametric shape's level, in tenths of a decibel held between a floor
    /// and a ceiling, at <see cref="CurvePoints"/> frequencies spread evenly on
    /// the plot's scale, for the small curve beside a preset's name.
    /// </summary>
    /// <param name="bands">The ten bands.</param>
    /// <param name="floor">The lowest level drawn, in tenths.</param>
    /// <param name="ceiling">The highest level drawn, in tenths.</param>
    public static IReadOnlyList<int> Curve(IReadOnlyList<ParametricBand> bands, int floor, int ceiling)
    {
        var scale = new PlotScale(CurvePoints - 1, 1);
        return [.. Enumerable.Range(0, CurvePoints).Select(i =>
            Math.Clamp((int)Math.Round(BandResponse.TotalDb(bands, scale.Hertz(i)) * 10), floor, ceiling))];
    }
}
