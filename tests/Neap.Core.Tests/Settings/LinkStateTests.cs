using Neap.Core.Settings;

namespace Neap.Core.Tests.Settings;

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

    [Theory]
    [InlineData(3, BluetoothState.Connected)]
    [InlineData(2, BluetoothState.NotConnected)]
    public void TheStealthProIISaysWhetherBluetoothIsConnected(int value, BluetoothState state)
    {
        Assert.Equal(state, LinkState.BluetoothOf(HeadsetModels.StealthProII, value));
        Assert.Equal(state, LinkState.BluetoothOf(null, value));
    }

    /// <summary>One owner's Atlas Air read 1 on its transmitter and 0 while switched to Bluetooth.</summary>
    [Theory]
    [InlineData(1, BluetoothState.NotInUse)]
    [InlineData(0, BluetoothState.InUse)]
    public void TheAtlasAirSaysWhetherItIsListeningOverBluetooth(int value, BluetoothState state) =>
        Assert.Equal(state, LinkState.BluetoothOf(HeadsetModels.AtlasAir, value));

    [Fact]
    public void AnUnseenAttachmentIsUnknown()
    {
        Assert.Equal(Attachment.Unknown, LinkState.How(0));
    }
}
