using Neap.Core.Audio;
using Neap.Core.Protocol;

namespace Neap.Core.Tests.Audio;

public class LoudnessBoostTests
{
    // The frames proven on hardware (SP2Tools, 2026-10-17). Relayed through the
    // base, byte 2 carries the relay bit 0x80; op is 0 to set, 1 to read; the
    // parameter is 00 04 00 00 and the value a big-endian float of decibels.
    private const string ReadThroughBase =
        "061080" + "055a0c00" + "0a00" + "0001" + "00040000" + "00000000";
    private const string SetMinusNineThroughBase =
        "061080" + "055a0c00" + "0a00" + "0000" + "00040000" + "c1100000";
    private const string RevertThroughBase =
        "061080" + "055a0c00" + "0a00" + "0000" + "00040000" + "c1400000";

    [Fact]
    public void ReadFrameThroughBaseMatchesTheProvenCapture() =>
        Assert.Equal(Convert.FromHexString(ReadThroughBase),
            LoudnessBoost.ReadFrame(relayThroughBase: true));

    [Fact]
    public void SetFrameThroughBaseMatchesTheProvenCapture() =>
        Assert.Equal(Convert.FromHexString(SetMinusNineThroughBase),
            LoudnessBoost.SetFrame(-9f, relayThroughBase: true));

    [Fact]
    public void RevertingToStockMatchesTheProvenCapture() =>
        Assert.Equal(Convert.FromHexString(RevertThroughBase),
            LoudnessBoost.SetFrame(LoudnessBoost.StockDb, relayThroughBase: true));

    [Fact]
    public void WiredFramesClearTheRelayBit()
    {
        byte[] wired = LoudnessBoost.ReadFrame(relayThroughBase: false);

        Assert.Equal(0x00, wired[2]);
        Assert.Equal(0x80, LoudnessBoost.ReadFrame(relayThroughBase: true)[2]);
    }

    [Fact]
    public void ValuesAreHeldToTheFirmwareRange()
    {
        Assert.Equal(LoudnessBoost.MaxDb, LoudnessBoost.ClampLevel(50f));
        Assert.Equal(LoudnessBoost.MinDb, LoudnessBoost.ClampLevel(-200f));
        Assert.Equal(0f, LoudnessBoost.LevelForBoost(LoudnessBoost.MaxBoostDb));
        Assert.Equal(LoudnessBoost.StockDb, LoudnessBoost.LevelForBoost(0f));
    }

    [Fact]
    public void ReadsTheLevelBackFromAReply()
    {
        Assert.Equal(-12f, LoudnessBoost.LevelInReply(Reply("c1400000")));
        Assert.Equal(-9f, LoudnessBoost.LevelInReply(Reply("c1100000")));
    }

    [Fact]
    public void RejectsARepliesThatAreNotThisCommandOrParameter()
    {
        // Wrong parameter echoed.
        Assert.Null(LoudnessBoost.LevelInReply(Reply("c1400000", param: "00050000")));
        // A value outside the firmware's range (here +48 dB).
        Assert.Null(LoudnessBoost.LevelInReply(Reply("42400000")));
        // A set_kvp-shaped report, not a RACE command reply.
        Assert.Null(LoudnessBoost.LevelInReply(Frames.SetKey(0x2A0, 50)));
    }

    /// <summary>A command reply report carrying one parameter and value.</summary>
    private static byte[] Reply(string valueHex, string param = "00040000")
    {
        string inner = "0a00" + param + valueHex;              // command id, then payload
        int innerBytes = inner.Length / 2;
        int total = 4 + innerBytes;
        string frame = "07"
            + ((byte)(total & 0xFF)).ToString("x2", System.Globalization.CultureInfo.InvariantCulture)
            + "80"                                             // relay bit, as the base sets it
            + "055b"                                           // RACE start, reply marker
            + ((byte)(innerBytes & 0xFF)).ToString("x2", System.Globalization.CultureInfo.InvariantCulture)
            + "00"
            + inner;
        var report = new byte[62];
        Convert.FromHexString(frame).CopyTo(report, 0);
        return report;
    }
}
