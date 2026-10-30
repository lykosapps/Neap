using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class ParametricBandTests
{
    private static ParametricBand Bell(double hertz = 1000, double gain = 3, bool inUse = true) =>
        new(BandType.Peaking, hertz, 1, gain, inUse);

    [Fact]
    public void ABandThatIsOffIsSentAsBypassAndKeepsItsOwnValues()
    {
        var band = Bell(inUse: false);
        Assert.Equal(BandType.Bypass, band.WireType);
        Assert.Equal(0, band.WireGain);
        Assert.Equal(3, band.Gain);
    }

    [Theory]
    [InlineData(BandType.Peaking, true)]
    [InlineData(BandType.LowShelf, true)]
    [InlineData(BandType.HighShelf, true)]
    [InlineData(BandType.Notch, false)]
    [InlineData(BandType.LowCut, false)]
    [InlineData(BandType.HighCut, false)]
    [InlineData(BandType.GentleLowCut, false)]
    [InlineData(BandType.GentleHighCut, false)]
    public void OnlyABellAndTheShelvesHaveAGain(BandType type, bool expected) =>
        Assert.Equal(expected, new ParametricBand(type, 1000, 1, 3, true).UsesGain);

    [Fact]
    public void AFilterWithoutAGainSendsNone()
    {
        var cut = new ParametricBand(BandType.LowCut, 80, 0.7, 5, true);
        Assert.Equal(0, cut.WireGain);
    }

    [Fact]
    public void ValuesAreHeldInsideTheirRanges()
    {
        var held = new ParametricBand(BandType.Peaking, 5, 50, -30, true).Held();
        Assert.Equal(ParametricBand.LowestHertz, held.Hertz);
        Assert.Equal(ParametricBand.HighestQ, held.Q);
        Assert.Equal(-ParametricBand.MostGain, held.Gain);
    }

    [Fact]
    public void ATopFrequencyStaysBelowHalfTheLowestSampleRateInUse()
    {
        Assert.Equal(19500, Bell(30000).Held().Hertz);
        Assert.True(ParametricBand.HighestHertz < 0.45 * 44100);
    }

    [Theory]
    [InlineData(1.414, 1.0)]
    [InlineData(0.667, 2.0)]
    [InlineData(4.318, 0.33)]
    public void AWidthReadsAsTheOctavesItSpans(double q, double octaves) =>
        Assert.Equal(octaves, ParametricBand.Octaves(q), 2);
}
