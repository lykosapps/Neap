using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class ResponseCurveTests
{
    [Theory]
    [InlineData(90, 10)]
    [InlineData(0, 60)]
    [InlineData(-90, 110)]
    public void TheRangeFillsThePlotTopToBottom(int tenths, double y) =>
        Assert.Equal(y, ResponseCurve.Y(tenths, -90, 90, 10, 110), 6);

    [Fact]
    public void APressWithoutAMoveLeavesTheBandWhereItIs()
    {
        Assert.Equal(-82, ResponseCurve.Dragged(-82, 0, -90, 90, 180));
        Assert.Equal(-82, ResponseCurve.Dragged(-82, 3, -90, 90, 180));
    }

    [Fact]
    public void ADragMovesTheBandByTheDistanceInSteps()
    {
        // 180 pixels span 18 dB, so 20 pixels up is 2 dB.
        Assert.Equal(40, ResponseCurve.Dragged(20, -20, -90, 90, 180));
        Assert.Equal(0, ResponseCurve.Dragged(20, -7, -90, 90, 180) % PresetStore.BandStep);
    }

    [Fact]
    public void ADraggedBandStaysInsideTheRange() =>
        Assert.Equal(90, ResponseCurve.Dragged(80, -100, -90, 90, 180));

    [Fact]
    public void EachBandSitsInTheMiddleOfItsColumn()
    {
        Assert.Equal(5, ResponseCurve.X(0, 10, 100), 6);
        Assert.Equal(95, ResponseCurve.X(9, 10, 100), 6);
    }

    [Fact]
    public void APeakIsFlatAtItsTop()
    {
        // A tangent that is not flat at a local peak would carry the line
        // above the band's own value.
        var slopes = ResponseCurve.Slopes([(0, 50), (10, 10), (20, 50)]);
        Assert.Equal(0, slopes[1], 6);
    }

    [Fact]
    public void ARunOfEqualValuesIsFlat()
    {
        var slopes = ResponseCurve.Slopes([(0, 30), (10, 30), (20, 30)]);
        Assert.All(slopes, s => Assert.Equal(0, s, 6));
    }

    [Fact]
    public void ASteepStepIsHeldToAMonotoneTangent()
    {
        var points = new (double X, double Y)[] { (0, 0), (10, 1), (20, 100), (30, 101) };
        var slopes = ResponseCurve.Slopes(points);

        // Fritsch–Carlson keeps each tangent within three times the secant
        // either side of it, which is what keeps the cubic monotone.
        for (int i = 0; i < points.Length - 1; i++)
        {
            double secant = (points[i + 1].Y - points[i].Y) / (points[i + 1].X - points[i].X);
            double a = slopes[i] / secant, b = slopes[i + 1] / secant;
            Assert.True(a * a + b * b <= 9 + 1e-9);
        }
    }
}
