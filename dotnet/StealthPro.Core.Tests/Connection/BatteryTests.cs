using StealthPro.Core.Connection;

namespace StealthPro.Core.Tests.Connection;

public class BatteryTests
{
    [Theory]
    [InlineData(Link.Connected, Route.ChargingDock, true)]
    [InlineData(Link.Quiet, Route.DirectUsb, true)]
    [InlineData(Link.Quiet, Route.ChargingDock, false)]
    [InlineData(Link.Silent, Route.UsbTransmitter, false)]
    [InlineData(Link.Connecting, Route.Unknown, false)]
    [InlineData(Link.Absent, Route.Unknown, false)]
    public void OnlyARealReadingIsShown(Link link, Route route, bool shown) =>
        Assert.Equal(shown, Battery.Of(new HeadsetStatus(link, route, "", ""), 76, 0) is not null);

    [Fact]
    public void NoReportNoReading() =>
        Assert.Null(Battery.Of(new HeadsetStatus(Link.Connected, Route.ChargingDock, "", ""), null, 1));

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(null, false)]
    public void SaysWhetherItIsCharging(int? power, bool charging) =>
        Assert.Equal(new BatteryReading(76, charging),
            Battery.Of(new HeadsetStatus(Link.Connected, Route.ChargingDock, "", ""), 76, power));
}
