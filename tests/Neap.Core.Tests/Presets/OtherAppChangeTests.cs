using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class OtherAppChangeTests
{
    private static double[] Gains(double gain = 1) => [.. Enumerable.Repeat(gain, 10)];

    [Fact]
    public void TheSameGainsAreNoChange() =>
        Assert.False(OtherAppChange.Changed(Gains(), Gains()));

    [Fact]
    public void ATinyDifferenceIsNoChange()
    {
        var reported = Gains();
        reported[3] = 1.04;
        Assert.False(OtherAppChange.Changed(Gains(), reported));
    }

    [Fact]
    public void AnyBandMovedIsAChange()
    {
        var reported = Gains();
        reported[9] = -9;
        Assert.True(OtherAppChange.Changed(Gains(), reported));
    }

    [Fact]
    public void NothingSeenYetIsNoChange() =>
        Assert.False(OtherAppChange.Changed([], Gains()));
}
