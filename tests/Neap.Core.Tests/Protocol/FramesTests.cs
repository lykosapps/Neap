using System.Globalization;
using System.Text;
using Neap.Core.Presets;
using Neap.Core.Protocol;

namespace Neap.Core.Tests.Protocol;

public class FramesTests
{
    /// <summary>
    /// The ANC-level command captured from Swarm II, as FINDINGS.md lays it out.
    /// </summary>
    private static readonly byte[] CapturedAncLevel100 = Convert.FromHexString(
        "062A00" + "055A2600" + "029948" + "03" + "FBDE44" + "00000000000000" + "B7"
        + Convert.ToHexString(Encoding.ASCII.GetBytes("set_kvp")) + "FF"
        + Convert.ToHexString(Encoding.ASCII.GetBytes("{\"0x760\":\"100\"}")));

    [Fact]
    public void SetKeyMatchesTheCapturedCommand()
    {
        Assert.Equal(CapturedAncLevel100, Frames.SetKey(0x760, 100, counter: 0xDE44));
    }

    [Fact]
    public void LengthsFollowTheArgument()
    {
        byte[] frame = Frames.SetKey(0x760, 100);
        int json = "{\"0x760\":\"100\"}".Length;

        Assert.Equal(json + 27, frame[1] | (frame[2] << 8));
        Assert.Equal(json + 23, frame[5] | (frame[6] << 8));
    }

    [Fact]
    public void ReadVerbsCarryNoArgument()
    {
        byte[] frame = Frames.Build("SGSI");

        Assert.Equal(0x01, frame[10]);
        Assert.Equal(new byte[] { 0x61, 0x00 }, frame[21..23]);
        Assert.Equal("SGSI", Encoding.ASCII.GetString(frame[23..]));
    }

    [Fact]
    public void UnconfirmedVerbsAreRefused()
    {
        Assert.Throws<ProtocolException>(() => Frames.Build("SLED"));
    }

    [Fact]
    public void SetKvpNeedsAnArgument()
    {
        Assert.Throws<ProtocolException>(() => Frames.Build("set_kvp"));
    }

    [Theory]
    [InlineData("Bass & Treble Boost", "Bass & Treble Boost")]
    [InlineData("Say \"hi\"", "Say \\\"hi\\\"")]
    [InlineData("Café", @"Caf\u00e9")]
    public void NamesAreEncodedTheWayTheHeadsetStoresThem(string name, string onTheWire)
    {
        string sent = Encoding.ASCII.GetString(Frames.SetKey(0x12C0, name));

        Assert.EndsWith($"{{\"0x12c0\":\"{onTheWire}\"}}", sent);
    }

    [Fact]
    public void TheLongestFactoryNameFillsOneReportExactly()
    {
        Assert.Equal(Frames.ReportLength, Frames.SetKey(0x12C0, "Bass & Treble Boost").Length);
        Assert.True(PresetStore.NameFits("Bass & Treble Boost", Bank.Game));
        Assert.False(PresetStore.NameFits("Bass & Treble Boosts", Bank.Game));
    }

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("fa-IR")]
    [InlineData("ar-SA")]
    public void NegativeValuesAreSentTheSameOnEveryLanguage(string culture)
    {
        byte[] expected = Frames.SetKey(0x1240, -30);
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Assert.Equal(expected, Frames.SetKey(0x1240, -30));
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [Fact]
    public void PayloadReadsASixteenBitLength()
    {
        var report = new byte[62];
        report[0] = Frames.InReportId;
        report[1] = 0x2C;
        report[2] = 0x00;

        Assert.Equal(0x2C, Frames.PayloadOf(report).Length);
    }

    [Fact]
    public void PayloadOfAnotherReportIsEmpty()
    {
        Assert.True(Frames.PayloadOf(new byte[] { 0x06, 0x05, 0x00, 1, 2, 3, 4, 5 }).IsEmpty);
    }
}
