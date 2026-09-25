using Neap.Core.Connection;

namespace Neap.Core.Tests.Connection;

public class TransmitterLightsTests
{
    [Theory]
    [InlineData("2283", LightSet.Dock)]
    [InlineData("229B", LightSet.Dock)]
    [InlineData("2285", LightSet.Transmitter)]
    [InlineData("229D", LightSet.Transmitter)]
    [InlineData("2286", LightSet.None)]
    [InlineData("FFFF", LightSet.None)]
    public void OffersTheLightsOfTheTransmitterInUse(string product, LightSet lights) =>
        Assert.Equal(lights, TransmitterLights.For(
            new HeadsetStatus(Link.Connected, Route.ChargingDock, "", "", Product: product)));

    [Theory]
    [InlineData(Link.Silent)]
    [InlineData(Link.Quiet)]
    [InlineData(Link.Connecting)]
    [InlineData(Link.Absent)]
    public void OffersNoneUnlessConnected(Link link) =>
        Assert.Equal(LightSet.None, TransmitterLights.For(
            new HeadsetStatus(link, Route.ChargingDock, "", "", Product: "2283")));

    [Fact]
    public void OffersNoneWithSoundAndSettingsOnDifferentTransmitters() =>
        Assert.Equal(LightSet.None, TransmitterLights.For(
            new HeadsetStatus(Link.Connected, Route.ChargingDock, "", "", Product: "2283", ControlVia: "USB Transmitter")));
}
