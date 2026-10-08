using Neap.Core.Mix;

namespace Neap.Core.Tests.Mix;

public class MixDetentTests
{
    private TimeSpan _now = TimeSpan.FromSeconds(10);

    private MixDetent Detent() => new(() => _now);

    [Fact]
    public void TheFirstMoveIntoTheWidthSnapsAndCues()
    {
        var snap = Detent().Apply(50, previous: null, wheel: false);

        Assert.Equal(new MixSnap(50, Cue: true), snap);
    }

    [Fact]
    public void ANotchThatCrossesCentreSnapsAndCues()
    {
        // 45 to 55: either side of 50, ten points apart, well inside a wheel notch.
        var snap = Detent().Apply(55, previous: 45, wheel: false);

        Assert.Equal(new MixSnap(50, Cue: true), snap);
    }

    [Fact]
    public void WhileHeldFurtherMovesInsideTheWidthStaySilent()
    {
        var detent = Detent();
        detent.Apply(50, previous: 45, wheel: false);

        Assert.Equal(new MixSnap(50, Cue: false), detent.Apply(52, previous: 50, wheel: false));
    }

    [Fact]
    public void MovingPastTheWidthReleasesTheHold()
    {
        var detent = Detent();
        detent.Apply(50, previous: 45, wheel: false);

        Assert.Equal(new MixSnap(70, Cue: false), detent.Apply(70, previous: 50, wheel: false));
        Assert.Equal(new MixSnap(50, Cue: true), detent.Apply(48, previous: 70, wheel: false));
    }

    [Fact]
    public void ADialDraggedAcrossTheWholeRangePassesThroughCentre()
    {
        // Far more than one notch, so this is a drag, not a step: never held.
        var snap = Detent().Apply(80, previous: 20, wheel: false);

        Assert.Equal(new MixSnap(80, Cue: false), snap);
    }

    [Fact]
    public void AWheelTurnedFastIsCaughtAtCentreHoweverFarItJumps()
    {
        // Measured: turned fast, the wheel jumped from 40 straight to 70.
        var snap = Detent().Apply(70, previous: 40, wheel: true);

        Assert.Equal(new MixSnap(50, Cue: true), snap);
    }

    [Fact]
    public void TheWheelStaysAtCentreForAMomentAfterItIsCaught()
    {
        var detent = Detent();
        detent.Apply(70, previous: 40, wheel: true);
        _now += MixDetent.Stick - TimeSpan.FromMilliseconds(1);

        Assert.Equal(new MixSnap(50, Cue: false), detent.Apply(80, previous: 50, wheel: true));
    }

    [Fact]
    public void TurningOnPastTheStickMovesTheMixOn()
    {
        var detent = Detent();
        detent.Apply(70, previous: 40, wheel: true);
        _now += MixDetent.Stick;

        Assert.Equal(new MixSnap(80, Cue: false), detent.Apply(80, previous: 50, wheel: true));
    }

    [Fact]
    public void TheDialIsNotHeldByTheStick()
    {
        var detent = Detent();
        detent.Apply(70, previous: 40, wheel: true);

        Assert.Equal(new MixSnap(80, Cue: false), detent.Apply(80, previous: 50, wheel: false));
    }

    [Fact]
    public void TheFirstReadingHasNothingToCrossFrom()
    {
        // Not near 50, and there is no previous value to have crossed from.
        var snap = Detent().Apply(70, previous: null, wheel: true);

        Assert.Equal(new MixSnap(70, Cue: false), snap);
    }
}
