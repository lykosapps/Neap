namespace Neap.Core.Presets;

/// <summary>Which of the equaliser's actions can be taken for the curve it is on.</summary>
/// <remarks>
/// The buttons stay on screen whether or not they can be used, and are
/// disabled when they cannot, so none moves under the pointer when a band
/// is first dragged.
/// </remarks>
public readonly record struct EqualiserActions(bool Discard, bool Save, bool Overwrite)
{
    /// <param name="edited">The curve has moved since its preset was chosen.</param>
    /// <param name="baseline">The preset it came from, or null when that is not known.</param>
    public static EqualiserActions For(bool edited, Preset? baseline) => new(
        Discard: edited,
        // Saving an unchanged preset spends one of five slots on a copy of
        // it; a curve whose preset is not known can only be kept by saving.
        Save: edited || baseline is null,
        // Only a preset of yours has somewhere to write the curve back to.
        Overwrite: edited && baseline is { Custom: true });
}
