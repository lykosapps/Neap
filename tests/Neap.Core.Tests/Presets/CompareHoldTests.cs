using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class CompareHoldTests
{
    private static readonly int[] Curve = [10, -20, 0, 35, 0, 0, 5, -15, 0, 0];

    [Fact]
    public void PressingAsksForFlatAndKeepsThePresetAndTheCurve()
    {
        var hold = new CompareHold();

        Assert.True(hold.Press(Curve, 16));

        Assert.True(hold.Held);
        Assert.True(hold.Owes);
    }

    [Fact]
    public void ReleasingAsksForThePresetAndTheCurveBack()
    {
        var hold = new CompareHold();
        hold.Press(Curve, 16);

        var back = hold.Release()!.Value;

        Assert.Equal(16, back.PresetId);
        Assert.Equal(Curve, back.Bands);
        Assert.False(hold.Held);
        Assert.False(hold.Owes);
    }

    [Fact]
    public void ACurveOfNoPresetIsOwedWithNoPreset()
    {
        var hold = new CompareHold();
        hold.Press(Curve, null);

        Assert.Null(hold.Release()!.Value.PresetId);
    }

    [Fact]
    public void PressingTwiceDoesNotTakeTheFlatBandsForTheCurve()
    {
        var hold = new CompareHold();
        hold.Press(Curve, 16);

        Assert.False(hold.Press(new int[10], 2));

        var back = hold.Release()!.Value;
        Assert.Equal(Curve, back.Bands);
        Assert.Equal(16, back.PresetId);
    }

    [Fact]
    public void ReleasingWithoutPressingOwesNothing()
    {
        var hold = new CompareHold();

        Assert.Null(hold.Release());
        Assert.False(hold.Held);
    }

    [Fact]
    public void ReleasingTwiceRestoresOnce()
    {
        var hold = new CompareHold();
        hold.Press(Curve, 16);
        hold.Release();

        Assert.Null(hold.Release());
    }

    [Fact]
    public void AFailedRestoreOwesTheCurveUntilTheNextReleaseTriesAgain()
    {
        var hold = new CompareHold();
        hold.Press(Curve, 16);
        var owed = hold.Release()!.Value;

        hold.RestoreFailed(owed);

        Assert.True(hold.Owes);
        Assert.False(hold.Held);
        Assert.Equal(owed, hold.Release()!.Value);
        Assert.False(hold.Owes);
    }

    [Fact]
    public void PressingAfterAFailedRestoreStillOwesTheOriginalNotTheFlatOne()
    {
        var hold = new CompareHold();
        hold.Press(Curve, 16);
        hold.RestoreFailed(hold.Release()!.Value);

        hold.Press(new int[10], 2);

        var back = hold.Release()!.Value;
        Assert.Equal(Curve, back.Bands);
        Assert.Equal(16, back.PresetId);
    }

    [Fact]
    public void AFailedFlatWriteHoldsNothingButStillOwesTheCurve()
    {
        var hold = new CompareHold();
        hold.Press(Curve, 16);

        hold.PressFailed();

        Assert.False(hold.Held);
        Assert.True(hold.Owes);
        Assert.Equal(Curve, hold.Release()!.Value.Bands);
    }

    [Fact]
    public void ACurveThatIsAlreadyFlatStillRestoresToFlat()
    {
        var hold = new CompareHold();
        hold.Press(new int[10], 2);

        Assert.Equal(new int[10], hold.Release()!.Value.Bands);
    }
}
