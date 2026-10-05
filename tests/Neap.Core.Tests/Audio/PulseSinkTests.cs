using Neap.Core.Audio.Pulse;

namespace Neap.Core.Tests.Audio;

public class PulseSinkTests
{
    [Theory]
    [InlineData("0x10f5", "10F5")]
    [InlineData("10f5", "10F5")]
    [InlineData("", "")]
    [InlineData("0x10f", "")]
    public void AUsbIdIsFourHexDigitsWhicheverServerWroteIt(string written, string read) =>
        Assert.Equal(read, PulseSink.UsbId(written));

    [Theory]
    [InlineData("10F5", "229B", true)]   // the white Xbox dock
    [InlineData("10F5", "2286", true)]   // the black Xbox headset, over its cable
    [InlineData("10F5", "0000", false)]  // a Turtle Beach device outside the family
    [InlineData("046D", "229B", false)]  // another maker's device with the same product id
    [InlineData("", "", false)]          // not USB at all
    public void TheHeadsetIsKnownByItsUsbIds(string vendor, string product, bool headset) =>
        Assert.Equal(headset, new PulseSink(1, "alsa_output", "Speakers", vendor, product).IsHeadset);
}
