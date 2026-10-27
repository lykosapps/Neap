using Neap.Core.Presets;
using Neap.Core.Protocol;
using Neap.Core.Tests.Presets;

namespace Neap.Core.Tests.Protocol;

public class NvKeyTests
{
    [Fact]
    public void AReadIsOneReportThatMatchesSp2ToolsFrame()
    {
        // From SP2Tools' handover: 05 5A 06 00 | 00 0A | 0B 50 | BA 00, key 0x500B and length 186.
        var reports = NvKey.ReadReports(GameEqRecord.Key, GameEqRecord.Length, relayThroughBase: false);

        var report = Assert.Single(reports);
        Assert.Equal(62, report.Length);
        Assert.Equal(Convert.FromHexString("06" + "0a00" + "055a0600" + "000a" + "0b50" + "ba00"), report[..13]);
        Assert.All(report[13..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void AWriteIsCutIntoReportsOfAtMost59BytesOfTheFrame()
    {
        var reports = NvKey.WriteReports(GameEqRecord.Key, GameEqRecord.Parse(GameEqRecordTests.Record())!.Bytes, relayThroughBase: false);

        // The frame is 6 header bytes, the key and 186 record bytes: 194 in all.
        Assert.Equal([59, 59, 59, 17], reports.Select(r => (int)r[1]));
        Assert.All(reports, r => { Assert.Equal(62, r.Length); Assert.Equal(0x06, r[0]); Assert.Equal(0x00, r[2]); });
        Assert.Equal(Convert.FromHexString("055abe00" + "010a" + "0b50"), reports[0][3..11]);
    }

    [Fact]
    public void TheReportsCarryTheRecordWholeAndInOrder()
    {
        var data = GameEqRecordTests.Record();
        var reports = NvKey.WriteReports(GameEqRecord.Key, data, relayThroughBase: false);

        var frame = reports.SelectMany(r => r.Skip(3).Take(r[1])).ToArray();
        Assert.Equal(data, frame[8..]);
    }

    [Fact]
    public void ThroughTheDockTheRelayBitIsSetOnEveryReport() =>
        Assert.All(NvKey.WriteReports(GameEqRecord.Key, new byte[186], relayThroughBase: true), r => Assert.Equal(0x80, r[2]));

    [Fact]
    public void TheRecordIsFoundInReportsBetweenNotificationsThatAreNotForIt()
    {
        var record = GameEqRecordTests.Record();
        var reader = new NvKeyReader(GameEqRecord.Length);
        reader.Add(Input([0x05, 0x5B, 0x01, 0x00, 0xAA]));                  // a notification before
        foreach (var piece in Pieces(new byte[] { 0xBA, 0x00 }.Concat(record).ToArray())) reader.Add(Input(piece));
        reader.Add(Input([0x05, 0x5B, 0x02, 0x00, 0x11, 0x22]));            // and another after

        Assert.Equal(record, reader.Record(GameEqRecord.Header));
    }

    [Fact]
    public void NothingIsFoundUntilAllOfTheRecordHasArrived()
    {
        var record = GameEqRecordTests.Record();
        var reader = new NvKeyReader(GameEqRecord.Length);
        var pieces = Pieces(new byte[] { 0xBA, 0x00 }.Concat(record).ToArray());
        foreach (var piece in pieces.Take(pieces.Count - 1)) reader.Add(Input(piece));

        Assert.Null(reader.Record(GameEqRecord.Header));

        reader.Add(Input(pieces[^1]));
        Assert.Equal(record, reader.Record(GameEqRecord.Header));
    }

    [Fact]
    public void ReportsThatAreNotInputReportsAreIgnored()
    {
        var reader = new NvKeyReader(GameEqRecord.Length);
        reader.Add([0x06, 0x03, 0x00, 0xBA, 0x00, 0x0A]);
        reader.Add([]);
        Assert.Null(reader.Record(GameEqRecord.Header));
    }

    /// <summary>A reply's bytes cut into the pieces an input report carries, at most 59 each.</summary>
    private static List<byte[]> Pieces(byte[] reply) =>
        [.. reply.Chunk(59)];

    /// <summary>An input report carrying some stream bytes, as the headset sends them.</summary>
    private static byte[] Input(byte[] data)
    {
        var report = new byte[62];
        report[0] = 0x07;
        report[1] = (byte)data.Length;
        data.CopyTo(report, 3);
        return report;
    }
}
