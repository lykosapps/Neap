using StealthPro.Core.Connection;

namespace StealthPro.Core.Tests.Connection;

public class ConnectionLineTests
{
    private static HeadsetStatus Connected(bool noSound = false) =>
        new(Link.Connected, Route.ChargingDock, "Charging Dock", "", Product: "2B00", NoSound: noSound);

    [Theory]
    [InlineData(Link.Quiet)]
    [InlineData(Link.Silent)]
    [InlineData(Link.Connecting)]
    [InlineData(Link.Absent)]
    public void NotConnectedShowsTheState(Link link) =>
        Assert.Equal(ConnectionShown.State,
            ConnectionLine.Of(new HeadsetStatus(link, Route.ChargingDock, "", ""), cable: true, ["2B00"]));

    [Fact]
    public void TheCableWinsOverAnyTransmitter() =>
        Assert.Equal(ConnectionShown.Cable, ConnectionLine.Of(Connected(noSound: true), cable: true, ["2B00"]));

    [Fact]
    public void NoSoundNamesNoTransmitter() =>
        Assert.Equal(ConnectionShown.NoTransmitter, ConnectionLine.Of(Connected(noSound: true), cable: false, ["2B00"]));

    [Fact]
    public void ThePluggedInTransmitterIsNamed() =>
        Assert.Equal(ConnectionShown.Transmitter, ConnectionLine.Of(Connected(), cable: false, ["2b00", "2B01"]));

    [Fact]
    public void ATransmitterNotPluggedInIsSaidToBe() =>
        Assert.Equal(ConnectionShown.TransmitterUnplugged, ConnectionLine.Of(Connected(), cable: false, ["2B01"]));

    [Fact]
    public void NotYetLookingIsNotUnplugged()
    {
        Assert.Equal(ConnectionShown.Transmitter, ConnectionLine.Of(Connected(), cable: false, null));
        Assert.Equal(ConnectionShown.Transmitter, ConnectionLine.Of(Connected(), cable: false, []));
    }

    [Theory]
    [InlineData(Route.DirectUsb, "", true)]
    [InlineData(Route.ChargingDock, "2B02", true)]
    [InlineData(Route.ChargingDock, "", false)]
    public void TheCableIsWhatIsPluggedInNotWhereWindowsPlays(Route route, string cableDevice, bool over) =>
        Assert.Equal(over, ConnectionLine.OverCable(new HeadsetStatus(Link.Connected, route, "", ""), cableDevice));
}
