using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class BandWritesTests
{
    private static ParametricBand Bell(double hertz, double gain = 0, bool inUse = true) =>
        new(BandType.Peaking, hertz, 1, gain, inUse);

    private static ParametricBand[] Flat() =>
        [.. new double[] { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 }.Select(hertz => Bell(hertz))];

    [Fact]
    public void TheIdsMatchTheHeadsetsOwnExample()
    {
        // From SP2Tools' handover: band 5's frequency is 0x0007015B, and a read leaves bit 3 off.
        Assert.Equal(0x0007015Bu, BandWrites.Id(5, BandParameter.Frequency));
        Assert.Equal(0x00070153u, BandWrites.Id(5, BandParameter.Frequency, toSoundNow: false));
        Assert.Equal(0x0007010Du, BandWrites.Id(0, BandParameter.Gain));
        Assert.Equal(0x0007019Fu, BandWrites.Id(9, BandParameter.Type));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void ABandThatDoesNotExistHasNoId(int band) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => BandWrites.Id(band, BandParameter.Gain));

    [Fact]
    public void NothingIsSentWhenNothingChanged() =>
        Assert.Empty(BandWrites.Plan(Flat(), Flat()));

    [Fact]
    public void EverythingIsSentWhenWhatTheHeadsetHoldsIsNotKnown() =>
        Assert.Equal(40, BandWrites.Plan(null, Flat()).Count);

    [Fact]
    public void OnlyTheChangedParameterIsSent()
    {
        var wanted = Flat();
        wanted[5] = wanted[5] with { Gain = 4 };
        var writes = BandWrites.Plan(Flat(), wanted);
        Assert.Equal([new BandWrite(5, BandParameter.Gain, 4)], writes);
    }

    [Fact]
    public void GainGoesBeforeFrequencyForOneBand()
    {
        var wanted = Flat();
        wanted[5] = wanted[5] with { Gain = 4, Hertz = 3000 };
        var writes = BandWrites.Plan(Flat(), wanted);
        Assert.Equal([BandParameter.Gain, BandParameter.Frequency], writes.Select(write => write.Parameter));
    }

    [Fact]
    public void TheFilterAndWidthGoBeforeTheGain()
    {
        var wanted = Flat();
        wanted[2] = new ParametricBand(BandType.LowShelf, 125, 0.7, 4, true);
        var writes = BandWrites.Plan(Flat(), wanted);
        Assert.Equal(
            [BandParameter.Type, BandParameter.Q, BandParameter.Gain],
            writes.Select(write => write.Parameter));
    }

    [Fact]
    public void ABandSwitchedOffIsSentAsBypassWithNoGain()
    {
        var on = Flat();
        on[3] = on[3] with { Gain = 5 };
        var off = Flat();
        off[3] = on[3] with { InUse = false };
        var writes = BandWrites.Plan(on, off);
        Assert.Equal(
            [new BandWrite(3, BandParameter.Type, (double)BandType.Bypass), new BandWrite(3, BandParameter.Gain, 0)],
            writes);
    }

    [Fact]
    public void ABandSwitchedBackOnSendsItsOwnValuesAgain()
    {
        var off = Flat();
        off[3] = off[3] with { Gain = 5, InUse = false };
        var on = Flat();
        on[3] = off[3] with { InUse = true };
        var writes = BandWrites.Plan(off, on);
        Assert.Equal(
            [new BandWrite(3, BandParameter.Type, (double)BandType.Peaking), new BandWrite(3, BandParameter.Gain, 5)],
            writes);
    }

    [Fact]
    public void AFrequencyIsHeldInsideTheLimitBeforeItIsCompared()
    {
        var wanted = Flat();
        wanted[9] = wanted[9] with { Hertz = 30000 };
        var writes = BandWrites.Plan(Flat(), wanted);
        Assert.Equal(new BandWrite(9, BandParameter.Frequency, (float)ParametricBand.HighestHertz), Assert.Single(writes));
    }

    [Theory]
    [InlineData(BandParameter.Type, 0)]
    [InlineData(BandParameter.Type, 10)]
    [InlineData(BandParameter.Frequency, ParametricBand.LowestHertz)]
    [InlineData(BandParameter.Frequency, ParametricBand.HighestHertz)]
    [InlineData(BandParameter.Q, ParametricBand.LowestQ)]
    [InlineData(BandParameter.Q, ParametricBand.HighestQ)]
    [InlineData(BandParameter.Gain, -ParametricBand.MostGain)]
    [InlineData(BandParameter.Gain, ParametricBand.MostGain)]
    public void AWriteInsideABandsLimitsMaySend(BandParameter parameter, double value) =>
        Assert.True(BandWrites.InRange(new BandWrite(0, parameter, value)));

    [Theory]
    [InlineData(BandParameter.Type, 3)]    // band-pass, never sent
    [InlineData(BandParameter.Type, 8)]    // all-pass, never sent
    [InlineData(BandParameter.Type, 11)]
    [InlineData(BandParameter.Type, 4.5)]
    [InlineData(BandParameter.Frequency, 19)]
    [InlineData(BandParameter.Frequency, 19501)]
    [InlineData(BandParameter.Q, 0.05)]
    [InlineData(BandParameter.Q, 10.5)]
    [InlineData(BandParameter.Gain, 12.5)]
    [InlineData(BandParameter.Gain, -13)]
    [InlineData(BandParameter.Gain, double.NaN)]
    public void AWriteOutsideABandsLimitsMayNot(BandParameter parameter, double value) =>
        Assert.False(BandWrites.InRange(new BandWrite(0, parameter, value)));

    [Fact]
    public void EveryPlannedWriteIsInsideABandsLimits()
    {
        var wild = Enumerable.Range(0, BandWrites.Bands)
            .Select(band => new ParametricBand(BandType.LowShelf, band % 2 == 0 ? 1 : 99999, band % 2 == 0 ? 0.01 : 50, band % 2 == 0 ? -40 : 40, true))
            .ToArray();

        Assert.All(BandWrites.Plan(null, wild), write => Assert.True(BandWrites.InRange(write), write.ToString()));
    }

    [Fact]
    public void AnythingButTenBandsIsRefused()
    {
        Assert.Throws<ArgumentException>(() => BandWrites.Plan(null, Flat()[..9]));
        Assert.Throws<ArgumentException>(() => BandWrites.Plan(Flat()[..9], Flat()));
    }
}
