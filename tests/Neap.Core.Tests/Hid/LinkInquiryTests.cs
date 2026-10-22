using Neap.Core.Hid;

namespace Neap.Core.Tests.Hid;

public class LinkInquiryTests
{
    private static byte[] Report(params byte[] start)
    {
        var report = new byte[62];
        start.CopyTo(report, 0);
        return report;
    }

    [Fact]
    public void TheRequestIsTheFrameSwarmIISends() =>
        Assert.Equal(
            new byte[] { 0x06, 0x0a, 0x00, 0x05, 0x5a, 0x06, 0x00, 0x31, 0x99, 0x01, 0x00, 0x00, 0x00 },
            LinkInquiry.Frame);

    [Fact]
    public void TheRequestIsAnsweredByTheTransmitterNotRelayed() => Assert.Equal(0x00, LinkInquiry.Frame[2] & 0x80);

    [Theory]
    [InlineData(0x01, true)]
    [InlineData(0x00, false)]
    public void TheReplySaysWhetherTheHeadsetIsLinked(byte answer, bool linked) =>
        Assert.Equal(linked, LinkInquiry.Linked(Report(0x07, 0x07, 0x00, 0x05, 0x5b, 0x03, 0x00, 0x31, 0x99, answer)));

    [Fact]
    public void AnEmptyReportIsNoAnswer() => Assert.Null(LinkInquiry.Linked(Report(0x07, 0x00, 0x00)));

    [Fact]
    public void AnotherCommandsReplyIsNoAnswer() =>
        Assert.Null(LinkInquiry.Linked(Report(0x07, 0x07, 0x00, 0x05, 0x5b, 0x03, 0x00, 0x0a, 0x00, 0x01)));

    [Fact]
    public void AnAnswerOtherThanYesOrNoIsNoAnswer() =>
        Assert.Null(LinkInquiry.Linked(Report(0x07, 0x07, 0x00, 0x05, 0x5b, 0x03, 0x00, 0x31, 0x99, 0x05)));
}
