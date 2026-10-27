using System.Buffers.Binary;
using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class GameEqRecordTests
{
    /// <summary>The first band as SP2Tools read it from one headset: 31.5 Hz, Q 1.4142, no gain, 96000, filter 4.</summary>
    private const string FirstBandAsRead = "0000fc41" + "8104b53f" + "00000000" + "0080bb47" + "0400";

    /// <summary>A record made the way the headset holds it: flat octave bells, the gain correction on.</summary>
    internal static byte[] Record(bool correction = true, ushort type = 4)
    {
        var bytes = new byte[GameEqRecord.Length];
        GameEqRecord.Header.CopyTo(bytes);
        for (int band = 0; band < 10; band++)
        {
            var at = bytes.AsSpan(4 + (band * 18), 18);
            BinaryPrimitives.WriteSingleLittleEndian(at, (float)StockBands.Game[band].Hertz);
            BinaryPrimitives.WriteSingleLittleEndian(at[4..], (float)StockBands.Q);
            BinaryPrimitives.WriteSingleLittleEndian(at[8..], 0f);
            BinaryPrimitives.WriteSingleLittleEndian(at[12..], 96000f);
            BinaryPrimitives.WriteUInt16LittleEndian(at[16..], type);
        }

        bytes[184] = correction ? (byte)1 : (byte)0;
        return bytes;
    }

    [Fact]
    public void TheBandLayoutIsTheOneSp2ToolsReadFromTheHeadset()
    {
        var bytes = Record();
        Convert.FromHexString(FirstBandAsRead).CopyTo(bytes.AsSpan(4));
        var first = GameEqRecord.Parse(bytes)!.Bands[0];

        Assert.Equal(new ParametricBand(BandType.Peaking, 31.5, 1.4142, 0, true).Hertz, first.Hertz);
        Assert.Equal(1.4142, first.Q, 4);
        Assert.Equal(0, first.Gain);
        Assert.Equal(BandType.Peaking, first.Type);
    }

    [Fact]
    public void ARecordHoldsTenBandsAndTheFlagSitsAfterThem()
    {
        var record = GameEqRecord.Parse(Record())!;
        Assert.Equal(10, record.Bands.Count);
        Assert.True(record.GainCorrectionOn);
        Assert.False(GameEqRecord.Parse(Record(correction: false))!.GainCorrectionOn);
    }

    [Fact]
    public void TheShippedRecordMatchesTheStockTable() =>
        Assert.True(StockBands.Match(GameEqRecord.Parse(Record())!.Bands));

    [Fact]
    public void ClearingTheCorrectionChangesThatByteAndNoOther()
    {
        var shipped = GameEqRecord.Parse(Record())!;
        var cleared = shipped.WithGainCorrection(false);

        Assert.False(cleared.GainCorrectionOn);
        var differing = Enumerable.Range(0, GameEqRecord.Length).Where(i => shipped.Bytes[i] != cleared.Bytes[i]).ToArray();
        Assert.Equal([184], differing);
        Assert.True(shipped.WithGainCorrection(false).WithGainCorrection(true).Matches(shipped));
    }

    [Fact]
    public void TheOriginalIsNeverChangedByAnEdit()
    {
        var shipped = GameEqRecord.Parse(Record())!;
        shipped.WithGainCorrection(false);
        Assert.True(shipped.GainCorrectionOn);
    }

    [Fact]
    public void AnythingThatDoesNotFitIsNotARecord()
    {
        Assert.Null(GameEqRecord.Parse(Record().AsSpan(..185)));
        Assert.Null(GameEqRecord.Parse([.. Record(), 0]));

        var badHeader = Record();
        badHeader[0] = 9;
        Assert.Null(GameEqRecord.Parse(badHeader));

        Assert.Null(GameEqRecord.Parse(Record(type: 11)));

        var badFlag = Record();
        badFlag[184] = 2;
        Assert.Null(GameEqRecord.Parse(badFlag));

        var notANumber = Record();
        BinaryPrimitives.WriteSingleLittleEndian(notANumber.AsSpan(4), float.NaN);
        Assert.Null(GameEqRecord.Parse(notANumber));
    }
}
