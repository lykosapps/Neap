using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class GainFollowTests
{
    private static ParametricBand Bell(double hertz, double gain = 0, bool inUse = true) =>
        new(BandType.Peaking, hertz, 1.2, gain, inUse);

    private static ParametricBand[] Mine() =>
        [.. new double[] { 31, 62, 125, 250, 500, 3000, 2000, 4000, 8000, 16000 }.Select(hertz => Bell(hertz, 1))];

    private static double[] Gains(double gain = 1) => [.. Enumerable.Repeat(gain, 10)];

    [Fact]
    public void NothingChangesWhenTheHeadsetStillReportsWhatWasSeen()
    {
        var result = GainFollow.Follow(Mine(), Gains(), Gains());
        Assert.False(result.Changed);
        Assert.Equal(Mine(), result.Bands);
    }

    [Fact]
    public void ATinyDifferenceIsNotAChange()
    {
        var reported = Gains();
        reported[3] = 1.04;
        Assert.False(GainFollow.Follow(Mine(), Gains(), reported).Changed);
    }

    [Fact]
    public void AChangedGainIsTakenAndTheRestOfTheBandIsKept()
    {
        var reported = Gains();
        reported[5] = -2.5;
        var result = GainFollow.Follow(Mine(), Gains(), reported);

        Assert.Equal([5], result.ChangedBands);
        Assert.Equal(-2.5, result.Bands[5].Gain);
        Assert.Equal(3000, result.Bands[5].Hertz);
        Assert.Equal(1.2, result.Bands[5].Q);
        Assert.Equal(BandType.Peaking, result.Bands[5].Type);
        Assert.Equal(1, result.Bands[4].Gain);
    }

    [Fact]
    public void AGainPastTheLimitIsHeldToIt()
    {
        var reported = Gains();
        reported[0] = 40;
        Assert.Equal(ParametricBand.MostGain, GainFollow.Follow(Mine(), Gains(), reported).Bands[0].Gain);
    }

    [Fact]
    public void ABandThatIsOffIsLeftAloneBecauseItsGainIsNotHeard()
    {
        var mine = Mine();
        mine[2] = mine[2] with { InUse = false };
        var reported = Gains();
        reported[2] = 6;
        var result = GainFollow.Follow(mine, Gains(), reported);
        Assert.False(result.Changed);
        Assert.Equal(1, result.Bands[2].Gain);
    }

    [Fact]
    public void ACutHasNoGainToFollow()
    {
        var mine = Mine();
        mine[1] = new ParametricBand(BandType.LowCut, 80, 0.7, 0, true);
        var reported = Gains();
        reported[1] = 6;
        var result = GainFollow.Follow(mine, Gains(), reported);
        Assert.False(result.Changed);
        Assert.Equal(0, result.Bands[1].Gain);
    }

    [Fact]
    public void SeveralChangesAreAllTaken()
    {
        var reported = Gains();
        reported[0] = 3;
        reported[9] = -3;
        Assert.Equal([0, 9], GainFollow.Follow(Mine(), Gains(), reported).ChangedBands);
    }

    [Fact]
    public void AnythingButTenOfEachIsRefused()
    {
        Assert.Throws<ArgumentException>(() => GainFollow.Follow(Mine()[..9], Gains(), Gains()));
        Assert.Throws<ArgumentException>(() => GainFollow.Follow(Mine(), Gains()[..9], Gains()));
        Assert.Throws<ArgumentException>(() => GainFollow.Follow(Mine(), Gains(), Gains()[..9]));
    }
}
