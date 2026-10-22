using Neap.Core.Audio;

namespace Neap.Core.Tests.Audio;

public class SidetoneTests
{
    private static byte[] Report(params byte[] start)
    {
        var report = new byte[62];
        start.CopyTo(report, 0);
        return report;
    }

    [Fact]
    public void TheReadIsTheFrameSP2ToolsGives() =>
        Assert.Equal(
            new byte[] { 0x06, 0x10, 0x80, 0x05, 0x5a, 0x0c, 0x00, 0x0a, 0x00, 0x00, 0x01, 0x03, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            Sidetone.ReadFrame(relayThroughBase: true));

    [Fact]
    public void TheReadNeverSetsTheGain() => Assert.Equal(new byte[] { 0x00, 0x01 }, Sidetone.ReadFrame(true)[9..11]);

    [Theory]
    [InlineData(0, -96f)]
    [InlineData(1, -29.7f)]
    [InlineData(20, -24f)]
    [InlineData(50, -15f)]
    [InlineData(100, 0f)]
    public void EachLevelHasTheGainTheFirmwareStores(int level, float db) =>
        Assert.Equal(db, Sidetone.GainFor(level), 0.01f);

    [Fact]
    public void TheReplyHoldsTheStoredGain() =>
        Assert.Equal(-24f, Sidetone.GainInReply(Report(0x07, 0x0e, 0x80, 0x05, 0x5b, 0x0a, 0x00, 0x0a, 0x00, 0x03, 0x02, 0x00, 0x00, 0xc1, 0xc0, 0x00, 0x00)));

    [Fact]
    public void TheLoudnessReplyIsNotTheSidetones() =>
        Assert.Null(Sidetone.GainInReply(Report(0x07, 0x0e, 0x80, 0x05, 0x5b, 0x0a, 0x00, 0x0a, 0x00, 0x00, 0x04, 0x00, 0x00, 0xc1, 0x40, 0x00, 0x00)));

    [Fact]
    public void AnEmptyReportIsNoReading() => Assert.Null(Sidetone.GainInReply(Report(0x07, 0x00, 0x00)));
}
