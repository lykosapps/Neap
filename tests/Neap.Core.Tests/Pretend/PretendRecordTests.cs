using Neap.Core.Presets;
using Neap.Core.Pretend;

namespace Neap.Core.Tests.Pretend;

public class PretendRecordTests
{
    private static HeadsetClient Wired(PretendHeadset headset, bool allowWrites = true) =>
        new(allowWrites, headset.Open());

    [Fact]
    public void TheShippedRecordIsReadOverTheCable()
    {
        var headset = new PretendHeadset(wired: true);
        using var client = Wired(headset);

        var record = client.ReadGameEqRecord();

        Assert.NotNull(record);
        Assert.True(record.Matches(GameEqRecord.Shipped));
        Assert.True(record.GainCorrectionOn);
        Assert.Null(client.UnreadableRecord);
    }

    [Fact]
    public void ARecordNeapDoesNotUnderstandIsToldFromNoReply()
    {
        var unknown = GameEqRecord.Shipped.Bytes.ToArray();
        unknown[184] = 2;
        var headset = new PretendHeadset(wired: true) { UnknownRecord = unknown };
        using var client = Wired(headset);

        Assert.Null(client.ReadGameEqRecord());
        Assert.Equal(Convert.ToHexString(unknown), client.UnreadableRecord);
    }

    [Fact]
    public void AClearedRecordIsWrittenInSeveralReportsAndReadBack()
    {
        var headset = new PretendHeadset(wired: true);
        using var client = Wired(headset);
        var attempts = new List<WriteAttempt>();
        client.Wrote += attempts.Add;
        var cleared = GameEqRecord.Shipped.WithGainCorrection(false);

        client.WriteGameEqRecord(cleared);

        Assert.False(headset.Record.GainCorrectionOn);
        Assert.True(headset.Record.Matches(cleared));
        Assert.True(client.ReadGameEqRecord()!.Matches(cleared));
        var attempt = Assert.Single(attempts);
        Assert.Null(attempt.Refused);
        Assert.Equal(HeadsetClient.GameEqRecordName, attempt.Name);
        Assert.Equal("gain correction off", attempt.Value);
    }

    [Fact]
    public void TheOriginalCanBeWrittenBack()
    {
        var headset = new PretendHeadset(wired: true);
        using var client = Wired(headset);
        client.WriteGameEqRecord(GameEqRecord.Shipped.WithGainCorrection(false));

        client.WriteGameEqRecord(GameEqRecord.Shipped);

        Assert.True(headset.Record.Matches(GameEqRecord.Shipped));
    }

    [Fact]
    public void ThroughADockTheRecordIsReadAndWrittenToo()
    {
        var headset = new PretendHeadset();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());
        var cleared = GameEqRecord.Shipped.WithGainCorrection(false);

        Assert.True(client.ReadGameEqRecord()!.Matches(GameEqRecord.Shipped));
        client.WriteGameEqRecord(cleared);

        Assert.True(headset.Record.Matches(cleared));
        Assert.True(client.ReadGameEqRecord()!.Matches(cleared));
    }

    [Fact]
    public void ARecordIsNotWrittenThroughAReadOnlyClient()
    {
        var headset = new PretendHeadset(wired: true);
        using var client = Wired(headset, allowWrites: false);

        Assert.Throws<WritesDisabledException>(() => client.WriteGameEqRecord(GameEqRecord.Shipped.WithGainCorrection(false)));
        Assert.True(headset.Record.Matches(GameEqRecord.Shipped));
    }

    [Fact]
    public void ARecordIsNotSentToAnAtlasAir()
    {
        var headset = new PretendHeadset(atlas: true);
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());

        Assert.Throws<InvalidOperationException>(() => client.WriteGameEqRecord(GameEqRecord.Shipped.WithGainCorrection(false)));
    }
}
