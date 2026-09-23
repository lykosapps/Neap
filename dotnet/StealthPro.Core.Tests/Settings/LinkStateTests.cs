using StealthPro.Core.Settings;

namespace StealthPro.Core.Tests.Settings;

public class LinkStateTests
{
    /// <summary>The four readings of 0x290 recorded in FINDINGS.md.</summary>
    [Theory]
    [InlineData(3, Attachment.Wireless, true)]
    [InlineData(2, Attachment.Wireless, false)]
    [InlineData(4, Attachment.Usb, false)]
    [InlineData(5, Attachment.Usb, true)]
    public void ReadsAttachmentAndBluetooth(int value, Attachment how, bool bluetooth)
    {
        Assert.Equal(how, LinkState.How(value));
        Assert.Equal(bluetooth, LinkState.Bluetooth(value));
    }

    [Fact]
    public void AnUnseenAttachmentIsUnknown()
    {
        Assert.Equal(Attachment.Unknown, LinkState.How(0));
    }
}
