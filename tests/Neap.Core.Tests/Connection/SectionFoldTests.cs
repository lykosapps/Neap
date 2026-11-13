using Neap.Core.Connection;

namespace Neap.Core.Tests.Connection;

public class SectionFoldTests
{
    [Theory]
    [InlineData(Link.Connecting, Fold.Unchanged)]
    [InlineData(Link.Absent, Fold.Hidden)]
    [InlineData(Link.Quiet, Fold.Off)]
    [InlineData(Link.Silent, Fold.Unreachable)]
    [InlineData(Link.Connected, Fold.Open)]
    public void SettingsFoldForEachState(Link link, Fold fold) =>
        Assert.Equal(fold, SectionFold.For(new HeadsetStatus(link, Route.ChargingDock, "", ""), soundOnly: false));

    [Theory]
    [InlineData(Link.Connecting, Fold.Unchanged)]
    [InlineData(Link.Absent, Fold.Hidden)]
    [InlineData(Link.Quiet, Fold.Open)]
    [InlineData(Link.Silent, Fold.Open)]
    [InlineData(Link.Connected, Fold.Open)]
    public void SoundStaysOpenWhileATransmitterHoldsTheDevice(Link link, Fold fold) =>
        Assert.Equal(fold, SectionFold.For(new HeadsetStatus(link, Route.UsbTransmitter, "", ""), soundOnly: true));

    [Fact]
    public void SoundFoldsWhenTheSettingsWentWithTheSound() =>
        Assert.Equal(Fold.Unreachable,
            SectionFold.For(new HeadsetStatus(Link.Silent, Route.ChargingDock, "", "", NoSound: true), soundOnly: true));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwitchedOffOnItsCableFoldsAsOff(bool soundOnly) =>
        Assert.Equal(Fold.Off,
            SectionFold.For(new HeadsetStatus(Link.Quiet, Route.DirectUsb, "", ""), soundOnly));

    [Theory]
    [InlineData(Link.Connecting)]
    [InlineData(Link.Absent)]
    [InlineData(Link.Quiet)]
    [InlineData(Link.Silent)]
    [InlineData(Link.Connected)]
    public void WithoutAccessSettingsFoldAndSoundStays(Link link)
    {
        var status = new HeadsetStatus(link, Route.ChargingDock, "", "");
        Assert.Equal(Fold.Hidden, SectionFold.For(status, soundOnly: false, accessDenied: true));
        Assert.Equal(Fold.Open, SectionFold.For(status, soundOnly: true, accessDenied: true));
    }
}
