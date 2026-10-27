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
    public void TheFirstParametricShapeUsesOnlyTheBandsThatHaveAGain()
    {
        var bands = StockBands.FromGains(MudCut);

        Assert.Equal([false, true, true, true, false, false, false, true, false, true], bands.Select(band => band.InUse));
        Assert.Equal(-2.5, bands[2].Gain);
    }

    [Fact]
    public void AGainPastTheLimitIsHeld() =>
        Assert.Equal(ParametricBand.MostGain, StockBands.WithGains([0, 0, 0, 0, 0, 0, 0, 0, 0, 200])[9].Gain);

    [Fact]
    public void AnythingButTenGainsIsRefused() =>
        Assert.Throws<ArgumentException>(() => StockBands.WithGains([0, 1]));
}
