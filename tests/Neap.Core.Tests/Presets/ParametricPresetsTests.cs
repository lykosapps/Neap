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
    public void WithNoPresetBehindItOnlySavingAsNewIsOffered()
    {
        var can = ParametricPresets.Actions(Shape(1), null);
        Assert.False(can.Edited);
        Assert.False(can.Rename);
        Assert.Equal(EqualiserActions.For(edited: false, baseline: null), can.Buttons);
    }

    [Fact]
    public void AnUnchangedPresetCanBeRenamedOrDuplicatedButNotSavedOver()
    {
        var can = ParametricPresets.Actions(Shape(3), Two[0]);
        Assert.False(can.Edited);
        Assert.True(can.Rename);
        Assert.False(can.Buttons.Overwrite);
        Assert.True(can.Buttons.Duplicate);
        Assert.False(can.Buttons.Discard);
    }

    [Fact]
    public void AnEditedPresetCanBeSavedOverOrDiscarded()
    {
        var can = ParametricPresets.Actions(Shape(4), Two[0]);
        Assert.True(can.Edited);
        Assert.True(can.Buttons.Overwrite);
        Assert.True(can.Buttons.Discard);
    }

    /// <summary>A built-in shape is chosen as one of Turtle Beach's presets is, never as an edit of the preset before it.</summary>
    [Fact]
    public void ABuiltInShapeIsAPresetOfItsOwnThatCannotBeSavedOver()
    {
        var flat = ParametricShape.Of(StartingShapes.Bands(StartingShape.Flat));
        var chosen = ParametricPresets.Actions(flat, null, StartingShape.Flat);
        Assert.False(chosen.Edited);
        Assert.False(chosen.Rename);
        Assert.True(chosen.Buttons.Duplicate);
        Assert.False(chosen.Buttons.OfferOverwrite);

        var moved = ParametricPresets.Actions(Shape(2), null, StartingShape.Flat);
        Assert.True(moved.Edited);
        Assert.True(moved.Buttons.Discard);
        Assert.False(moved.Buttons.Overwrite);
    }

    [Fact]
    public void DiscardPutsBackWhatTheShapeCameFrom()
    {
        Assert.Equal(Two[0].Shape, ParametricPresets.From(Two[0], StartingShape.AddBass));
        Assert.Equal(ParametricShape.Of(StartingShapes.Bands(StartingShape.AddBass)), ParametricPresets.From(null, StartingShape.AddBass));
        Assert.Null(ParametricPresets.From(null, null));
    }

    /// <summary>A profile holds a shape, not its name, so an unchanged built-in shape is named again by its bands.</summary>
    [Fact]
    public void AnUnchangedBuiltInShapeIsKnownByItsBands()
    {
        Assert.Equal(StartingShape.CutMud, ParametricPresets.MatchingBuiltIn(ParametricShape.Of(StartingShapes.Bands(StartingShape.CutMud))));
        Assert.Null(ParametricPresets.MatchingBuiltIn(Shape(7)));
    }

    [Fact]
    public void BothEqualisersFollowOneRule()
    {
        foreach (bool edited in new[] { false, true })
        {
            Assert.Equal(EqualiserActions.For(edited, new Preset(16, "Mine", new int[10], Bank.Game, true)), EqualiserActions.For(edited, known: true, yours: true));
            Assert.Equal(EqualiserActions.For(edited, baseline: null), EqualiserActions.For(edited, known: false, yours: false));
        }
    }
}
