namespace Neap.Core.Tests;

public class DensityTests
{
    [Theory]
    [InlineData(1280, 800)]
    [InlineData(1280, 820)]
    [InlineData(1024, 600)]
    [InlineData(500, 480)]
    public void ASmallScreenIsDrawnAtTouchSize(double width, double height) =>
        Assert.Equal(DensityMode.Touch, Density.For(width, height));

    [Theory]
    [InlineData(1281, 800)]
    [InlineData(1280, 821)]
    [InlineData(1180, 900)]
    [InlineData(1920, 1032)]
    public void ALargerScreenStaysCompact(double width, double height) =>
        Assert.Equal(DensityMode.Compact, Density.For(width, height));
}
