using Neap.Core.Mix;

namespace Neap.Core.Tests.Mix;

public class MixDetentTests
{
    [Fact]
    public void TheFirstMoveIntoTheWidthSnapsAndCues()
    {
        var snap = MixDetent.Apply(50, previous: null, held: false);

        Assert.Equal(50, snap.Value);
        Assert.True(snap.Cue);
        Assert.True(snap.Held);
    }

    [Fact]
    public void ANotchThatCrossesCentreSnapsAndCues()
    {
        // 45 to 55: either side of 50, ten points apart, well inside a wheel notch.
        var snap = MixDetent.Apply(55, previous: 45, held: false);

        Assert.Equal(50, snap.Value);
        Assert.True(snap.Cue);
        Assert.True(snap.Held);
    }

    [Fact]
    public void WhileHeldFurtherMovesInsideTheWidthStaySilent()
    {
        var snap = MixDetent.Apply(52, previous: 50, held: true);

        Assert.Equal(50, snap.Value);
        Assert.False(snap.Cue);
        Assert.True(snap.Held);
    }

    [Fact]
    public void MovingPastTheWidthReleasesTheHold()
    {
        var snap = MixDetent.Apply(70, previous: 50, held: true);

        Assert.Equal(70, snap.Value);
        Assert.False(snap.Cue);
        Assert.False(snap.Held);
    }

    [Fact]
    public void ADialDraggedAcrossTheWholeRangePassesThroughCentre()
    {
        // Far more than one notch, so this is a drag, not a step: never held.
        var snap = MixDetent.Apply(80, previous: 20, held: false);

        Assert.Equal(80, snap.Value);
        Assert.False(snap.Cue);
        Assert.False(snap.Held);
    }

    [Fact]
    public void TheFirstReadingHasNothingToCrossFrom()
    {
        // Not near 50, and there is no previous value to have crossed from.
        var snap = MixDetent.Apply(70, previous: null, held: false);

        Assert.Equal(70, snap.Value);
        Assert.False(snap.Cue);
        Assert.False(snap.Held);
    }
}
