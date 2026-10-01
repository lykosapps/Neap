using Neap.Core.Connection;
using Neap.Core.Mix;

namespace Neap.Core.Tests.Mix;

public class ChatWheelTests
{
    private static readonly TimeSpan Turning = TimeSpan.FromMilliseconds(100);

    private TimeSpan _now = TimeSpan.FromMinutes(1);

    private ChatWheel NewWheel() => new(() => _now);

    /// <summary>Read the wheel as someone turning it at a steady pace would.</summary>
    private WheelStep? Turn(ChatWheel wheel, int count)
    {
        _now += Turning;
        return wheel.Read(count);
    }

    /// <summary>Read the wheel after it has been left alone.</summary>
    private WheelStep? AfterRest(ChatWheel wheel, int count)
    {
        _now += TimeSpan.FromSeconds(30);
        return wheel.Read(count);
    }

    private static HeadsetStatus Status(Link link, bool noSound = false) =>
        new(link, Route.ChargingDock, "", "", NoSound: noSound);

    [Fact]
    public void TheFirstReadingIsAStartingPoint()
    {
        var wheel = NewWheel();
        Assert.Null(wheel.Read(45));
        Assert.Equal(new WheelStep(45, 50), Turn(wheel, 50));
    }

    [Fact]
    public void EachNotchIsAMovementFromTheLast()
    {
        var wheel = NewWheel();
        wheel.Read(50);
        Assert.Equal(new WheelStep(50, 55), Turn(wheel, 55));
        Assert.Equal(new WheelStep(55, 60), Turn(wheel, 60));
        Assert.Equal(new WheelStep(60, 55), Turn(wheel, 55));
    }

    [Fact]
    public void ANotchAfterRestMovesAsUsual()
    {
        var wheel = NewWheel();
        wheel.Read(50);
        Assert.Equal(new WheelStep(50, 45), AfterRest(wheel, 45));
    }

    [Fact]
    public void AFarReadingAfterRestMovesNothingOnItsOwn()
    {
        var wheel = NewWheel();
        wheel.Read(50);
        Assert.Null(AfterRest(wheel, 10));
        Assert.Equal(10, wheel.Doubted);
    }

    [Fact]
    public void AFarReadingConfirmedByTheNextBecomesTheStartingPoint()
    {
        var wheel = NewWheel();
        wheel.Read(50);
        AfterRest(wheel, 10);
        Assert.Equal(new WheelStep(10, 15), Turn(wheel, 15));
        Assert.Null(wheel.Doubted);
    }

    [Fact]
    public void AStrayReadingIsDropped()
    {
        var wheel = NewWheel();
        wheel.Read(50);
        AfterRest(wheel, 10);
        Assert.Equal(new WheelStep(50, 55), Turn(wheel, 55));
    }

    [Fact]
    public void AReadingFarFromBothIsANewStartingPoint()
    {
        var wheel = NewWheel();
        wheel.Read(50);
        AfterRest(wheel, 10);
        Assert.Null(Turn(wheel, 90));
        Assert.Equal(new WheelStep(90, 85), Turn(wheel, 85));
    }

    [Fact]
    public void AFarReadingWhileTurningIsAMovement()
    {
        var wheel = NewWheel();
        wheel.Read(50);
        Turn(wheel, 55);
        Assert.Equal(new WheelStep(55, 75), Turn(wheel, 75));
    }

    [Fact]
    public void ReadingsArrivingTogetherEachMove()
    {
        var wheel = NewWheel();
        wheel.Read(50);
        Turn(wheel, 55);
        Assert.Equal(new WheelStep(55, 60), wheel.Read(60));
        Assert.Equal(new WheelStep(60, 65), wheel.Read(65));
    }

    [Fact]
    public void AfterTheLinkGoesTheNextReadingIsAStartingPoint()
    {
        var wheel = NewWheel();
        wheel.Read(45);
        wheel.Link(Status(Link.Quiet));
        wheel.Link(Status(Link.Connected));
        Assert.Null(Turn(wheel, 0));
        Assert.Equal(new WheelStep(0, 5), Turn(wheel, 5));
    }

    [Fact]
    public void AfterNoSoundTheFirstCountToDifferIsAStartingPoint()
    {
        var wheel = NewWheel();
        wheel.Read(50);
        wheel.Link(Status(Link.Connected, noSound: true));
        wheel.Link(Status(Link.Connected));
        Assert.Null(Turn(wheel, 50));
        Assert.Null(Turn(wheel, 70));
        Assert.Equal(new WheelStep(70, 75), Turn(wheel, 75));
    }

    [Theory]
    [InlineData(50, 50, 55, 55)]
    [InlineData(50, 50, 45, 45)]
    [InlineData(20, 10, 0, 0)]
    [InlineData(80, 90, 100, 100)]
    [InlineData(20, 0, 0, 20)]
    [InlineData(80, 100, 100, 80)]
    [InlineData(60, 50, 45, 54)]
    public void EachStepCoversTheSameShareOfWhatIsLeft(int mix, int from, int to, int next) =>
        Assert.Equal(next, ChatWheel.Follow(mix, new WheelStep(from, to)));

    [Theory]
    [InlineData(0, 5, 55)]
    [InlineData(100, 95, 45)]
    public void FromCentreANotchMovesAWholeNotch(int from, int to, int next) =>
        Assert.Equal(next, ChatWheel.Follow(50, new WheelStep(from, to)));

    [Theory]
    [InlineData(5)]
    [InlineData(-5)]
    public void TheFirstNotchLeavesTheCentreDetentWhereverTheCountIs(int notch)
    {
        // The count reads 0 after the headset is switched on, so turning toward
        // chat from there covers the smallest share of what is left.
        int count = notch > 0 ? 0 : 100;
        var snap = MixDetent.Apply(ChatWheel.Follow(50, new WheelStep(count, count + notch)), previous: 50, held: true);

        Assert.NotEqual(50, snap.Value);
        Assert.False(snap.Held);
    }
}
