using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class StartingShapesTests
{
    public static TheoryData<StartingShape> All => [.. Enum.GetValues<StartingShape>()];

    [Theory]
    [MemberData(nameof(All))]
    public void EveryShapeIsTenBandsInsideTheLimits(StartingShape shape)
    {
        var bands = StartingShapes.Bands(shape);
        Assert.Equal(10, bands.Count);
        Assert.All(bands, band => Assert.Equal(band, band.Held()));
    }

    [Theory]
    [MemberData(nameof(All))]
    public void NoBoostIsBiggerThanFourDecibels(StartingShape shape) =>
        Assert.All(StartingShapes.Bands(shape).Where(band => band.InUse && band.UsesGain), band => Assert.True(band.Gain <= 4));

    [Fact]
    public void FlatUsesNoBandsAndEveryOtherShapeUsesSome()
    {
        Assert.DoesNotContain(StartingShapes.Bands(StartingShape.Flat), band => band.InUse);
        Assert.All(Enum.GetValues<StartingShape>().Where(shape => shape != StartingShape.Flat),
            shape => Assert.Contains(StartingShapes.Bands(shape), band => band.InUse));
    }

    [Fact]
    public void TheBandsInUseComeFirstAndTheRestKeepTheirStockFrequencies()
    {
        var bands = StartingShapes.Bands(StartingShape.CutMud);
        Assert.True(bands[0].InUse);
        Assert.True(bands[1].InUse);
        Assert.False(bands[2].InUse);
        Assert.Equal(StockBands.Game[2].Hertz, bands[2].Hertz);
    }

    [Fact]
    public void ShapesDoWhatTheyAreNamedFor()
    {
        Assert.True(BandResponse.TotalDb(StartingShapes.Bands(StartingShape.CutMud), 250) < -2);
        Assert.True(BandResponse.TotalDb(StartingShapes.Bands(StartingShape.AddBass), 60) > 3);
        Assert.True(BandResponse.TotalDb(StartingShapes.Bands(StartingShape.CutRumble), 20) < -10);
        Assert.True(BandResponse.TotalDb(StartingShapes.Bands(StartingShape.AddTreble), 16000) > 2);
    }

    [Fact]
    public void AShapeThatDoesNotExistIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => StartingShapes.Bands((StartingShape)99));
}
