using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class StockWithGainsTests
{
    private static readonly int[] MudCut = [0, -10, -25, 5, 0, 0, 0, -10, 0, 30];

    [Fact]
    public void ThePresetsGainsAreOnTheStockBandsAndAllInUse()
    {
        var bands = StockBands.WithGains(MudCut);

        Assert.True(StockBands.Match(bands));
        Assert.Equal([0, -1, -2.5, 0.5, 0, 0, 0, -1, 0, 3], bands.Select(band => band.Gain));
        Assert.All(bands, band => Assert.True(band.InUse));
    }

    [Fact]
    public void TheFlatShapeSentWholeBypassesEveryBand()
    {
        var writes = BandWrites.Plan(null, StockBands.Flat);

        Assert.All(Enumerable.Range(0, BandWrites.Bands), band =>
        {
            Assert.Contains(new BandWrite(band, BandParameter.Type, (double)BandType.Bypass), writes);
            Assert.Contains(new BandWrite(band, BandParameter.Gain, 0), writes);
        });
    }

    [Fact]
    public void AGainPastTheLimitIsHeld() =>
        Assert.Equal(ParametricBand.MostGain, StockBands.WithGains([0, 0, 0, 0, 0, 0, 0, 0, 0, 200])[9].Gain);

    [Fact]
    public void AnythingButTenGainsIsRefused() =>
        Assert.Throws<ArgumentException>(() => StockBands.WithGains([0, 1]));
}
