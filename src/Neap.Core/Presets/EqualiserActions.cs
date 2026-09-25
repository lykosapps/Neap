namespace Neap.Core.Presets;

/// <summary>Which of the equaliser's actions can be taken for the curve it is on.</summary>
/// <remarks>
/// The buttons stay on screen whether or not they can be used, and are
/// disabled when they cannot, so none moves under the pointer when a band
/// is first dragged.
/// </remarks>
/// <param name="Discard">The curve can go back to its preset.</param>
/// <param name="Save">The curve can be saved as a new preset.</param>
/// <param name="Overwrite">The curve can be written over the preset it came from.</param>
/// <param name="Duplicate">Saving as new copies the preset as it is, and is named for that.</param>
public readonly record struct EqualiserActions(bool Discard, bool Save, bool Overwrite, bool Duplicate)
{
    /// <param name="edited">The curve has moved since its preset was chosen.</param>
    /// <param name="baseline">The preset it came from, or null when that is not known.</param>
    public static EqualiserActions For(bool edited, Preset? baseline) => new(
        Discard: edited,
        // Always: a changed curve saves as a new preset, and an unchanged
        // one copies its preset.
        Save: true,
        // Only a preset of yours has somewhere to write the curve back to.
        Overwrite: edited && baseline is { Custom: true },
        Duplicate: !edited && baseline is not null);
}
