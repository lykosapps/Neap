using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class ToneSweepTests
{
    [Fact]
    public void ASweepCrossesTheRangeInItsTime() =>
        Assert.Equal(20000, ToneSweep.After(20, ToneSweep.Seconds), 3);

    [Fact]
    public void EveryDecadeTakesTheSameTime()
    {
        // 20 Hz to 20 kHz is three decades, so each takes a third.
        Assert.Equal(200, ToneSweep.After(20, ToneSweep.Seconds / 3), 3);
        Assert.Equal(2000, ToneSweep.After(200, ToneSweep.Seconds / 3), 3);
    }

    [Fact]
    public void ASweepStopsAtTheTop()
    {
        Assert.Equal(20000, ToneSweep.After(10000, ToneSweep.Seconds));
        Assert.True(ToneSweep.Finished(20000));
    }

    [Fact]
    public void ASweepCarriesOnFromWhereTheToneIs() =>
        Assert.Equal(4915, ToneSweep.Start(4915));

    [Fact]
    public void ASweepFromTheTopStartsAgainAtTheBottom() =>
        Assert.Equal(20, ToneSweep.Start(20000));

    [Fact]
    public void LevelZeroIsSilent() => Assert.Equal(0, ToneSweep.Amplitude(0));

    [Fact]
    public void TheLoudestLevelStaysWellBelowFullScale() =>
        Assert.Equal(0.25, ToneSweep.Amplitude(100), 6);

    [Fact]
    public void TheFirstLevelIsQuiet() =>
        Assert.True(ToneSweep.Amplitude(ToneSweep.FirstLevel) < 0.02);

    [Fact]
    public void ACutGoesDownAndABoostGoesUp()
    {
        var cut = ToneSweep.At(4915.4, boost: false);
        var boost = ToneSweep.At(4915.4, boost: true);
        Assert.Equal(4915, cut.Frequency);
        Assert.True(cut.Gain < 0);
        Assert.True(boost.Gain > 0);
        Assert.Equal(cut.Width, boost.Width);
    }
}
