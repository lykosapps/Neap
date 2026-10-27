namespace Neap.Core.Presets;

/// <summary>The ten parametric bands as saved in a preset or a profile, equal to another with the same bands.</summary>
/// <param name="Bands">The ten bands as stored.</param>
public sealed record ParametricShape(IReadOnlyList<StoredBand> Bands)
{
    /// <summary>The shape of a set of bands.</summary>
    /// <param name="bands">The ten bands.</param>
    public static ParametricShape Of(IReadOnlyList<ParametricBand> bands) => new([.. bands.Select(StoredBand.Of)]);

    /// <summary>Whether another shape has the same bands, so a profile or a preset reads as unchanged when it is.</summary>
    /// <param name="other">The other shape.</param>
    public bool Equals(ParametricShape? other) => other is not null && Bands.SequenceEqual(other.Bands);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var band in Bands) hash.Add(band);
        return hash.ToHashCode();
    }
}

/// <summary>A parametric shape saved under a name the person chose.</summary>
/// <param name="Name">The name.</param>
/// <param name="Shape">The bands.</param>
public sealed record ParametricPreset(string Name, ParametricShape Shape);

/// <summary>What is wrong with a name for a parametric preset, if anything.</summary>
public enum PresetNameProblem
{
    None,
    Empty,
    Taken,
}

/// <summary>The rules for the parametric equaliser's saved presets, which Neap keeps rather than the headset.</summary>
/// <remarks>
/// A preset is chosen, saved over or renamed by its name; names are compared
/// ignoring case and the spaces around them, so two cannot read alike.
/// </remarks>
public static class ParametricPresets
{
    /// <summary>The longest name a preset can have, so it fits the picker.</summary>
    public const int LongestName = 40;

    /// <summary>What is wrong with a name, if anything.</summary>
    /// <param name="name">The name typed.</param>
    /// <param name="presets">The presets saved.</param>
    /// <param name="renaming">The preset being renamed, whose own name is free to keep, or null.</param>
    public static PresetNameProblem Check(string name, IReadOnlyList<ParametricPreset> presets, string? renaming = null)
    {
        if (name.Trim().Length == 0) return PresetNameProblem.Empty;
        return presets.Any(p => Same(p.Name, name) && !(renaming is not null && Same(p.Name, renaming)))
            ? PresetNameProblem.Taken
            : PresetNameProblem.None;
    }

    /// <summary>The preset with a name, or null when there is none.</summary>
    /// <param name="presets">The presets saved.</param>
    /// <param name="name">The name, or null.</param>
    public static ParametricPreset? Find(IReadOnlyList<ParametricPreset> presets, string? name) =>
        name is null ? null : presets.FirstOrDefault(p => Same(p.Name, name));

    /// <summary>The first preset with the same bands as a shape, or null, so a shape put back by a profile shows its name.</summary>
    /// <param name="presets">The presets saved.</param>
    /// <param name="shape">The shape.</param>
    public static ParametricPreset? Matching(IReadOnlyList<ParametricPreset> presets, ParametricShape shape) =>
        presets.FirstOrDefault(p => p.Shape == shape);

    /// <summary>The presets with one saved: a new one at the end, or over the one with its name.</summary>
    /// <param name="presets">The presets saved.</param>
    /// <param name="name">The name.</param>
    /// <param name="shape">The bands.</param>
    public static IReadOnlyList<ParametricPreset> Saved(IReadOnlyList<ParametricPreset> presets, string name, ParametricShape shape)
    {
        var preset = new ParametricPreset(name.Trim(), shape);
        int at = presets.ToList().FindIndex(p => Same(p.Name, name));
        return at < 0 ? [.. presets, preset] : [.. presets.Select((p, i) => i == at ? p with { Shape = shape } : p)];
    }

    /// <summary>The presets with one renamed.</summary>
    /// <param name="presets">The presets saved.</param>
    /// <param name="from">The name it has.</param>
    /// <param name="to">The name it is given.</param>
    public static IReadOnlyList<ParametricPreset> Renamed(IReadOnlyList<ParametricPreset> presets, string from, string to) =>
        [.. presets.Select(p => Same(p.Name, from) ? p with { Name = to.Trim() } : p)];

    /// <summary>The presets with one deleted.</summary>
    /// <param name="presets">The presets saved.</param>
    /// <param name="name">Its name.</param>
    public static IReadOnlyList<ParametricPreset> Without(IReadOnlyList<ParametricPreset> presets, string name) =>
        [.. presets.Where(p => !Same(p.Name, name))];

    /// <summary>What can be done with the shape on screen and the preset it came from.</summary>
    /// <param name="shape">The shape on screen.</param>
    /// <param name="baseline">The preset it came from, or null.</param>
    public static ParametricPresetActions Actions(ParametricShape shape, ParametricPreset? baseline) =>
        new(Edited: baseline is not null && baseline.Shape != shape,
            Save: baseline is not null && baseline.Shape != shape,
            Rename: baseline is not null,
            Delete: baseline is not null);

    private static bool Same(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Which of a parametric preset's actions can be taken.</summary>
/// <remarks>Saving as new can always be taken, so it is not here.</remarks>
/// <param name="Edited">The shape has moved since its preset was chosen.</param>
/// <param name="Save">The shape can be saved over its preset.</param>
/// <param name="Rename">The preset can be renamed.</param>
/// <param name="Delete">The preset can be deleted.</param>
public readonly record struct ParametricPresetActions(bool Edited, bool Save, bool Rename, bool Delete);
