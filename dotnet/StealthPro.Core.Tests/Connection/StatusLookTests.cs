using StealthPro.Core.Connection;

namespace StealthPro.Core.Tests.Connection;

public class StatusLookTests
{
    [Theory]
    [InlineData(Link.Connected, Route.ChargingDock, false, Headline.Connected, Tone.Good)]
    [InlineData(Link.Connected, Route.ChargingDock, true, Headline.NoSound, Tone.Caution)]
    [InlineData(Link.Silent, Route.UsbTransmitter, false, Headline.SettingsUnavailable, Tone.Neutral)]
    [InlineData(Link.Quiet, Route.DirectUsb, false, Headline.HeadsetOff, Tone.Neutral)]
    [InlineData(Link.Quiet, Route.ChargingDock, false, Headline.NotConnected, Tone.Caution)]
    [InlineData(Link.Connecting, Route.Unknown, false, Headline.Connecting, Tone.Caution)]
    [InlineData(Link.Absent, Route.Unknown, false, Headline.NothingPluggedIn, Tone.Critical)]
    public void EachStateHasOneHeadlineAndTone(Link link, Route route, bool noSound, Headline headline, Tone tone)
    {
        var look = StatusLook.Of(new HeadsetStatus(link, route, "", "", NoSound: noSound));

        Assert.Equal(headline, look.Headline);
        Assert.Equal(tone, look.Tone);
    }

    [Fact]
    public void SoundSentElsewhereReadsAsNoSound()
    {
        var look = StatusLook.Of(new HeadsetStatus(Link.Connected, Route.UsbTransmitter, "", ""),
            soundElsewhere: true);

        Assert.Equal(new StatusLook(Headline.NoSound, Tone.Caution), look);
    }

    [Fact]
    public void SoundSentElsewhereDoesNotHideAHeadsetThatIsOff()
    {
        var look = StatusLook.Of(new HeadsetStatus(Link.Quiet, Route.ChargingDock, "", ""),
            soundElsewhere: true);

        Assert.Equal(Headline.NotConnected, look.Headline);
    }
}
