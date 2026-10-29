using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class ParametricPresetsTests
{
    private static ParametricShape Shape(double gain) =>
        ParametricShape.Of([.. StockBands.Game.Select((band, i) => i == 4 ? band with { Gain = gain } : band)]);

    private static readonly IReadOnlyList<ParametricPreset> Two =
        [new("Footsteps", Shape(3)), new("Voices", Shape(-2))];

    [Fact]
    public void ShapesWithTheSameBandsAreEqual()
    {
        Assert.Equal(Shape(3), Shape(3));
        Assert.Equal(Shape(3).GetHashCode(), Shape(3).GetHashCode());
        Assert.NotEqual(Shape(3), Shape(2));
    }

    [Theory]
    [InlineData("", PresetNameProblem.Empty)]
    [InlineData("   ", PresetNameProblem.Empty)]
    [InlineData("footsteps ", PresetNameProblem.Taken)]
    [InlineData("Music", PresetNameProblem.None)]
    public void ANameMustBeGivenAndFree(string name, PresetNameProblem expected) =>
        Assert.Equal(expected, ParametricPresets.Check(name, Two));

    [Fact]
    public void ARenameMayKeepItsOwnName()
    {
        Assert.Equal(PresetNameProblem.None, ParametricPresets.Check("FOOTSTEPS", Two, renaming: "Footsteps"));
        Assert.Equal(PresetNameProblem.Taken, ParametricPresets.Check("Voices", Two, renaming: "Footsteps"));
    }

    [Fact]
    public void SavingANewNameAddsItAtTheEnd()
    {
        var saved = ParametricPresets.Saved(Two, " Music ", Shape(1));
        Assert.Equal(["Footsteps", "Voices", "Music"], saved.Select(p => p.Name));
    }

    [Fact]
    public void SavingATakenNameWritesOverItAndKeepsItsPlaceAndName()
    {
        var saved = ParametricPresets.Saved(Two, "footsteps", Shape(6));
        Assert.Equal(["Footsteps", "Voices"], saved.Select(p => p.Name));
        Assert.Equal(Shape(6), saved[0].Shape);
    }

    [Fact]
    public void RenamingAndDeletingFindThePresetIgnoringCase()
    {
        Assert.Equal(["Steps", "Voices"], ParametricPresets.Renamed(Two, "FOOTSTEPS", " Steps").Select(p => p.Name));
        Assert.Equal(["Voices"], ParametricPresets.Without(Two, "footsteps").Select(p => p.Name));
    }

    [Fact]
    public void AShapeFindsThePresetWithTheSameBands()
    {
        Assert.Equal("Voices", ParametricPresets.Matching(Two, Shape(-2))?.Name);
        Assert.Null(ParametricPresets.Matching(Two, Shape(9)));
        Assert.Equal("Voices", ParametricPresets.Find(Two, "voices")?.Name);
        Assert.Null(ParametricPresets.Find(Two, null));
    }

    [Fact]
    public void WithNoPresetBehindItOnlySavingAsNewIsOffered() =>
        Assert.Equal(new ParametricPresetActions(false, false, false), ParametricPresets.Actions(Shape(1), null));

    [Fact]
    public void AnUnchangedPresetCanBeRenamedButNotSavedOver() =>
        Assert.Equal(new ParametricPresetActions(false, false, true), ParametricPresets.Actions(Shape(3), Two[0]));

    [Fact]
    public void AnEditedPresetCanBeSavedOver() =>
        Assert.Equal(new ParametricPresetActions(true, true, true), ParametricPresets.Actions(Shape(4), Two[0]));
}
