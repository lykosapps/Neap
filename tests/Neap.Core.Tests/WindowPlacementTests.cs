namespace Neap.Core.Tests;

public class WindowPlacementTests
{
    private static readonly Box Primary = new(0, 0, 1920, 1032);
    private static readonly Box Second = new(1920, 0, 2560, 1400);

    [Fact]
    public void TheFirstTimeItOpensCentredOnTheMainScreen() =>
        Assert.Equal(new Box(222, 66, 1475, 900), WindowPlacement.Fit(null, null, Primary, 1475, 900));

    [Fact]
    public void ItNeverOpensTallerOrWiderThanTheScreen() =>
        Assert.Equal(new Box(0, 0, 1366, 728),
            WindowPlacement.Fit(null, null, new Box(0, 0, 1366, 728), 1475, 1125));

    [Fact]
    public void ItGoesBackWhereItWasLeft()
    {
        var left = new Box(2100, 200, 1200, 900);
        Assert.Equal(left, WindowPlacement.Fit(left, Second, Primary, 1475, 900));
    }

    [Fact]
    public void AScreenThatHasGoneSendsItToTheMainOne() =>
        Assert.Equal(new Box(222, 66, 1475, 900),
            WindowPlacement.Fit(new Box(2100, 200, 1200, 900), null, Primary, 1475, 900));

    [Fact]
    public void AWindowHangingOffTheEdgeIsBroughtBackOn() =>
        Assert.Equal(new Box(720, 132, 1200, 900),
            WindowPlacement.Fit(new Box(1500, 400, 1200, 900), Primary, Primary, 1475, 900));

    [Fact]
    public void AWindowLeftOnABiggerScreenShrinksToFitASmallerOne() =>
        Assert.Equal(new Box(0, 0, 1920, 1032),
            WindowPlacement.Fit(new Box(-10, -10, 2400, 1300), Primary, Primary, 1475, 900));

    [Fact]
    public void AWindowBelongsToTheScreenHoldingItsCentre() =>
        Assert.Equal((2500, 650), WindowPlacement.Centre(new Box(1900, 200, 1200, 900)));
}
