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
    public void EveryGainIsAStepTheHeadsetTakes()
    {
        var bands = ParametricEq.Fit([new Adjustment(125, -45, 15), new Adjustment(5100, -60, 10),
            new Adjustment(253, 35, 10), new Adjustment(15900, 90, 15)]);
        Assert.All(bands, band => Assert.Equal(0, band % PresetStore.BandStep));
    }

    [Fact]
    public void DraggingAPointLeavesDistantBandsAlone()
    {
        // A deep, narrow cut dragged across two octaves, a step at a time.
        for (int i = 0; i <= 200; i++)
        {
            int frequency = (int)Math.Round(300 * Math.Pow(2, i / 100.0));
            var bands = ParametricEq.Fit([new Adjustment(frequency, -90, 10)]);
            for (int band = 0; band < bands.Length; band++)
            {
                if (Math.Abs(Math.Log2(frequency / ParametricEq.Centres[band])) > 2)
                    Assert.Equal(0, bands[band]);
            }
        }
    }

    [Fact]
    public void NoBandPassesItsLimit()
    {
        var bands = ParametricEq.Fit([new Adjustment(1000, 90, 30), new Adjustment(2000, 90, 30)]);
        Assert.All(bands, band => Assert.InRange(band, -90, 90));
        Assert.Contains(90, bands);
    }

    [Fact]
    public void AShapeDescribesTheGainsItWasMadeWith()
    {
        var shape = Shaped(Cut);
        Assert.True(shape.Describes(ParametricEq.Fit(Cut)));
    }

    [Fact]
    public void AShapeIsKnownByItsGainsNotByFittingAgain()
    {
        // As a preset saved before a change to the fit: its gains are no
        // longer what its adjustments fit to, but still what it was made with.
        int[] saved = [0, 0, 0, 0, 0, 0, 0, -45, -20, 0];
        Assert.NotEqual(saved, ParametricEq.Fit(Cut));
        Assert.True(new ParametricShape(Cut, saved).Describes(saved));
    }

    [Fact]
    public void GainsChangedElsewhereAreNoLongerDescribed()
    {
        var bands = ParametricEq.Fit(Cut);
        bands[0] += 10;
        Assert.False(Shaped(Cut).Describes(bands));
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

    private static readonly Adjustment[] Cut = [new Adjustment(4915, -60, 10)];

    private static ParametricShape Shaped(Adjustment[] adjustments) => new(adjustments, ParametricEq.Fit(adjustments));
    private static readonly int[] Flat = new int[10];
    private static readonly int[] BassBoost = [50, 50, 35, 0, 0, 0, 0, 0, 0, 0];

    [Fact]
    public void ALookAtTheBandsComesBackAsItWas() =>
        Assert.Equal(ParametricStart.Resume, ParametricEq.StartFrom(Shaped(Cut), ParametricEq.Fit(Cut), null, null));

    [Fact]
    public void BandsMovedSinceGiveUpWhatWasSetAside()
    {
        var moved = ParametricEq.Fit(Cut);
        moved[0] = 30;
        Assert.Equal(ParametricStart.Flat, ParametricEq.StartFrom(Shaped(Cut), moved, null, null));
    }

    [Fact]
    public void APresetMadeParametricallyComesBackInThatForm()
    {
        var edited = ParametricEq.Fit(Cut);
        edited[0] = 30;
        Assert.Equal(ParametricStart.Reopen, ParametricEq.StartFrom(null, edited, Shaped(Cut), ParametricEq.Fit(Cut)));
    }

    [Fact]
    public void WhatWasSetAsideComesBeforeThePreset()
    {
        Adjustment[] unsaved = [new Adjustment(1000, 40, 20)];
        Assert.Equal(ParametricStart.Resume,
            ParametricEq.StartFrom(Shaped(unsaved), ParametricEq.Fit(unsaved), Shaped(Cut), ParametricEq.Fit(Cut)));
    }

    [Fact]
    public void StoredAdjustmentsThatNoLongerFitThePresetAreIgnored() =>
        Assert.Equal(ParametricStart.Flat, ParametricEq.StartFrom(null, Flat, Shaped(Cut), BassBoost));

    [Fact]
    public void APresetMadeWithTheBandsStartsFlat() =>
        Assert.Equal(ParametricStart.Flat, ParametricEq.StartFrom(null, Flat, null, BassBoost));

    [Fact]
    public void StartingFlatFromACurveChangesTheSound() =>
        Assert.True(ParametricEq.StartChangesSound(ParametricStart.Flat, BassBoost, BassBoost));

    [Fact]
    public void StartingFlatFromFlatChangesNothing() =>
        Assert.False(ParametricEq.StartChangesSound(ParametricStart.Flat, Flat, null));

    [Fact]
    public void ResumingChangesNothing() =>
        Assert.False(ParametricEq.StartChangesSound(ParametricStart.Resume, ParametricEq.Fit(Cut), null));

    [Fact]
    public void ReopeningAnUnchangedPresetChangesNothing() =>
        Assert.False(ParametricEq.StartChangesSound(ParametricStart.Reopen, ParametricEq.Fit(Cut), ParametricEq.Fit(Cut)));

    [Fact]
    public void ReopeningAPresetMovedSinceChangesTheSound()
    {
        var edited = ParametricEq.Fit(Cut);
        edited[0] = 30;
        Assert.True(ParametricEq.StartChangesSound(ParametricStart.Reopen, edited, ParametricEq.Fit(Cut)));
    }

    private static readonly Adjustment Pressed = new(4915, -60, 10);

    private static Adjustment Drag(double across, double down) =>
        ParametricEq.Dragged(Pressed, across, down, width: 600, height: 240, range: 120);

    [Fact]
    public void APressWithoutAMoveLeavesThePointWhereItIs()
    {
        Assert.Equal(Pressed, Drag(0, 0));
        Assert.Equal(Pressed, Drag(2, -2));
    }

    [Fact]
    public void ADragUpRaisesTheGainByTheDistance() =>
        // 240 pixels span 24 dB, so 20 pixels up is 2 dB.
        Assert.Equal(-40, Drag(0, -20).Gain);

    [Fact]
    public void ADragAcrossMovesTheFrequencyFromWhereItWas()
    {
        var moved = Drag(60, 0);
        Assert.Equal(Pressed.Gain, moved.Gain);
        // A tenth of the plot is a tenth of the range's three decades.
        Assert.InRange(moved.Frequency, 9700, 9900);
    }

    [Fact]
    public void ADraggedGainIsAStepTheHeadsetTakes() =>
        Assert.Equal(0, Drag(0, -7).Gain % PresetStore.BandStep);

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
