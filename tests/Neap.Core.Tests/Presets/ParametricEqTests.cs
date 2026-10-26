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
    public void ThePlacesRunFromTheFirstBandToTheLast()
    {
        Assert.Equal(ParametricEq.Centres[0], ParametricEq.FrequencyAtPlace(0), 6);
        Assert.Equal(ParametricEq.Centres[9], ParametricEq.FrequencyAtPlace(9), 6);
        Assert.Equal(ParametricEq.Centres[0] * Math.Sqrt(ParametricEq.Centres[9] / ParametricEq.Centres[0]),
            ParametricEq.FrequencyAtPlace(4.5), 6);
    }

    [Fact]
    public void FlatBandsPlayFlatAcrossTheView() =>
        Assert.All(ParametricEq.HeardAcross(new int[10], 50), tenths => Assert.Equal(0, tenths, 6));

    [Fact]
    public void TheViewIsDrawnFromTheFiltersNotThroughTheGains()
    {
        // All ten at +9 dB play about +13.5 dB in the middle, not +9.
        var heard = ParametricEq.HeardAcross(Enumerable.Repeat(90, 10).ToArray(), 101);
        Assert.InRange(heard.Max(), 125, 145);
    }

    [Fact]
    public void OneBandPeaksAtItsOwnColumn()
    {
        var bands = new int[10];
        bands[5] = 60;
        // 101 places a tenth of a column apart, the first half a column before band 0.
        var heard = ParametricEq.HeardAcross(bands, 101);
        double place = -0.5 + Array.IndexOf(heard, heard.Max()) * 0.1;
        Assert.Equal(5, place, 0.15);
        Assert.InRange(heard.Max(), 55, 60.01);
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
    public void ABoostAtTheTopOfTheRangeIsAMissTheBandsCannotReach()
    {
        // The highest band is at 16 kHz, so 20 kHz is beyond what its octave-wide filter lifts.
        var asked = new[] { new Adjustment(20000, 90, 10) };
        Assert.True(ParametricEq.FallsShort(asked, ParametricEq.Fit(asked)));
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

    [Fact]
    public void ArrowKeysMoveGainByAStepAndFrequencyByATwelfthOfAnOctave()
    {
        var start = new Adjustment(1000, 0, 10);

        var up = ParametricEq.Nudged(start, 0, 1, coarse: false);
        var right = ParametricEq.Nudged(start, 1, 0, coarse: false);
        var left = ParametricEq.Nudged(start, -1, 0, coarse: false);

        Assert.Equal(PresetStore.BandStep, up.Gain);
        Assert.Equal(1000, up.Frequency);
        Assert.Equal(1060, right.Frequency);
        Assert.Equal(944, left.Frequency);
    }

    [Fact]
    public void ShiftMovesFourStepsAndAThirdOfAnOctave()
    {
        var start = new Adjustment(1000, 0, 10);

        var moved = ParametricEq.Nudged(start, 1, -1, coarse: true);

        Assert.Equal(-4 * PresetStore.BandStep, moved.Gain);
        Assert.Equal(1260, moved.Frequency);
    }

    [Fact]
    public void ArrowKeysStayInsideTheRangeAndAlwaysMoveALowFrequency()
    {
        Assert.Equal(Adjustment.LowestFrequency, ParametricEq.Nudged(new Adjustment(20, 0, 10), -1, 0, false).Frequency);
        Assert.Equal(Adjustment.HighestFrequency, ParametricEq.Nudged(new Adjustment(20000, 0, 10), 1, 0, true).Frequency);
        Assert.Equal(Adjustment.MostGain, ParametricEq.Nudged(new Adjustment(1000, 90, 10), 0, 1, true).Gain);
        Assert.NotEqual(20, ParametricEq.Nudged(new Adjustment(20, 0, 10), 1, 0, false).Frequency);
    }
}
