namespace Neap.Core.Presets;

/// <summary>Which presets a quick preset picker offers.</summary>
public enum PickerList
{
    /// <summary>The ten-band presets the headset keeps.</summary>
    Bands,

    /// <summary>The parametric presets Neap keeps.</summary>
    Parametric,

    /// <summary>None: the Parametric mode is in use and no parametric preset is saved yet.</summary>
    NoParametric,
}

/// <summary>What the game equaliser's quick preset picker, on Home and in the mini panel, names and offers.</summary>
/// <remarks>
/// The picker follows the mode in use. Choosing a ten-band preset in the
/// Parametric mode would put its gains on the parametric bands, leaving a
/// shape that matches neither and is saved nowhere, so the Parametric mode
/// offers its own presets instead.
/// </remarks>
public static class PresetPickerLook
{
    /// <summary>The number of points the small curve beside a parametric preset is drawn through.</summary>
    public const int CurvePoints = 24;

    /// <summary>Which presets the picker lists.</summary>
    /// <param name="parametricActive">The Parametric mode is the one in use.</param>
    /// <param name="hasParametricBands">The headset connected has parametric bands.</param>
    /// <param name="parametricPresets">How many parametric presets are saved.</param>
    public static PickerList List(bool parametricActive, bool hasParametricBands, int parametricPresets) =>
        !(parametricActive && hasParametricBands) ? PickerList.Bands
        : parametricPresets == 0 ? PickerList.NoParametric
        : PickerList.Parametric;

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
