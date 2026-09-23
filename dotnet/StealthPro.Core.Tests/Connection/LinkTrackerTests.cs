using StealthPro.Core.Connection;

namespace StealthPro.Core.Tests.Connection;

public class LinkTrackerTests
{
    private const ushort Dock = 0x229B;
    private const ushort Transmitter = 0x229D;
    private const ushort Headset = 0x229E;

    private static readonly LinkWords Words = new(
        "looking", "off", "unreachable", "off on cable", route => route.ToString());

    private TimeSpan _now;
    private bool _cabled;
    private readonly LinkTracker _link;

    public LinkTrackerTests() => _link = new LinkTracker(Words, () => _now, () => _cabled);

    private void After(double seconds) => _now += TimeSpan.FromSeconds(seconds);

    private HeadsetStatus? OnTheDock() =>
        _link.Answering(false, Route.ChargingDock, "Charging Dock", "Charging Dock", "device", "229B");

    private HeadsetStatus? OnTheCable() =>
        _link.Answering(false, Route.DirectUsb, "Headset", "Headset", "device", "229E");

    [Fact]
    public void StartsOutLooking()
    {
        Assert.Equal(Link.Connecting, _link.Status.Link);
        Assert.Equal("looking", _link.Status.Detail);
    }

    [Fact]
    public void AnAnsweringHeadsetIsConnected()
    {
        var status = OnTheDock();

        Assert.NotNull(status);
        Assert.Equal(Link.Connected, status.Link);
        Assert.Equal(Route.ChargingDock, status.Route);
        Assert.Equal("Charging Dock", status.Adapter);
        Assert.False(status.NoSound);
    }

    [Fact]
    public void SoundAndSettingsCanBeOnDifferentTransmitters()
    {
        var status = _link.Answering(true, Route.ChargingDock, "Charging Dock",
            "USB Transmitter", "device", "229D");

        Assert.NotNull(status);
        Assert.Equal(Link.Connected, status.Link);
        Assert.Equal(Route.UsbTransmitter, status.Route);
        Assert.Equal("USB Transmitter", status.Adapter);
        Assert.Equal("Charging Dock", status.ControlVia);
    }

    [Fact]
    public void NoSoundOnceTheLinkHasBeenDownFiveSeconds()
    {
        OnTheDock();
        _link.SoundLink(2);
        After(60);
        _link.SoundLink(0);

        After(4.9);
        Assert.Null(_link.Refresh());
        After(0.1);
        Assert.True(_link.Refresh()?.NoSound);

        _link.SoundLink(2);
        Assert.False(_link.Refresh()?.NoSound);
    }

    [Fact]
    public void SoundThatNeverArrivedCountsOnlyAfterThirtySeconds()
    {
        OnTheDock();
        _link.SoundLink(0);

        After(29);
        Assert.Null(_link.Refresh());
        After(1);
        Assert.True(_link.Refresh()?.NoSound);
    }

    [Fact]
    public void NeverNoSoundWhileTheCableIsIn()
    {
        _cabled = true;
        OnTheDock();
        _link.SoundLink(0);

        After(60);

        Assert.Null(_link.Refresh());
        Assert.False(_link.Status.NoSound);
    }

    [Fact]
    public void AHeadsetThatStopsAnsweringIsNotConnected()
    {
        OnTheDock();

        var status = _link.Unreachable([Dock]);

        Assert.NotNull(status);
        Assert.Equal(Link.Quiet, status.Link);
        Assert.True(status.NotConnected);
        Assert.Equal(Route.ChargingDock, status.Route);
        Assert.Equal("off", status.Detail);
    }

    [Fact]
    public void UnpluggingTheTransmitterWithTheSettingsMakesThemUnavailable()
    {
        OnTheDock();

        Assert.Equal(Link.Connecting, _link.Lost(Dock, [Transmitter], "lost")?.Link);
        var status = _link.Unreachable([Transmitter]);

        Assert.NotNull(status);
        Assert.True(status.SettingsUnreachable);
        Assert.Equal(Route.UsbTransmitter, status.Route);
        Assert.Equal("unreachable", status.Detail);
    }

    [Fact]
    public void TheLossIsForgottenOnceTheHeadsetAnswersAgain()
    {
        OnTheDock();
        _link.Lost(Dock, [Transmitter], "lost");
        _link.Answering(false, Route.UsbTransmitter, "USB Transmitter", "USB Transmitter", "device", "229D");

        Assert.Equal(Link.Quiet, _link.Unreachable([Transmitter])?.Link);
    }

    [Fact]
    public void NothingPluggedInFromTheStartIsAbsent()
    {
        var status = _link.Lost(null, [], "nothing plugged in");

        Assert.Equal(Link.Absent, status?.Link);
        Assert.Equal("nothing plugged in", status?.Detail);
    }

    [Fact]
    public void ADeviceGoneForAMomentIsStillBeingLookedFor()
    {
        _cabled = true;
        OnTheCable();

        Assert.Equal(Link.Connecting, _link.Lost(Headset, [], "nothing")?.Link);
        After(3);
        Assert.Null(_link.Lost(null, [], "nothing"));
        After(3);
        Assert.Equal(Link.Absent, _link.Lost(null, [], "nothing")?.Link);
    }

    [Fact]
    public void OnItsCableWithNoSoundDeviceItIsOffAfterFiveSeconds()
    {
        Assert.Null(OnTheCable());

        After(4.9);
        Assert.Null(_link.Cable(false, Route.DirectUsb, "Headset", "Headset", "device", "229E"));
        After(0.1);
        var status = _link.Cable(false, Route.DirectUsb, "Headset", "Headset", "device", "229E");

        Assert.NotNull(status);
        Assert.True(status.SwitchedOff);
        Assert.Equal("off on cable", status.Detail);
    }

    [Fact]
    public void OnItsCableTheSoundDeviceComingBackIsConnected()
    {
        OnTheCable();
        After(5);
        _link.Cable(false, Route.DirectUsb, "Headset", "Headset", "device", "229E");

        _cabled = true;
        var status = _link.Cable(false, Route.DirectUsb, "Headset", "Headset", "device", "229E");

        Assert.Equal(Link.Connected, status?.Link);
        Assert.Equal(Route.DirectUsb, status?.Route);
    }

    [Fact]
    public void LookingAgainIsOnlyAnnouncedOnTheWayDown()
    {
        OnTheDock();
        Assert.Equal(Link.Connecting, _link.Reconnecting()?.Link);

        _link.Unreachable([Dock]);
        Assert.Null(_link.Reconnecting());
    }

    [Theory]
    [InlineData(Dock, Route.ChargingDock)]
    [InlineData(Transmitter, Route.UsbTransmitter)]
    [InlineData(Headset, Route.DirectUsb)]
    [InlineData((ushort)0x1234, Route.Unknown)]
    public void KnowsTheRouteFromTheProduct(ushort product, Route route)
    {
        Assert.Equal(route, LinkTracker.RouteOf(product));
    }
}
