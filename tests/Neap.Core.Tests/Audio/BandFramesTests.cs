using Neap.Core.Audio;
using Neap.Core.Presets;

namespace Neap.Core.Tests.Audio;

public class BandFramesTests
{
    // From SP2Tools' handover: band 5 is 0-based, so the 1000 Hz band. Its frequency is
    // id 0x0007015B to the sound now, and 0x00070153 to read; 1200 Hz is the float 0x44960000.
    private const string SetBand5To1200Hz = "061000" + "055a0c00" + "0a00" + "0000" + "0007015b" + "44960000";
    private const string SetBand5ToPeaking = "061000" + "055a0c00" + "0a00" + "0000" + "0007015f" + "00000004";
    private const string ReadBand5Frequency = "061000" + "055a0c00" + "0a00" + "0001" + "00070153" + "00000000";

    [Fact]
    public void AFrequencyIsSetAsAFloatAtTheHandoversId() =>
        Assert.Equal(Convert.FromHexString(SetBand5To1200Hz),
            BandFrames.SetFrame(new BandWrite(5, BandParameter.Frequency, 1200), relayThroughBase: false));

    [Fact]
    public void AFilterIsSetAsAnInteger() =>
        Assert.Equal(Convert.FromHexString(SetBand5ToPeaking),
            BandFrames.SetFrame(new BandWrite(5, BandParameter.Type, (double)BandType.Peaking), relayThroughBase: false));

    [Fact]
    public void AReadLeavesBitThreeOffTheId() =>
        Assert.Equal(Convert.FromHexString(ReadBand5Frequency),
            BandFrames.ReadFrame(5, BandParameter.Frequency, relayThroughBase: false));

    [Fact]
    public void ThroughTheDockTheRelayBitIsSet() =>
        Assert.Equal(0x80, BandFrames.SetFrame(new BandWrite(0, BandParameter.Gain, 3), relayThroughBase: true)[2]);

    [Theory]
    [InlineData(BandParameter.Frequency, "44960000", 1200.0)]
    [InlineData(BandParameter.Q, "3fb504f3", 1.4142135)]
    [InlineData(BandParameter.Gain, "c0400000", -3.0)]
    [InlineData(BandParameter.Type, "00000004", 4.0)]
    public void AReplyGivesTheValueBack(BandParameter parameter, string valueHex, double expected)
    {
        double? value = BandFrames.ValueInReply(Reply(Id(5, parameter), valueHex), 5, parameter);
        Assert.Equal(expected, value!.Value, 5);
    }

    [Theory]
    [InlineData(BandParameter.Frequency, "47c35000")] // 100000 Hz
    [InlineData(BandParameter.Frequency, "40a00000")] // 5 Hz
    [InlineData(BandParameter.Q, "41300000")] // 11
    [InlineData(BandParameter.Gain, "41c80000")] // +25 dB
    [InlineData(BandParameter.Gain, "7fc00000")] // not a number
    [InlineData(BandParameter.Type, "0000000b")] // 11
    public void AValueOutsideTheFirmwaresRangeIsNotAReading(BandParameter parameter, string valueHex) =>
        Assert.Null(BandFrames.ValueInReply(Reply(Id(5, parameter), valueHex), 5, parameter));

    [Fact]
    public void AReplyForAnotherBandOrParameterIsNotThisReading()
    {
        Assert.Null(BandFrames.ValueInReply(Reply(Id(4, BandParameter.Frequency), "44960000"), 5, BandParameter.Frequency));
        Assert.Null(BandFrames.ValueInReply(Reply(Id(5, BandParameter.Gain), "44960000"), 5, BandParameter.Frequency));
    }

    [Fact]
    public void TheIdsParseBackWithOrWithoutBitThree()
    {
        Assert.True(BandWrites.TryParse(0x0007015B, out int band, out var parameter));
        Assert.Equal((5, BandParameter.Frequency), (band, parameter));
        Assert.True(BandWrites.TryParse(0x00070153, out band, out parameter));
        Assert.Equal((5, BandParameter.Frequency), (band, parameter));
    }

    [Theory]
    [InlineData(0x00040000u)] // the input level
    [InlineData(0x00070300u)] // another bank
    [InlineData(0x000701A3u)] // band 10 does not exist
    [InlineData(0x00070151u)] // parameter 1 is not one
    public void AnIdThatIsNotAGameBandIsNotParsed(uint id) =>
        Assert.False(BandWrites.TryParse(id, out _, out _));

    private static uint Id(int band, BandParameter parameter) => BandWrites.Id(band, parameter, toSoundNow: false);

    /// <summary>A command reply report carrying one parameter and value.</summary>
    private static byte[] Reply(uint id, string valueHex)
    {
        string inner = "0a00" + id.ToString("x8", System.Globalization.CultureInfo.InvariantCulture) + valueHex;
        int innerBytes = inner.Length / 2;
        int total = 4 + innerBytes;
        string frame = "07"
            + ((byte)(total & 0xFF)).ToString("x2", System.Globalization.CultureInfo.InvariantCulture)
            + "80"
            + "055b"
            + ((byte)(innerBytes & 0xFF)).ToString("x2", System.Globalization.CultureInfo.InvariantCulture)
            + "00"
            + inner;
        var report = new byte[62];
        Convert.FromHexString(frame).CopyTo(report, 0);
        return report;
    }
}
