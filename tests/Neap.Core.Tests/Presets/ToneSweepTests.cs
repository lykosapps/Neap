using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class ToneSweepTests
{
    private static readonly double Top = ParametricBand.HighestHertz;

    [Fact]
    public void ASweepCrossesTheRangeInItsTime() =>
        Assert.Equal(Top, ToneSweep.After(20, ToneSweep.Seconds), 3);

    [Fact]
    public void EveryThirdOfTheTimeCoversTheSameRatio()
    {
        // The range is a fixed ratio, so a third of the time covers a third of it, on the logarithmic scale.
        double third = Math.Cbrt(Top / 20);
        Assert.Equal(20 * third, ToneSweep.After(20, ToneSweep.Seconds / 3), 3);
        Assert.Equal(20 * third * third, ToneSweep.After(20 * third, ToneSweep.Seconds / 3), 3);
    }

    [Fact]
    public void ASweepStopsAtTheTop()
    {
        Assert.Equal(Top, ToneSweep.After(10000, ToneSweep.Seconds));
        Assert.True(ToneSweep.Finished(Top));
    }

    [Fact]
    public void ASweepCarriesOnFromWhereTheToneIs() =>
        Assert.Equal(4915, ToneSweep.Start(4915));

    [Fact]
    public void ASweepFromTheTopStartsAgainAtTheBottom() =>
        Assert.Equal(20, ToneSweep.Start(Top));

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
        var cut = ToneSweep.Bell(4915.4, boost: false);
        var boost = ToneSweep.Bell(4915.4, boost: true);
        Assert.Equal(4915, cut.Hertz);
        Assert.Equal(-3, cut.Gain);
        Assert.Equal(3, boost.Gain);
        Assert.Equal(cut.Q, boost.Q);
        Assert.True(cut.InUse && boost.InUse);
    }

    [Fact]
    public void ASliderPutsTheToneOnALogarithmicScaleAndTheRoundTripHolds()
    {
        Assert.Equal(20, ToneSweep.Hertz(0, 2000), 6);
        Assert.Equal(ParametricBand.HighestHertz, ToneSweep.Hertz(2000, 2000), 6);
        Assert.Equal(ToneSweep.Hertz(1000, 2000), Math.Sqrt(20 * ParametricBand.HighestHertz), 3);
        Assert.Equal(1234, ToneSweep.Position(ToneSweep.Hertz(1234, 2000), 2000), 6);
    }
}
