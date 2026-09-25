using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class EqualiserActionsTests
{
    private static readonly Preset Factory = new(2, "Bass Boost", new int[10], Bank.Game, false);
    private static readonly Preset Yours = new(16, "Mud cut", new int[10], Bank.Game, true);

    [Fact]
    public void AnUnchangedPresetOffersNothing() =>
        Assert.Equal(new EqualiserActions(false, false, false), EqualiserActions.For(false, Factory));

    [Fact]
    public void AnEditedFactoryPresetCanBeDiscardedOrSavedAsNew() =>
        Assert.Equal(new EqualiserActions(true, true, false), EqualiserActions.For(true, Factory));

    [Fact]
    public void AnEditedPresetOfYoursCanAlsoBeOverwritten() =>
        Assert.Equal(new EqualiserActions(true, true, true), EqualiserActions.For(true, Yours));

    [Fact]
    public void ACurveFromNoKnownPresetCanOnlyBeSaved() =>
        Assert.Equal(new EqualiserActions(false, true, false), EqualiserActions.For(false, null));
}
