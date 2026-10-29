using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class BandResponseTests
{
    private static ParametricBand Make(BandType type, double hertz = 1000, double q = 1, double gain = 0) => new(type, hertz, q, gain, true);

    [Theory]
    [InlineData(6)]
    [InlineData(-9)]
    [InlineData(0.5)]
    public void ABellGivesItsFullGainAtItsCentre(double gain) =>
        Assert.Equal(gain, BandResponse.Db(Make(BandType.Peaking, gain: gain), 1000), 3);

    [Fact]
    public void BandsAt0DbDoNotChangeTheSound()
    {
        Assert.False(BandResponse.ChangesSound([]));
        Assert.False(BandResponse.ChangesSound([Make(BandType.Peaking), Make(BandType.LowShelf, 100), Make(BandType.HighShelf, 8000)]));
    }

    [Fact]
    public void ABoostOrACutChangesTheSound()
    {
        Assert.True(BandResponse.ChangesSound([Make(BandType.Peaking, gain: 1)]));
        Assert.True(BandResponse.ChangesSound([Make(BandType.LowCut, 80)]));
    }

    [Fact]
    public void ABellAndItsOppositeCancel() =>
        Assert.False(BandResponse.ChangesSound([Make(BandType.Peaking, gain: 4), Make(BandType.Peaking, gain: -4)]));

    [Fact]
    public void ABellLeavesFarAwayFrequenciesAlone()
    {
        var bell = Make(BandType.Peaking, gain: 9);
        Assert.Equal(0, BandResponse.Db(bell, 30), 1);
        Assert.Equal(0, BandResponse.Db(bell, 18000), 1);
    }

    [Fact]
    public void AWiderBellReachesFurtherFromItsCentre()
    {
        double narrow = BandResponse.Db(Make(BandType.Peaking, q: 4, gain: 6), 1500);
        double wide = BandResponse.Db(Make(BandType.Peaking, q: 0.5, gain: 6), 1500);
        Assert.True(wide > narrow);
    }

    [Fact]
    public void AShelfSettlesToItsGainOnItsOwnSide()
    {
        var low = Make(BandType.LowShelf, 200, 0.7, 5);
        Assert.Equal(5, BandResponse.Db(low, 20), 1);
        Assert.Equal(0, BandResponse.Db(low, 15000), 1);
        var high = Make(BandType.HighShelf, 5000, 0.7, -4);
        Assert.Equal(-4, BandResponse.Db(high, 19000), 1);
        Assert.Equal(0, BandResponse.Db(high, 50), 1);
    }

    [Fact]
    public void ACutIsAboutThreeDecibelsDownAtItsCutoffWhenItsWidthIsTheFlatOne()
    {
        Assert.Equal(-3.01, BandResponse.Db(Make(BandType.LowCut, 1000, 0.7071), 1000), 1);
        Assert.Equal(-3.01, BandResponse.Db(Make(BandType.HighCut, 1000, 0.7071), 1000), 1);
    }

    [Fact]
    public void ACutRemovesWhatIsBelowOrAboveItAndLeavesTheOtherSide()
    {
        var low = Make(BandType.LowCut, 1000, 0.7071);
        Assert.True(BandResponse.Db(low, 100) < -30);
        Assert.Equal(0, BandResponse.Db(low, 15000), 1);
        var high = Make(BandType.HighCut, 1000, 0.7071);
        Assert.True(BandResponse.Db(high, 10000) < -30);
        Assert.Equal(0, BandResponse.Db(high, 100), 1);
    }

    [Fact]
    public void AGentleCutFallsAboutSixDecibelsAnOctaveWhereAStandardOneFallsTwelve()
    {
        var gentle = Make(BandType.GentleLowCut, 1000, 0.7071);
        var standard = Make(BandType.LowCut, 1000, 0.7071);
        Assert.Equal(6, BandResponse.Db(gentle, 250) - BandResponse.Db(gentle, 125), 0);
        Assert.Equal(12, BandResponse.Db(standard, 250) - BandResponse.Db(standard, 125), 0);
        Assert.Equal(-3.01, BandResponse.Db(Make(BandType.GentleHighCut, 1000), 1000), 1);
    }

    [Fact]
    public void AStackedCutIsSteeperThanOneBand()
    {
        var two = CutBands.Bands(lowCut: true, 1000, CutSlope.Steep);
        var three = CutBands.Bands(lowCut: true, 1000, CutSlope.Steepest);
        double one = BandResponse.Db(Make(BandType.LowCut, 1000, 0.7071), 250);

        Assert.True(BandResponse.TotalDb(two, 250) < one - 10);
        Assert.True(BandResponse.TotalDb(three, 250) < BandResponse.TotalDb(two, 250) - 10);
        // About 3 dB down at the cutoff, as a flat stacked cut should be.
        Assert.Equal(-3.0, BandResponse.TotalDb(two, 1000), 0);
        Assert.Equal(-3.0, BandResponse.TotalDb(three, 1000), 0);
    }

    [Fact]
    public void ANotchRemovesItsCentre() =>
        Assert.True(BandResponse.Db(Make(BandType.Notch, q: 4), 1000) < -40);

    [Fact]
    public void ABandThatIsOffOrBypassedChangesNothing()
    {
        Assert.Equal(0, BandResponse.Db(Make(BandType.Peaking, gain: 9) with { InUse = false }, 1000));
        Assert.Equal(0, BandResponse.Db(Make(BandType.Bypass, gain: 9), 1000));
    }

    [Fact]
    public void BandsAddUpInDecibels()
    {
        ParametricBand[] bands = [Make(BandType.Peaking, gain: 3), Make(BandType.Peaking, gain: 4)];
        Assert.Equal(7, BandResponse.TotalDb(bands, 1000), 3);
        Assert.Equal(0, BandResponse.TotalDb([], 1000));
    }

    [Fact]
    public void AValueOutsideTheLimitsIsHeldBeforeItIsDrawn() =>
        Assert.Equal(12, BandResponse.Db(Make(BandType.Peaking, gain: 40), 1000), 3);
}
