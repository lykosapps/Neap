using StealthPro.Core.Connection;

namespace StealthPro.Core.Tests.Connection;

public class StatusLookTests
{
    [Theory]
    [InlineData(Link.Connected, Route.ChargingHub, false, Headline.Connected, Tone.Good)]
    [InlineData(Link.Connected, Route.ChargingHub, true, Headline.NoSound, Tone.Caution)]
    [InlineData(Link.Silent, Route.UsbTransmitter, false, Headline.SettingsUnavailable, Tone.Neutral)]
    [InlineData(Link.Quiet, Route.DirectUsb, false, Headline.HeadsetOff, Tone.Neutral)]
    [InlineData(Link.Quiet, Route.ChargingHub, false, Headline.NotConnected, Tone.Caution)]
    [InlineData(Link.Connecting, Route.Unknown, false, Headline.Connecting, Tone.Caution)]
    [InlineData(Link.Absent, Route.Unknown, false, Headline.NothingPluggedIn, Tone.Critical)]
    public void EachStateHasOneHeadlineAndTone(Link link, Route route, bool noSound, Headline headline, Tone tone)
    {
        var look = StatusLook.Of(new HeadsetStatus(link, route, "", "", NoSound: noSound));

        Assert.Equal(headline, look.Headline);
        Assert.Equal(tone, look.Tone);
    }
}
