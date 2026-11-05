using Neap.Core.Mix;

namespace Neap.Core.Tests.Mix;

public class MixOverlayTests
{
    [Theory]
    [InlineData(MixCause.Wheel)]
    [InlineData(MixCause.Keys)]
    public void AMoveMadeWithoutLookingAtNeapShows(MixCause cause) =>
        Assert.True(MixOverlay.Shows(cause, turnedOn: true, neapInFront: false, screenTaken: false));

    [Theory]
    [InlineData(MixCause.Dial)]
    [InlineData(MixCause.Profile)]
    public void AMoveMadeInNeapDoesNotShow(MixCause cause) =>
        Assert.False(MixOverlay.Shows(cause, turnedOn: true, neapInFront: false, screenTaken: false));

    [Fact]
    public void ItDoesNotShowWhenSwitchedOff() =>
        Assert.False(MixOverlay.Shows(MixCause.Wheel, turnedOn: false, neapInFront: false, screenTaken: false));

    [Fact]
    public void ItDoesNotShowWhileNeapIsInFront() =>
        Assert.False(MixOverlay.Shows(MixCause.Keys, turnedOn: true, neapInFront: true, screenTaken: false));

    [Fact]
    public void ItNeverShowsOverAGameThatHasTheScreenToItself() =>
        Assert.False(MixOverlay.Shows(MixCause.Wheel, turnedOn: true, neapInFront: false, screenTaken: true));

    [Fact]
    public void ItSitsCentredAboveTheSystemsVolumeFlyout() =>
        Assert.Equal(new Box(840, 900, 240, 44),
            MixOverlay.Place(new Box(0, 0, 1920, 1032), 240, 44, MixOverlay.SystemFlyoutBand));

    [Fact]
    public void ItCentresOnASecondScreenAndScalesItsBand() =>
        Assert.Equal(new Box(2400, 1800, 480, 88),
            MixOverlay.Place(new Box(1920, 0, 1440, 2064), 480, 88, 2 * MixOverlay.SystemFlyoutBand));
}
