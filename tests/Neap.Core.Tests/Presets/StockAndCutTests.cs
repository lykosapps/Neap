using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class StockAndCutTests
{
    [Fact]
    public void TheStockBandsAreTheTenOctaveBellsAtTheHeadsetsFrequencies()
    {
        Assert.Equal(
            [31.5, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000],
            StockBands.Game.Select(band => band.Hertz));
        Assert.All(StockBands.Game, band =>
        {
            Assert.Equal(BandType.Peaking, band.Type);
            Assert.Equal(StockBands.Q, band.Q);
            Assert.Equal(0, band.Gain);
        });
    }

    [Fact]
    public void BandsMatchStockWhateverTheirGains()
    {
        var bands = StockBands.Game.Select(band => band with { Gain = 4 }).ToArray();
        Assert.True(StockBands.Match(bands));
    }

    [Fact]
    public void AMovedOrReshapedBandIsNotStock()
    {
        var moved = StockBands.Game.ToArray();
        moved[5] = moved[5] with { Hertz = 1200 };
        var reshaped = StockBands.Game.ToArray();
        reshaped[2] = reshaped[2] with { Type = BandType.LowShelf };
        var wider = StockBands.Game.ToArray();
        wider[0] = wider[0] with { Q = 0.7 };

        Assert.False(StockBands.Match(moved));
        Assert.False(StockBands.Match(reshaped));
        Assert.False(StockBands.Match(wider));
        Assert.False(StockBands.Match([.. StockBands.Game.Take(9)]));
    }

    [Theory]
    [InlineData(CutSlope.Gentle, 1)]
    [InlineData(CutSlope.Standard, 1)]
    [InlineData(CutSlope.Steep, 2)]
    [InlineData(CutSlope.Steepest, 3)]
    public void ASlopeUsesAsManyBandsAsItNeeds(CutSlope slope, int expected)
    {
        Assert.Equal(expected, CutBands.BandsFor(slope));
        Assert.Equal(expected, CutBands.Bands(true, 80, slope).Count);
    }

    [Fact]
    public void StackedStagesUseTheirOwnWidthsAtOneFrequency()
    {
        var steep = CutBands.Bands(lowCut: true, 80, CutSlope.Steep);
        Assert.Equal([0.5412, 1.3066], steep.Select(band => band.Q));
        var steepest = CutBands.Bands(lowCut: false, 9000, CutSlope.Steepest);
        Assert.Equal([0.5176, 0.7071, 1.9319], steepest.Select(band => band.Q));
        Assert.All(steepest, band =>
        {
            Assert.Equal(9000, band.Hertz);
            Assert.Equal(BandType.HighCut, band.Type);
            Assert.True(band.InUse);
        });
    }

    [Fact]
    public void ALowCutRemovesWhatIsBelowAndAHighCutWhatIsAbove()
    {
        Assert.Equal(BandType.LowCut, CutBands.Bands(true, 80, CutSlope.Standard)[0].Type);
        Assert.Equal(BandType.HighCut, CutBands.Bands(false, 8000, CutSlope.Standard)[0].Type);
    }

    [Fact]
    public void AGentleCutUsesTheSixDecibelFilter()
    {
        Assert.Equal(BandType.GentleLowCut, CutBands.Bands(true, 80, CutSlope.Gentle)[0].Type);
        Assert.Equal(BandType.GentleHighCut, CutBands.Bands(false, 8000, CutSlope.Gentle)[0].Type);
    }

    [Fact]
    public void ASlopeThatDoesNotExistIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CutBands.Bands(true, 80, (CutSlope)7));
}
