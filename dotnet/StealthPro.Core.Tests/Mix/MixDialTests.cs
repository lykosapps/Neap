using StealthPro.Core.Mix;

namespace StealthPro.Core.Tests.Mix;

public class MixDialTests
{
    [Theory]
    [InlineData(0, -135)]
    [InlineData(50, 0)]
    [InlineData(100, 135)]
    [InlineData(-10, -135)]
    [InlineData(110, 135)]
    public void ThePointerRunsFromGameAtTheLeftToChatAtTheRight(int mix, double angle) =>
        Assert.Equal(angle, MixDial.AngleOf(mix), 6);

    [Fact]
    public void BalancedFillsBothHalvesToTheTop()
    {
        Assert.Equal(new DialArc(-135, 0), MixDial.GameArc(50));
        Assert.Equal(new DialArc(0, 135), MixDial.ChatArc(50));
    }

    [Fact]
    public void MovingTowardChatShortensOnlyTheGameArc()
    {
        Assert.Equal(135 * 0.5, MixDial.GameArc(75).Length, 6);
        Assert.Equal(135, MixDial.ChatArc(75).Length, 6);
    }

    [Fact]
    public void AnEndOfTheMixEmptiesTheOtherSide()
    {
        Assert.Equal(0, MixDial.ChatArc(0).Length);
        Assert.Equal(0, MixDial.GameArc(100).Length);
    }

    [Fact]
    public void OnlyTheTrackTurnsThroughMoreThanHalfACircle()
    {
        Assert.True(MixDial.Track.IsLarge);
        Assert.False(MixDial.GameArc(0).IsLarge);
        Assert.False(MixDial.ChatArc(100).IsLarge);
    }

    [Theory]
    [InlineData(0, -100, 50)]
    [InlineData(100, 0, 83)]
    [InlineData(-100, 0, 17)]
    [InlineData(-70.71, 70.71, 0)]
    [InlineData(70.71, 70.71, 100)]
    public void APointOnTheDialGivesTheMixUnderIt(double x, double y, int mix) =>
        Assert.Equal(mix, MixDial.MixAt(x, y));

    [Theory]
    [InlineData(-1, 100, 0)]
    [InlineData(1, 100, 100)]
    [InlineData(-30, 90, 0)]
    public void APointBelowTheDialGoesToTheNearerEnd(double x, double y, int mix) =>
        Assert.Equal(mix, MixDial.MixAt(x, y));

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(50)]
    [InlineData(83)]
    [InlineData(100)]
    public void APointDrawnForAMixReadsBackAsThatMix(int mix)
    {
        var (x, y) = MixDial.PointAt(MixDial.AngleOf(mix), 120);
        Assert.Equal(mix, MixDial.MixAt(x, y));
    }

    [Fact]
    public void PointsAreDrawnWithYGrowingDownward()
    {
        var (x, y) = MixDial.PointAt(0, 10);
        Assert.Equal(0, x, 6);
        Assert.Equal(-10, y, 6);
    }

    [Theory]
    [InlineData(0, -128, true)]
    [InlineData(128, 0, true)]
    [InlineData(-90.5, 90.5, true)]
    [InlineData(0, -40, false)]
    [InlineData(0, 128, false)]
    [InlineData(-30, 120, false)]
    [InlineData(0, -160, false)]
    public void OnlyAPressOnTheArcTakesHold(double x, double y, bool grabs) =>
        Assert.Equal(grabs, MixDial.Grabs(x, y, inner: 100, outer: 150));

    [Fact]
    public void TheSegmentsCoverTheRingWithGapsBetween()
    {
        Assert.True(MixDial.Segment(0).From > MixDial.GameEnd);
        Assert.True(MixDial.Segment(MixDial.SegmentCount - 1).To < MixDial.ChatEnd);
        Assert.True(MixDial.Segment(1).From > MixDial.Segment(0).To);
    }

    [Fact]
    public void BalancedLightsEverySegmentHalfEach()
    {
        var sides = Enumerable.Range(0, MixDial.SegmentCount).Select(i => MixDial.Lit(i, 50)).ToList();
        Assert.Equal(MixDial.SegmentCount / 2, sides.Count(s => s == DialSide.Game));
        Assert.Equal(MixDial.SegmentCount / 2, sides.Count(s => s == DialSide.Chat));
    }

    [Fact]
    public void TowardChatDarkensSomeOfTheGameSide()
    {
        var sides = Enumerable.Range(0, MixDial.SegmentCount).Select(i => MixDial.Lit(i, 75)).ToList();
        Assert.Equal(MixDial.SegmentCount / 2, sides.Count(s => s == DialSide.Chat));
        Assert.Contains(DialSide.None, sides);
        Assert.Equal(DialSide.Game, sides[0]);
    }

    [Theory]
    [InlineData(50, 0, 27, 60)]
    [InlineData(50, 0, -27, 40)]
    [InlineData(95, 0, 90, 100)]
    [InlineData(5, 0, -90, 0)]
    [InlineData(50, 170, -170, 57.4)]
    [InlineData(50, -170, 170, 42.6)]
    public void TurningTheKnobMovesTheMixByAsMuchAsItTurns(double mix, double from, double to, double after) =>
        Assert.Equal(after, MixDial.Turn(mix, from, to), 1);

    [Theory]
    [InlineData(0, -1, 0)]
    [InlineData(1, 0, 90)]
    [InlineData(-1, 0, -90)]
    public void AnglesRunClockwiseFromTwelve(double x, double y, double angle) =>
        Assert.Equal(angle, MixDial.AngleAt(x, y), 6);
}
