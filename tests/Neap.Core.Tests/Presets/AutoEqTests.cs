using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class AutoEqTests
{
    /// <summary>
    /// The change one Stealth Pro II needs to reach a diffuse-field target with a
    /// bass and treble tilt, both set to 0 dB at 1 kHz, every sixth of an octave.
    /// </summary>
    private static readonly (double Hertz, double Db)[] StealthProII =
    [
        (20, -5.0), (22, -5.2), (25, -5.4), (28, -5.7), (32, -6.1), (36, -6.3), (40, -6.3), (45, -6.2), (50, -6.1), (57, -5.7),
        (63, -5.3), (71, -4.6), (80, -3.9), (90, -3.1), (101, -3.0), (113, -3.4), (127, -3.2), (143, -2.3), (160, -0.9), (180, 0.4),
        (202, 1.8), (226, 3.1), (254, 4.2), (285, 4.5), (320, 3.6), (359, 2.1), (403, 0.4), (453, -0.3), (508, -0.2), (570, -0.1),
        (640, -0.5), (718, -0.8), (806, -0.6), (905, -0.1), (1016, 0.0), (1140, 0.5), (1280, 1.0), (1437, -0.6), (1613, 0.1), (1810, 0.5),
        (2032, 0.9), (2281, 0.7), (2560, 1.3), (2874, 2.0), (3225, 3.8), (3620, 6.0), (4064, 4.4), (4561, -1.7), (5120, -0.8), (5747, 0.0),
        (6451, -5.2), (7241, -6.6), (8127, -3.5), (9123, -2.0), (10240, -3.9), (11494, -2.3), (12902, -4.2), (14482, 0.6), (16255, 5.1), (18246, 5.5),
        (20480, 7.1),
    ];

    private static ParametricBand Bell(double hertz, double q, double gain) => new(BandType.Peaking, hertz, q, gain, true);

    /// <summary>Points every twelfth of an octave from 20 Hz to 20 kHz, highest first, so the fit has to sort them.</summary>
    private static (double Hertz, double Db)[] Curve(Func<double, double> db) =>
        [.. Enumerable.Range(0, 120).Reverse().Select(i => 20 * Math.Pow(2, i / 12.0)).Select(hertz => (hertz, db(hertz)))];

    private static (double Hertz, double Db)[] From(IReadOnlyList<ParametricBand> bands) => Curve(hertz => BandResponse.TotalDb(bands, hertz));

    /// <summary>The root mean square difference between the bands and the wanted change, from 20 Hz to the highest fitted.</summary>
    private static double Rms(IReadOnlyList<ParametricBand> bands, IEnumerable<(double Hertz, double Db)> wanted)
    {
        var fitted = wanted.Where(p => p.Hertz >= 20 && p.Hertz <= AutoEq.HighestFitted).ToList();
        return Math.Sqrt(fitted.Average(p => Math.Pow(p.Db - BandResponse.TotalDb(bands, p.Hertz), 2)));
    }

    [Fact]
    public void BandsMadeFromKnownBandsAreFoundAgain()
    {
        var wanted = From([Bell(300, 1.5, -5), Bell(3000, 2, 4), new(BandType.LowShelf, 80, 0.7071, -3, true)]);
        var bands = AutoEq.Fit(wanted);
        Assert.True(Rms(bands, wanted) < 0.5, $"error {Rms(bands, wanted):F2} dB");
    }

    [Fact]
    public void AFlatChangeGivesNoBands()
    {
        Assert.Equal(StockBands.Flat, AutoEq.Fit(Curve(_ => 0)));
    }

    [Fact]
    public void ASmallRippleGivesNoBands()
    {
        var bands = AutoEq.Fit(Curve(hertz => 0.3 * Math.Sin(Math.Log2(hertz) * 3)));
        Assert.DoesNotContain(bands, band => band.InUse);
    }

    [Fact]
    public void NoWantedPointsGiveNoBands()
    {
        Assert.Equal(StockBands.Flat, AutoEq.Fit([]));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(4)]
    [InlineData(1)]
    public void TheBandsKeepToTheHeadsetsLimits(int most)
    {
        var bands = AutoEq.Fit(Curve(hertz => 15 * Math.Sin(Math.Log2(hertz) * 2.3)), most);

        Assert.Equal(BandWrites.Bands, bands.Count);
        var used = bands.Where(band => band.InUse).ToList();
        Assert.InRange(used.Count, 1, most);
        Assert.Equal(used, bands.Take(used.Count));
        Assert.Equal(used.OrderBy(band => band.Hertz), used);
        Assert.All(used, band =>
        {
            Assert.InRange(band.Gain, -ParametricBand.MostGain, AutoEq.MostBoost);
            Assert.InRange(band.Q, ParametricBand.LowestQ, AutoEq.MostQ);
            Assert.InRange(band.Hertz, ParametricBand.LowestHertz, AutoEq.HighestFitted);
            Assert.Contains(band.Type, new[] { BandType.Peaking, BandType.LowShelf, BandType.HighShelf });
        });
        Assert.Equal(StockBands.Flat.Skip(used.Count), bands.Skip(used.Count));
    }

    [Fact]
    public void ABigBoostIsHeldToTheMostBoost()
    {
        var bands = AutoEq.Fit(From([Bell(1000, 1, 12)]));
        double most = Enumerable.Range(0, 200).Max(i => BandResponse.TotalDb(bands, 20 * Math.Pow(1000, i / 199.0)));
        Assert.InRange(most, AutoEq.MostBoost - 0.5, AutoEq.MostBoost + 0.1);
    }

    [Fact]
    public void ABigCutIsMadeInFull()
    {
        var wanted = From([Bell(1000, 1, -10)]);
        var bands = AutoEq.Fit(wanted);
        Assert.Equal(-10, BandResponse.TotalDb(bands, 1000), 0);
    }

    [Fact]
    public void NothingAboveTheHighestFittedIsChased()
    {
        var bands = AutoEq.Fit(Curve(hertz => -10 * Math.Exp(-Math.Pow(Math.Log2(hertz / 15000) / 0.15, 2))));
        Assert.DoesNotContain(bands, band => band.InUse);
    }

    [Fact]
    public void AMeasuredHeadsetIsBroughtCloseToItsTarget()
    {
        var bands = AutoEq.Fit(StealthProII);

        double before = Rms(StockBands.Flat, StealthProII), after = Rms(bands, StealthProII);
        Assert.True(before > 3, $"before {before:F2} dB");
        Assert.True(after < 0.8, $"after {after:F2} dB");
        Assert.InRange(bands.Count(band => band.InUse), 4, BandWrites.Bands);
    }

    [Fact]
    public void TheSameChangeGivesTheSameBands()
    {
        Assert.Equal(AutoEq.Fit(StealthProII), AutoEq.Fit(StealthProII));
    }

    [Fact]
    public void MoreBandsThanTheHeadsetHasAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AutoEq.Fit(StealthProII, BandWrites.Bands + 1));
    }
}
