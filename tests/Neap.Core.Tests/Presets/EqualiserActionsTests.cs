using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class EqualiserActionsTests
{
    private static readonly Preset Factory = new(2, "Bass Boost", new int[10], Bank.Game, false);
    private static readonly Preset Yours = new(16, "Mud cut", new int[10], Bank.Game, true);

    [Fact]
    public void AnUnchangedFactoryPresetCanOnlyBeDuplicatedAndNothingLeads() =>
        Assert.Equal(new EqualiserActions(false, true, false, true, false, false), EqualiserActions.For(false, Factory));

    [Fact]
    public void AnEditedFactoryPresetCanBeDiscardedOrSavedAsNewWhichLeads() =>
        Assert.Equal(new EqualiserActions(true, true, false, false, false, true), EqualiserActions.For(true, Factory));

    [Fact]
    public void AnUnchangedPresetOfYoursShowsOverwriteWithoutEnablingIt() =>
        Assert.Equal(new EqualiserActions(false, true, false, true, true, false), EqualiserActions.For(false, Yours));

    [Fact]
    public void AnEditedPresetOfYoursCanAlsoBeOverwritten() =>
        Assert.Equal(new EqualiserActions(true, true, true, false, true, true), EqualiserActions.For(true, Yours));

    [Fact]
    public void ACurveFromNoKnownPresetCanOnlyBeSavedWhichLeads() =>
        Assert.Equal(new EqualiserActions(false, true, false, false, false, true), EqualiserActions.For(false, null));
}
