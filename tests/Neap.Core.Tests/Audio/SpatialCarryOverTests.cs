using Neap.Core.Audio;

namespace Neap.Core.Tests.Audio;

public class SpatialCarryOverTests
{
    [Fact]
    public void TheFirstEndpointSeenNeverCarriesOverAnything() =>
        Assert.False(SpatialCarryOver.ShouldCarryOver(null, "dock", SpatialFormat.DolbyAtmos, SpatialFormat.Off));

    [Fact]
    public void TheSameEndpointAgainDoesNotCarryOver() =>
        Assert.False(SpatialCarryOver.ShouldCarryOver("dock", "dock", SpatialFormat.DolbyAtmos, SpatialFormat.Off));

    [Fact]
    public void ANewEndpointWithNothingChosenBeforeDoesNotCarryOver() =>
        Assert.False(SpatialCarryOver.ShouldCarryOver("dock", "usb", null, SpatialFormat.Off));

    [Fact]
    public void ANewEndpointAlreadyShowingTheChosenFormatDoesNotCarryOver() =>
        Assert.False(SpatialCarryOver.ShouldCarryOver("dock", "usb", SpatialFormat.DolbyAtmos, SpatialFormat.DolbyAtmos));

    [Fact]
    public void ANewEndpointShowingSomethingElseCarriesOverTheChosenFormat() =>
        Assert.True(SpatialCarryOver.ShouldCarryOver("dock", "usb", SpatialFormat.DolbyAtmos, SpatialFormat.Off));

    [Fact]
    public void OffIsCarriedOverLikeAnyOtherFormat() =>
        Assert.True(SpatialCarryOver.ShouldCarryOver("dock", "usb", SpatialFormat.Off, SpatialFormat.WindowsSonic));
}
