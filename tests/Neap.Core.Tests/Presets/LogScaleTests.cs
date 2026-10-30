using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class LogScaleTests
{
    [Theory]
    [InlineData(20, 0)]
    [InlineData(200, 1.0 / 3)]
    [InlineData(2000, 2.0 / 3)]
    [InlineData(20000, 1)]
    public void EachTenfoldStepIsAnEqualShare(double hertz, double share) =>
        Assert.Equal(share, LogScale.Share(hertz, 20, 20000), 9);

    [Theory]
    [InlineData(5, 0)]
    [InlineData(0, 0)]
    [InlineData(40000, 1)]
    public void AValuePastAnEndSitsAtThatEnd(double hertz, double share) =>
        Assert.Equal(share, LogScale.Share(hertz, 20, 20000), 9);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(0.5, 632.455532)]
    [InlineData(1, 20000)]
    [InlineData(-1, 20)]
    [InlineData(2, 20000)]
    public void AShareGivesItsValueHeldToTheEnds(double share, double hertz) =>
        Assert.Equal(hertz, LogScale.At(share, 20, 20000), 5);

    [Theory]
    [InlineData(31.5)]
    [InlineData(1000)]
    [InlineData(19500)]
    public void AValueComesBackFromItsShare(double hertz) =>
        Assert.Equal(hertz, LogScale.At(LogScale.Share(hertz, 20, 19500), 20, 19500), 6);

    [Fact]
    public void TheSweepSliderUsesTheSameScale()
    {
        const double hertz = 1000;
        double share = LogScale.Share(hertz, ParametricBand.LowestHertz, ParametricBand.HighestHertz);

        Assert.Equal(share * 500, ToneSweep.Position(hertz, 500), 9);
        Assert.Equal(hertz, ToneSweep.Hertz(share * 500, 500), 6);
    }
}
