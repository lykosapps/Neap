using Neap.Core.Mix;

namespace Neap.Core.Tests.Mix;

public class DialSizingTests
{
    [Theory]
    [InlineData(500, 180)]
    [InlineData(540, 194.4)]
    public void BesideTheColumnTheDialTakesAShareOfTheWidth(double width, double size) =>
        Assert.Equal(size, DialSizing.Fit(width, beside: true), 6);

    [Theory]
    [InlineData(360, 180)]
    [InlineData(390, 195)]
    public void AboveTheColumnTheDialTakesALargerShare(double width, double size) =>
        Assert.Equal(size, DialSizing.Fit(width, beside: false), 6);

    [Theory]
    [InlineData(0, true)]
    [InlineData(200, false)]
    public void ANarrowCardNeverShrinksTheDialBelowItsSmallestSize(double width, bool beside) =>
        Assert.Equal(DialSizing.Smallest, DialSizing.Fit(width, beside));

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2000, false)]
    public void AWideCardNeverGrowsTheDialPastItsLargestSize(double width, bool beside) =>
        Assert.Equal(DialSizing.Largest, DialSizing.Fit(width, beside));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AWidthNotYetMeasuredGivesTheSmallestSize(double width) =>
        Assert.Equal(DialSizing.Smallest, DialSizing.Fit(width, beside: true));

    [Fact]
    public void TheSizesKeepTheDrawnGeometryBetweenThem()
    {
        Assert.InRange(DialSizing.Drawn, DialSizing.Smallest, DialSizing.Largest);
        Assert.True(DialSizing.CapWidth(DialSizing.Smallest) < DialSizing.Smallest * 0.5);
    }
}
