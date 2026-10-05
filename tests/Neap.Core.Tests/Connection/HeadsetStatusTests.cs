using Neap.Core.Connection;

namespace Neap.Core.Tests.Connection;

public class HeadsetStatusTests
{
    [Fact]
    public void TheSummaryNamesTheStateAndWhereItIs()
    {
        var status = new HeadsetStatus(Link.Connected, Route.ChargingDock, "Charging Dock", "detail",
            ControlVia: "USB Transmitter", NoSound: true);

        Assert.Equal("Connected via Charging Dock, settings via USB Transmitter, no sound", status.Summary);
    }

    [Fact]
    public void TheSummaryLeavesOutTheDetail()
    {
        Assert.Equal("Quiet", new HeadsetStatus(Link.Quiet, Route.Unknown, "", "Switch it on").Summary);
    }
}
