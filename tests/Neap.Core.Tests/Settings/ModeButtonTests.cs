using Neap.Core.Settings;

namespace Neap.Core.Tests.Settings;

public class ModeButtonTests
{
    [Fact]
    public void TheCycleIsOfferedStraightAfterNoiseCancellationOnOff() =>
        Assert.Equal(
            [new ModeChoice(0, false), new ModeChoice(0, true), new ModeChoice(1, false), new ModeChoice(2, false)],
            ModeButton.Choices);

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(0, false, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    public void TheCycleShowsOnlyWhileTheHeadsetTogglesNoiseCancellation(int function, bool cycling, bool shown) =>
        Assert.Equal(new ModeChoice(function, shown), ModeButton.Shown(function, cycling, headsetCycles: false));

    [Fact]
    public void NothingIsShownUntilTheHeadsetReports() => Assert.Null(ModeButton.Shown(null, true, headsetCycles: false));

    [Fact]
    public void EditedFirmwareOffersNoCycleOfTheApps() =>
        Assert.Equal(
            [new ModeChoice(0, false), new ModeChoice(1, false), new ModeChoice(2, false)],
            ModeButton.Choices.Where(choice => ModeButton.Offered(choice, headsetCycles: true)));

    [Fact]
    public void StockFirmwareOffersEveryChoice() =>
        Assert.All(ModeButton.Choices, choice => Assert.True(ModeButton.Offered(choice, headsetCycles: false)));

    [Fact]
    public void EditedFirmwareShowsNoiseCancellationEvenWithTheAppsCycleSaved() =>
        Assert.Equal(new ModeChoice(0, false), ModeButton.Shown(0, cycling: true, headsetCycles: true));
}
