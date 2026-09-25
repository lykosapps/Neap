namespace Neap.Core.Presets;

/// <summary>How the equaliser's current curve is named.</summary>
public enum PresetShown
{
    /// <summary>A preset, as saved.</summary>
    Named,
    /// <summary>A preset whose bands have been moved since it was chosen.</summary>
    Edited,
    /// <summary>A curve that matches no preset the app knows.</summary>
    Unsaved,
}

/// <summary>Decides how the equaliser's current curve is named where a preset is picked.</summary>
/// <remarks>
/// The headset forgets which preset it is on the moment a band moves, so an
/// edited curve still takes the name of the preset it came from, marked as
/// edited, rather than showing nothing.
/// </remarks>
public static class PresetLabel
{
    /// <param name="baseline">The preset the curve came from, or empty when it is not known.</param>
    /// <param name="edited">The curve has moved since that preset was chosen.</param>
    public static PresetShown Of(string baseline, bool edited) =>
        baseline.Length == 0 ? PresetShown.Unsaved
        : edited ? PresetShown.Edited
        : PresetShown.Named;
}
