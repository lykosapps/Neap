using Neap.Core.Connection;
using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class GainCorrectionSwitchTests
{
    /// <summary>A headset's record that can be made to fail in each of the ways a write can.</summary>
    private sealed class FakeHeadset
    {
        public GameEqRecord? Held { get; set; } = GameEqRecord.Shipped;
        public List<GameEqRecord> Backups { get; } = [];
        public int Writes { get; private set; }
        public bool ReadsNothing { get; set; }
        public bool Throws { get; set; }
        public bool IgnoresWrites { get; set; }
        public bool BackupFails { get; set; }
        public bool ThrowsOnlyOnce { get; set; }

        public GainCorrectionSwitch Switch => new(
            () => ReadsNothing ? null : Held,
            record =>
            {
                Writes++;
                if (Throws)
                {
                    if (ThrowsOnlyOnce) Throws = false;
                    throw new InvalidOperationException("lost");
                }

                if (!IgnoresWrites) Held = record;
            },
            record =>
            {
                if (BackupFails) throw new IOException("disk full");
                Backups.Add(record);
            },
            () => { });
    }

    [Fact]
    public void TheCorrectionIsClearedAndOnlyThatByteChanges()
    {
        var headset = new FakeHeadset();

        var result = headset.Switch.TurnOff();

        Assert.Equal(new SwitchResult(SwitchOutcome.Done), result);
        Assert.False(headset.Held!.GainCorrectionOn);
        Assert.True(headset.Held.Matches(GameEqRecord.Shipped.WithGainCorrection(false)));
    }

    [Fact]
    public void ACopyOfTheOriginalIsKeptBeforeAnythingIsWritten()
    {
        var headset = new FakeHeadset();
        headset.Switch.TurnOff();
        Assert.True(Assert.Single(headset.Backups).Matches(GameEqRecord.Shipped));
    }

    [Fact]
    public void NothingIsWrittenWhenTheCorrectionIsAlreadyOff()
    {
        var headset = new FakeHeadset { Held = GameEqRecord.Shipped.WithGainCorrection(false) };
        Assert.Equal(SwitchOutcome.AlreadyDone, headset.Switch.TurnOff().Outcome);
        Assert.Equal(0, headset.Writes);
        Assert.Empty(headset.Backups);
    }

    [Fact]
    public void NothingIsWrittenWhenTheRecordCannotBeRead()
    {
        var headset = new FakeHeadset { ReadsNothing = true };
        Assert.Equal(SwitchOutcome.CouldNotRead, headset.Switch.TurnOff().Outcome);
        Assert.Equal(0, headset.Writes);
    }

    [Fact]
    public void NothingIsWrittenWhenTheCopyCannotBeKept()
    {
        var headset = new FakeHeadset { BackupFails = true };
        Assert.Throws<IOException>(() => headset.Switch.TurnOff());
        Assert.Equal(0, headset.Writes);
        Assert.True(headset.Held!.GainCorrectionOn);
    }

    [Fact]
    public void AFailedWriteIsUndoneFromTheCopy()
    {
        var headset = new FakeHeadset { Throws = true, ThrowsOnlyOnce = true };

        var result = headset.Switch.TurnOff();

        Assert.Equal(new SwitchResult(SwitchOutcome.WriteFailed, BackupRestored: true), result);
        Assert.True(headset.Held!.Matches(GameEqRecord.Shipped));
    }

    [Fact]
    public void AWriteThatNeverReachesTheHeadsetIsNotConfirmed()
    {
        var headset = new FakeHeadset { IgnoresWrites = true };

        var result = headset.Switch.TurnOff();

        Assert.Equal(SwitchOutcome.NotConfirmed, result.Outcome);
        Assert.True(result.BackupRestored);
        Assert.True(headset.Held!.Matches(GameEqRecord.Shipped));
    }

    [Fact]
    public void WhenEvenThePuttingBackFailsItIsSaid()
    {
        var headset = new FakeHeadset { Throws = true };
        Assert.Equal(new SwitchResult(SwitchOutcome.WriteFailed, BackupRestored: false), headset.Switch.TurnOff());
    }

    [Fact]
    public void ReturningToStockPutsTheBandsAndTheCorrectionBackAndKeepsTheGains()
    {
        var moved = GameEqRecord.Shipped.WithBands(
        [
            .. GameEqRecord.Shipped.Bands.Select((band, i) => i == 5
                ? new ParametricBand(BandType.LowShelf, 3000, 0.7, 4, true)
                : band with { Gain = i == 2 ? -2.5 : 0 }),
        ]).WithGainCorrection(false);
        var headset = new FakeHeadset { Held = moved };

        var result = headset.Switch.ReturnToStock();

        Assert.Equal(SwitchOutcome.Done, result.Outcome);
        Assert.True(headset.Held!.GainCorrectionOn);
        Assert.True(StockBands.Match(headset.Held.Bands));
        Assert.Equal(-2.5, headset.Held.Bands[2].Gain);
        Assert.Equal(4, headset.Held.Bands[5].Gain);
        Assert.True(headset.Backups.Single().Matches(moved));
    }

    [Fact]
    public void ReturningToStockWritesNothingWhenItIsAlreadyStock()
    {
        var headset = new FakeHeadset();
        Assert.Equal(SwitchOutcome.AlreadyDone, headset.Switch.ReturnToStock().Outcome);
        Assert.Equal(0, headset.Writes);
    }

    [Fact]
    public void ARecordEditedWithBandsKeepsEveryOtherByte()
    {
        var edited = GameEqRecord.Shipped.WithBands([.. GameEqRecord.Shipped.Bands.Select(band => band with { Gain = 1.5 })]);
        var before = GameEqRecord.Shipped.Bytes.ToArray();
        var after = edited.Bytes.ToArray();

        // Only the gain floats (offset 8 of each 18-byte band, from byte 4) differ.
        var differing = Enumerable.Range(0, GameEqRecord.Length).Where(i => before[i] != after[i]).ToArray();
        Assert.All(differing, i => Assert.InRange((i - 4) % 18, 8, 11));
        Assert.Equal(GameEqRecord.Shipped.GainCorrectionOn, edited.GainCorrectionOn);
    }

    [Theory]
    [InlineData(9, false, false)]
    [InlineData(9, true, true)]
    [InlineData(10, false, true)]
    [InlineData(80, false, true)]
    public void ALowBatteryStopsTheWriteUnlessItIsCharging(int percent, bool charging, bool allowed) =>
        Assert.Equal(allowed, GainCorrectionSwitch.BatteryAllows(new BatteryReading(percent, charging)));

    [Fact]
    public void ABatteryNotYetReadDoesNotStopIt() =>
        Assert.True(GainCorrectionSwitch.BatteryAllows(null));
}
