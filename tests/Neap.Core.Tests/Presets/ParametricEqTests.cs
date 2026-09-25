using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class ParametricEqTests
{
    [Fact]
    public void AFilterGivesItsFullGainAtItsCentre() =>
        Assert.Equal(6, ParametricEq.Peak(1000, 6, 1.414, 1000), 3);

    [Fact]
    public void AFilterLeavesFarAwayFrequenciesAlone() =>
        Assert.Equal(0, ParametricEq.Peak(1000, 6, 1.414, 30), 1);

    [Fact]
    public void AnOctaveIsTheHeadsetsOwnBandWidth() =>
        Assert.Equal(1.414, ParametricEq.Q(10), 3);

    [Fact]
    public void NeighbouringBandsAddUp()
    {
        // Every band at +9 plays well above +9, which is why the curve is
        // drawn from the filters, not through the gains.
        var bands = Enumerable.Repeat(90, 10).ToArray();
        Assert.True(ParametricEq.Heard(bands, 1000) > 12);
    }

    [Fact]
    public void NoAdjustmentsFitFlat() =>
        Assert.All(ParametricEq.Fit([]), band => Assert.Equal(0, band));

    [Fact]
    public void AnAdjustmentOnABandIsMetByThatBand()
    {
        var bands = ParametricEq.Fit([new Adjustment(1000, 60, 10)]);
        Assert.InRange(bands[5], 50, 70);
        Assert.True(ParametricEq.Miss([new Adjustment(1000, 60, 10)], bands) < ParametricEq.AudibleMiss);
    }

    [Fact]
    public void ABroadAdjustmentLandsWithinEarshot()
    {
        Adjustment[] asked = [new Adjustment(1000, 60, 20)];
        Assert.True(ParametricEq.Miss(asked, ParametricEq.Fit(asked)) < ParametricEq.AudibleMiss);
    }

    [Fact]
    public void ACutBetweenBandsIsSharedByTheBandsEitherSide()
    {
        var bands = ParametricEq.Fit([new Adjustment(4915, -60, 10)]);
        Assert.True(bands[7] < -30);
        Assert.True(bands[8] < 0);
    }

    [Fact]
    public void AnAdjustmentTooNarrowForTheBandsIsAnAudibleMiss()
    {
        Adjustment[] asked = [new Adjustment(3000, -90, 10), new Adjustment(4000, 90, 10)];
        Assert.True(ParametricEq.Miss(asked, ParametricEq.Fit(asked)) > ParametricEq.AudibleMiss);
    }

    [Fact]
    public void NoBandPassesItsLimit()
    {
        var bands = ParametricEq.Fit([new Adjustment(1000, 90, 30), new Adjustment(2000, 90, 30)]);
        Assert.All(bands, band => Assert.InRange(band, -90, 90));
        Assert.Contains(90, bands);
    }

    [Fact]
    public void TheSameAdjustmentsAlwaysFitTheSameGains()
    {
        Adjustment[] asked = [new Adjustment(4915, -60, 12), new Adjustment(120, 40, 25)];
        Assert.Equal(ParametricEq.Fit(asked), ParametricEq.Fit(asked));
        Assert.True(ParametricEq.Matches(asked, ParametricEq.Fit(asked)));
    }

    [Fact]
    public void GainsChangedElsewhereNoLongerMatch()
    {
        Adjustment[] asked = [new Adjustment(4915, -60, 10)];
        var bands = ParametricEq.Fit(asked);
        bands[0] += 10;
        Assert.False(ParametricEq.Matches(asked, bands));
    }

    [Fact]
    public void AnAudibleMissIsSaid()
    {
        Adjustment[] narrow = [new Adjustment(3000, -90, 10), new Adjustment(4000, 90, 10)];
        Adjustment[] broad = [new Adjustment(1000, 60, 20)];
        Assert.True(ParametricEq.FallsShort(narrow, ParametricEq.Fit(narrow)));
        Assert.False(ParametricEq.FallsShort(broad, ParametricEq.Fit(broad)));
    }

    [Fact]
    public void NoAdjustmentsHaveNothingToMiss() =>
        Assert.False(ParametricEq.FallsShort([], [60, 0, 0, 0, 0, 0, 0, 0, 0, 0]));

    [Fact]
    public void ANewAdjustmentIsFlat()
    {
        var added = ParametricEq.Next([]);
        Assert.Equal(0, added.Gain);
        Assert.All(ParametricEq.Fit([added]), band => Assert.Equal(0, band));
    }

    [Fact]
    public void ANewAdjustmentKeepsClearOfTheOthers()
    {
        var first = ParametricEq.Next([]);
        var second = ParametricEq.Next([first]);
        Assert.True(Math.Abs(Math.Log2((double)second.Frequency / first.Frequency)) > 1);
    }

    [Fact]
    public void OnlyTheGameBankIsParametric()
    {
        Assert.True(ParametricEq.Covers(Bank.Game));
        Assert.False(ParametricEq.Covers(Bank.Mic));
    }

    [Fact]
    public void AnAdjustmentIsHeldInsideItsRange() =>
        Assert.Equal(new Adjustment(20, 90, 10), new Adjustment(5, 200, 2).Held());

    [Theory]
    [InlineData(20, 0)]
    [InlineData(20000, 300)]
    [InlineData(632, 150)]
    public void FrequenciesSitOnALogarithmicScale(int frequency, double x) =>
        Assert.Equal(x, ParametricEq.X(frequency, 300), 0);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(300, 20000)]
    [InlineData(-40, 20)]
    [InlineData(999, 20000)]
    [InlineData(150, 632)]
    [InlineData(275, 11200)]
    public void APointAcrossThePlotMeansAFrequencyInsideTheRange(double x, int frequency) =>
        Assert.Equal(frequency, ParametricEq.FrequencyAt(x, 300));
}
