using Neap.Core.Connection;

namespace Neap.Core.Tests.Connection;

public class HeadsetStatusTests
{
    [Fact]
    public void OnItsCableAloneTheWheelCannotReachTheApp()
    {
        var status = new HeadsetStatus(Link.Connected, Route.DirectUsb, "Headset", "");

        Assert.True(status.WheelOffOnCable);
        Assert.True(status.WheelUnreachable);
    }

    [Theory]
    [InlineData(Route.ChargingDock)]
    [InlineData(Route.UsbTransmitter)]
    public void OverATransmitterTheWheelReachesTheApp(Route route) =>
        Assert.False(new HeadsetStatus(Link.Connected, route, "", "").WheelUnreachable);

    [Fact]
    public void WithTheSoundLinkDownTheWheelCannotReachTheApp() =>
        Assert.True(new HeadsetStatus(Link.Connected, Route.ChargingDock, "", "", NoSound: true).WheelUnreachable);

    [Fact]
    public void WithTheSettingsOutOfReachTheWheelCannotReachTheApp() =>
        Assert.True(new HeadsetStatus(Link.Silent, Route.ChargingDock, "", "").WheelUnreachable);

    [Fact]
    public void SwitchedOffOnItsCableIsNotAWheelProblem() =>
        Assert.False(new HeadsetStatus(Link.Quiet, Route.DirectUsb, "Headset", "").WheelOffOnCable);
}
