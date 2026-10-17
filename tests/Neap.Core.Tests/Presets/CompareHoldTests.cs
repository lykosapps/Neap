using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class CompareHoldTests
{
    private static readonly int[] Curve = [10, -20, 0, 35, 0, 0, 5, -15, 0, 0];

    [Fact]
    public void PressingAsksForFlatAndKeepsTheCurve()
    {
        var hold = new CompareHold();

        var flat = hold.Press(Curve);

        Assert.Equal(new int[10], flat);
        Assert.True(hold.Held);
        Assert.True(hold.Owes);
    }

    [Fact]
    public void ReleasingAsksForTheCurveBack()
    {
        var hold = new CompareHold();
        hold.Press(Curve);

        var back = hold.Release();

        Assert.Equal(Curve, back);
        Assert.False(hold.Held);
        Assert.False(hold.Owes);
    }

    [Fact]
    public void PressingTwiceDoesNotTakeTheFlatBandsForTheCurve()
    {
        var hold = new CompareHold();
        hold.Press(Curve);

        Assert.Null(hold.Press(new int[10]));

        Assert.Equal(Curve, hold.Release());
    }

    [Fact]
    public void ReleasingWithoutPressingAsksForNothing()
    {
        var hold = new CompareHold();

        Assert.Null(hold.Release());
        Assert.False(hold.Held);
    }

    [Fact]
    public void ReleasingTwiceWritesTheCurveOnce()
    {
        var hold = new CompareHold();
        hold.Press(Curve);
        hold.Release();

        Assert.Null(hold.Release());
    }

    [Fact]
    public void AFailedRestoreOwesTheCurveUntilTheNextReleaseWritesIt()
    {
        var hold = new CompareHold();
        hold.Press(Curve);
        hold.Release();

        hold.RestoreFailed(Curve);

        Assert.True(hold.Owes);
        Assert.False(hold.Held);
        Assert.Equal(Curve, hold.Release());
        Assert.False(hold.Owes);
    }

    [Fact]
    public void PressingAfterAFailedRestoreStillOwesTheOriginalCurveNotTheFlatOne()
    {
        var hold = new CompareHold();
        hold.Press(Curve);
        hold.RestoreFailed(hold.Release()!);

        hold.Press(new int[10]);

        Assert.Equal(Curve, hold.Release());
    }

    [Fact]
    public void AFailedFlatWriteHoldsNothingButStillOwesTheCurve()
    {
        var hold = new CompareHold();
        hold.Press(Curve);

        hold.PressFailed();

        Assert.False(hold.Held);
        Assert.True(hold.Owes);
        Assert.Equal(Curve, hold.Release());
    }

    [Fact]
    public void ACurveThatIsAlreadyFlatStillRestoresToFlat()
    {
        var hold = new CompareHold();
        hold.Press(new int[10]);

        Assert.Equal(new int[10], hold.Release());
    }
}
