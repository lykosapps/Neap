using Neap.Core.Hid;

namespace Neap.Core.Tests.Hid;

public class TransmitterLightTests
{
    private static byte[] Report(params byte[] start)
    {
        var report = new byte[62];
        start.CopyTo(report, 0);
        return report;
    }

    [Fact]
    public void TheRequestIsTheReadSP2ToolsFound() =>
        Assert.Equal(new byte[] { 0x06, 0x07, 0x00, 0x05, 0x5a, 0x03, 0x00, 0x06, 0x00, 0x03 }, TransmitterLight.ReadFrame);

    [Fact]
    public void TheRequestIsAnsweredByTheTransmitterNotRelayed() => Assert.Equal(0x00, TransmitterLight.ReadFrame[2] & 0x80);

    [Fact]
    public void TheRequestNeverSetsTheLevel() => Assert.NotEqual(2, TransmitterLight.ReadFrame[^1]);

    [Theory]
    [InlineData(0x64, 100)]
    [InlineData(0x32, 50)]
    [InlineData(0x00, 0)]
    public void TheReplyHoldsTheLevel(byte level, int expected) =>
        Assert.Equal(expected, TransmitterLight.LevelInReply(Report(0x07, 0x0a, 0x00, 0x05, 0x5b, 0x06, 0x00, 0x06, 0x00, level, 0, 0, 0)));

    [Fact]
    public void ALevelPastFullIsNoReading() =>
        Assert.Null(TransmitterLight.LevelInReply(Report(0x07, 0x0a, 0x00, 0x05, 0x5b, 0x06, 0x00, 0x06, 0x00, 0x65, 0, 0, 0)));

    [Fact]
    public void AnEmptyReportIsNoReading() => Assert.Null(TransmitterLight.LevelInReply(Report(0x07, 0x00, 0x00)));
}
