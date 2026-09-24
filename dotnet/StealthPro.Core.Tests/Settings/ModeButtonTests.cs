using StealthPro.Core.Settings;

namespace StealthPro.Core.Tests.Settings;

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
        Assert.Equal(new ModeChoice(function, shown), ModeButton.Shown(function, cycling));

    [Fact]
    public void NothingIsShownUntilTheHeadsetReports() => Assert.Null(ModeButton.Shown(null, true));
}
