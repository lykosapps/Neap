using Neap.Core.Updates;

namespace Neap.Core.Tests.Updates;

public class UpdateReminderTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Version Newer = new(0, 3, 0);

    private static bool Shows(UpdateStage stage, string? skipped = null, DateTimeOffset? hiddenUntil = null) =>
        UpdateReminder.Shows(stage, Newer, skipped, hiddenUntil, Noon);

    [Theory]
    [InlineData(UpdateStage.Available)]
    [InlineData(UpdateStage.CannotUpdateHere)]
    [InlineData(UpdateStage.Downloading)]
    [InlineData(UpdateStage.Installing)]
    [InlineData(UpdateStage.UpdateFailed)]
    public void ANewerVersionIsOnShowWhileThereIsSomethingToDoAboutIt(UpdateStage stage)
    {
        Assert.True(Shows(stage));
    }

    [Theory]
    [InlineData(UpdateStage.NotChecked)]
    [InlineData(UpdateStage.Checking)]
    [InlineData(UpdateStage.UpToDate)]
    [InlineData(UpdateStage.CheckFailed)]
    public void NothingIsOnShowWithoutANewerVersionToOffer(UpdateStage stage)
    {
        Assert.False(Shows(stage));
        Assert.False(UpdateReminder.Shows(UpdateStage.Available, null, null, null, Noon));
    }

    [Fact]
    public void ASkippedVersionIsNotOnShow()
    {
        Assert.False(Shows(UpdateStage.Available, skipped: "0.3.0"));
    }

    [Fact]
    public void ANewerVersionThanTheOneSkippedIsOnShowAgain()
    {
        Assert.True(Shows(UpdateStage.Available, skipped: "0.2.5"));
        Assert.True(UpdateReminder.Shows(UpdateStage.Available, new Version(0, 3, 1), "0.3.0", null, Noon));
    }

    [Fact]
    public void AHoldKeepsItAwayUntilItEnds()
    {
        Assert.False(Shows(UpdateStage.Available, hiddenUntil: Noon.AddHours(1)));
        Assert.True(Shows(UpdateStage.Available, hiddenUntil: Noon));
        Assert.True(Shows(UpdateStage.Available, hiddenUntil: Noon.AddHours(-1)));
    }

    [Fact]
    public void APutAwayBannerStaysAwayWhileUpdatingFromSettings()
    {
        // Updating from Settings after putting the banner away doesn't bring it back.
        Assert.False(Shows(UpdateStage.Downloading, hiddenUntil: Noon.AddHours(1)));
    }

    [Fact]
    public void AHoldEndingMoreThanADayAheadIsOver()
    {
        // The clock was moved back since it was made.
        Assert.True(Shows(UpdateStage.Available, hiddenUntil: Noon.AddDays(30)));
    }
}
