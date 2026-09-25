using Neap.Core.Audio;

namespace Neap.Core.Tests.Audio;

public sealed class RingRefreshTests
{
    private static readonly DateTime Start = new(2026, 9, 25, 7, 0, 0, DateTimeKind.Utc);

    private static DateTime At(double seconds) => Start.AddSeconds(seconds);

    [Fact]
    public void ResetsOnceTheMicrophoneHasBeenOpenAMoment()
    {
        var refresh = new RingRefresh();
        Assert.False(refresh.Next(micOpen: false, wanted: true, At(0)));
        Assert.False(refresh.Next(micOpen: true, wanted: true, At(1)));
        Assert.False(refresh.Next(micOpen: true, wanted: true, At(2)));
        Assert.True(refresh.Next(micOpen: true, wanted: true, At(3)));
    }

    [Fact]
    public void ResetsOnlyOnceForOneOpening()
    {
        var refresh = new RingRefresh();
        refresh.Next(micOpen: true, wanted: true, At(0));
        Assert.True(refresh.Next(micOpen: true, wanted: true, At(2)));
        for (int second = 3; second < 60; second++)
            Assert.False(refresh.Next(micOpen: true, wanted: true, At(second)));
    }

    [Fact]
    public void DoesNothingWhenTheMicrophoneClosesBeforeItSettles()
    {
        var refresh = new RingRefresh();
        refresh.Next(micOpen: true, wanted: true, At(0));
        Assert.False(refresh.Next(micOpen: false, wanted: true, At(1)));
        Assert.False(refresh.Next(micOpen: false, wanted: true, At(5)));
    }

    [Fact]
    public void DoesNothingWhileNotWanted()
    {
        var refresh = new RingRefresh();
        for (int second = 0; second < 10; second++)
            Assert.False(refresh.Next(micOpen: true, wanted: false, At(second)));
    }

    [Fact]
    public void ResetsWhenWantedWithTheMicrophoneAlreadyOpen()
    {
        // Switched on during a call, or sound moved to the dock mid-call.
        var refresh = new RingRefresh();
        refresh.Next(micOpen: true, wanted: false, At(0));
        refresh.Next(micOpen: true, wanted: true, At(1));
        Assert.True(refresh.Next(micOpen: true, wanted: true, At(3)));
    }

    [Fact]
    public void TakesAnOpeningStraightAfterAResetAsTheResetItself()
    {
        var refresh = new RingRefresh();
        refresh.Next(micOpen: true, wanted: true, At(0));
        Assert.True(refresh.Next(micOpen: true, wanted: true, At(2)));
        // The reset restarts the device's audio, and the stream reopens.
        refresh.Next(micOpen: false, wanted: true, At(3));
        refresh.Next(micOpen: true, wanted: true, At(4));
        for (int second = 5; second < 20; second++)
            Assert.False(refresh.Next(micOpen: true, wanted: true, At(second)));
    }

    [Fact]
    public void ResetsForTheNextCallOnceThingsAreQuiet()
    {
        var refresh = new RingRefresh();
        refresh.Next(micOpen: true, wanted: true, At(0));
        Assert.True(refresh.Next(micOpen: true, wanted: true, At(2)));
        refresh.Next(micOpen: false, wanted: true, At(30));
        refresh.Next(micOpen: true, wanted: true, At(40));
        Assert.True(refresh.Next(micOpen: true, wanted: true, At(42)));
    }
}
