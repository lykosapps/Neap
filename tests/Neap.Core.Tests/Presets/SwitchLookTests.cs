using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class SwitchLookTests
{
    [Theory]
    [InlineData(SwitchOutcome.Done, true, SwitchSaying.TurnedOn)]
    [InlineData(SwitchOutcome.Done, false, SwitchSaying.RestoredStock)]
    [InlineData(SwitchOutcome.AlreadyDone, true, SwitchSaying.WasAlreadyOn)]
    [InlineData(SwitchOutcome.AlreadyDone, false, SwitchSaying.WasAlreadyStock)]
    [InlineData(SwitchOutcome.CouldNotRead, true, SwitchSaying.CouldNotRead)]
    [InlineData(SwitchOutcome.NoHeadset, false, SwitchSaying.NoHeadset)]
    [InlineData(SwitchOutcome.LowBattery, true, SwitchSaying.LowBattery)]
    [InlineData(SwitchOutcome.ReadOnly, true, SwitchSaying.ReadOnly)]
    public void EachResultHasItsOwnSaying(SwitchOutcome outcome, bool turningOn, SwitchSaying expected) =>
        Assert.Equal(expected, SwitchLook.Say(new SwitchResult(outcome), turningOn));

    [Theory]
    [InlineData(SwitchOutcome.WriteFailed)]
    [InlineData(SwitchOutcome.NotConfirmed)]
    public void AFailedChangeSaysWhetherTheEarlierSettingsWerePutBack(SwitchOutcome outcome)
    {
        Assert.Equal(SwitchSaying.NotChangedAndPutBack, SwitchLook.Say(new SwitchResult(outcome, BackupRestored: true), true));
        Assert.Equal(SwitchSaying.NotChangedAndNotPutBack, SwitchLook.Say(new SwitchResult(outcome, BackupRestored: false), false));
    }

    [Fact]
    public void EveryOutcomeHasASaying() =>
        Assert.All(Enum.GetValues<SwitchOutcome>(), outcome => Assert.True(Enum.IsDefined(SwitchLook.Say(new SwitchResult(outcome), true))));
}
