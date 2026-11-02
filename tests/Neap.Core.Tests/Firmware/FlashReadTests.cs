using Neap.Core.Firmware;
using Neap.Core.Pretend;

namespace Neap.Core.Tests.Firmware;

public class FlashReadTests
{
    private static readonly FirmwareEdit Cycle = FirmwareEdit.NoiseCycle;
    private static readonly FlashChange Call = Cycle.Changes[1];
    private static readonly TimeSpan Wait = TimeSpan.FromMilliseconds(200);

    [Fact]
    public void AReadThroughTheDockMatchesSp2ToolsFrame() =>
        // SP2Tools' dock_read.py, read of 0x08232094 relayed by the dock.
        Assert.Equal(Convert.FromHexString("060C80055A08008016000094202308"), FlashRead.ReadFrame(0x08232094, relayThroughBase: true)[..15]);

    [Fact]
    public void TheReplyGivesTheBytesInFlashOrder()
    {
        var report = Input(FlashRead.Reply(0x08232094, [0x16, 0xF1, 0xB4, 0xFF]));
        Assert.Equal(0xFFB4F116u, FlashRead.WordInReply(report, 0x08232094));
    }

    [Fact]
    public void AReplyForAnotherAddressIsNotTheOne() =>
        Assert.Null(FlashRead.WordInReply(Input(FlashRead.Reply(0x08232090, [1, 2, 3, 4])), 0x08232094));

    [Fact]
    public void AFailedReadIsNotTakenForBytes()
    {
        var reply = FlashRead.Reply(0x08232094, [1, 2, 3, 4]);
        reply[6] = 1;
        Assert.Null(FlashRead.WordInReply(Input(reply), 0x08232094));
    }

    [Fact]
    public void TheReadsCoverARunThatStartsPartWayIntoAWord() =>
        Assert.Equal([0x082F9B94], FlashRead.Words(0x082F9B94, 1));

    [Fact]
    public void ARunOfManyWordsIsReadWordByWord() =>
        Assert.Equal(27, FlashRead.Words(0x08349000, 108).Count());

    [Fact]
    public void ThePretendHeadsetsFlashReadsFactoryUntilAnEditIsWritten()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(transport: headset.Open());

        Assert.Equal(Call.Factory, client.ReadFlash(Call.Address, Call.Factory.Count, Wait));

        headset.Flash.Write(0x08232000, [Cycle.Id]);
        Assert.Equal(Call.Active, client.ReadFlash(Call.Address, Call.Factory.Count, Wait));
    }

    [Fact]
    public void ARunNotOnAWordBoundaryIsCutFromTheWordsRead()
    {
        var headset = new PretendHeadset();
        headset.Flash.Write(0x082F9000, [FirmwareEdit.LightOffWhenDisconnected.Id]);
        using var client = new HeadsetClient(transport: headset.Open());

        Assert.Equal([0x00], client.ReadFlash(0x082F9B94, 1, Wait));
    }

    [Fact]
    public void TheStatusFollowsWhatThePretendFlashHolds()
    {
        var headset = new PretendHeadset();
        headset.Flash.Write(0x08349000, [Cycle.Id]);
        using var client = new HeadsetClient(transport: headset.Open());
        var flash = Cycle.Changes.ToDictionary(change => change.Address, change => client.ReadFlash(change.Address, change.Factory.Count, Wait)!);

        Assert.Equal(EditStatus.NotInstalled, Cycle.StatusIn(Cycle.FirmwareBuild, flash));
    }

    private static byte[] Input(byte[] message)
    {
        var report = new byte[62];
        report[0] = 0x07;
        report[1] = (byte)message.Length;
        report[2] = 0x80;
        message.CopyTo(report, 3);
        return report;
    }
}
