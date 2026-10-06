using Neap.Core.Updates;

namespace Neap.Core.Tests.Updates;

public class UpdateBarLookTests
{
    [Theory]
    [InlineData(UpdateStage.Available)]
    [InlineData(UpdateStage.UpdateFailed)]
    [InlineData(UpdateStage.CannotUpdateHere)]
    public void WhenThereIsAVersionToUpdateToEverythingIsOffered(UpdateStage stage)
    {
        Assert.Equal(new UpdateBarLook(ActEnabled: true, Notes: true, PutAway: true, UpdateProgress.None),
            UpdateBarLook.Of(stage));
    }

    [Theory]
    [InlineData(UpdateStage.Downloading, UpdateProgress.Percent)]
    [InlineData(UpdateStage.Installing, UpdateProgress.Unknown)]
    public void WhileAnUpdateRunsTheButtonIsOffAndNothingElseIsOffered(UpdateStage stage, UpdateProgress progress)
    {
        Assert.Equal(new UpdateBarLook(ActEnabled: false, Notes: false, PutAway: false, progress),
            UpdateBarLook.Of(stage));
    }

    [Fact]
    public void OnlyAnUpdateRunningShowsProgress()
    {
        foreach (var stage in Enum.GetValues<UpdateStage>()
            .Where(s => s is not (UpdateStage.Downloading or UpdateStage.Installing)))
            Assert.Equal(UpdateProgress.None, UpdateBarLook.Of(stage).Progress);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(719.9, true)]
    [InlineData(720, false)]
    [InlineData(1920, false)]
    public void BelowTheThresholdTheBannerUsesAMenu(double width, bool compact)
    {
        Assert.Equal(compact, UpdateBarLook.Compact(width));
    }
}
