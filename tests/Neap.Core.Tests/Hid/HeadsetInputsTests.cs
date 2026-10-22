using Neap.Core.Hid;

namespace Neap.Core.Tests.Hid;

public class HeadsetInputsTests
{
    private static byte[] Report(params byte[] start)
    {
        var report = new byte[62];
        start.CopyTo(report, 0);
        return report;
    }

    [Fact]
    public void TheRequestIsTheReadSP2ToolsFound() =>
        Assert.Equal(new byte[] { 0x06, 0x07, 0x80, 0x05, 0x5a, 0x03, 0x00, 0x00, 0x00, 0x01 }, HeadsetInputs.ReadFrame(relayThroughBase: true));

    [Fact]
    public void TheRequestOnlyReads()
    {
        Assert.Equal(0x01, HeadsetInputs.ReadFrame(true)[^1]);
        Assert.Equal(0x01, HeadsetInputs.ReadFrame(false)[^1]);
    }

    [Fact]
    public void TheReplyHoldsTheFlagsAndTheWheels()
    {
        var reading = HeadsetInputs.InReply(Report(
            0x07, 0x16, 0x00, 0x05, 0x5b, 0x12, 0x00, 0x00, 0x00,
            0xc0, 0x00, 0x01, 0x00,
            0x32, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x07, 0x00, 0x00, 0x00));

        Assert.Equal(new InputReading(0x100c0, 50, 0, 7), reading);
    }

    [Fact]
    public void AReplyOfAnotherCommandIsNoReading() =>
        Assert.Null(HeadsetInputs.InReply(Report(0x07, 0x0a, 0x00, 0x05, 0x5b, 0x06, 0x00, 0x06, 0x00, 0x32, 0, 0, 0)));

    [Fact]
    public void AnEmptyReportIsNoReading() => Assert.Null(HeadsetInputs.InReply(Report(0x07, 0x00, 0x00)));

    [Fact]
    public void EachFlagIsSaidInWords()
    {
        Assert.Equal(["button 0x7f pressed", "button 0x7f released", "wheel 0 up"], HeadsetInputs.Describe(0x100c0));
        Assert.Equal(["mute switch pressed", "mute switch's second contact released"], HeadsetInputs.Describe(0x804));
        Assert.Empty(HeadsetInputs.Describe(0));
    }

    [Fact]
    public void AFlagNotKnownIsSaidByItsNumber() => Assert.Equal(["bit 12", "bit 31"], HeadsetInputs.Describe(0x80001000));
}
