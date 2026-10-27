using Neap.Core.Audio;
using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class LevelHeadroomTests
{
    private static ParametricBand[] WithBoost(double gain)
    {
        ParametricBand[] bands = [.. StockBands.Game];
        bands[5] = bands[5] with { Gain = gain };
        return bands;
    }

    [Fact]
    public void AtStockABoostOfTwoDecibelsJustReachesTheCompressor()
    {
        Assert.False(LevelHeadroom.Squashes(LoudnessBoost.StockDb, WithBoost(2)));
        Assert.True(LevelHeadroom.Squashes(LoudnessBoost.StockDb, WithBoost(3)));
    }

    [Fact]
    public void FlatBandsNeverReachItFromStockOrBelow()
    {
        Assert.False(LevelHeadroom.Squashes(LoudnessBoost.StockDb, [.. StockBands.Game]));
        Assert.Equal(0, LevelHeadroom.LowerBy(LoudnessBoost.StockDb, [.. StockBands.Game]));
    }

    [Fact]
    public void ALevelRaisedAboveStockReachesItWithNoBoostAtAll()
    {
        double level = LoudnessBoost.LevelForBoost(6);
        Assert.True(LevelHeadroom.Squashes(level, [.. StockBands.Game]));
        Assert.Equal(4, LevelHeadroom.LowerBy(level, [.. StockBands.Game]));
    }

    [Fact]
    public void TheSuggestedLoweringBringsAPeakBackToTheThreshold()
    {
        var bands = WithBoost(4.5);
        double lower = LevelHeadroom.LowerBy(LoudnessBoost.StockDb, bands);
        Assert.Equal(2.5, lower);
        Assert.False(LevelHeadroom.Squashes(LoudnessBoost.StockDb - lower, bands));
    }

    [Fact]
    public void OnlyABandThatIsOnAndHasAGainCounts()
    {
        var bands = WithBoost(9);
        bands[5] = bands[5] with { InUse = false };
        bands[2] = new ParametricBand(BandType.LowCut, 80, 0.7, 9, true);
        Assert.Equal(LoudnessBoost.StockDb, LevelHeadroom.PeakDb(LoudnessBoost.StockDb, bands));
    }

    [Fact]
    public void ACutNeverRaisesThePeak()
    {
        var bands = WithBoost(-6);
        Assert.Equal(LoudnessBoost.StockDb, LevelHeadroom.PeakDb(LoudnessBoost.StockDb, bands));
    }

    [Fact]
    public void TheLoweringNeverTakesTheLevelBelowWhatTheLoudnessControlOffers()
    {
        // Lowering always lands the peak on the threshold, so the level ends at the threshold minus the boost: -22 dB at most.
        var bands = WithBoost(ParametricBand.MostGain);
        double after = LoudnessBoost.StockDb - LevelHeadroom.LowerBy(LoudnessBoost.StockDb, bands);
        Assert.Equal(-22, after);
        Assert.True(after >= LoudnessBoost.StockDb + LoudnessBoost.MinBoostDb);
    }
}
