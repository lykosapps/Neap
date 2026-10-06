using Neap.Core.Updates;

namespace Neap.Core.Tests.Updates;

public class RestartWatchTests
{
    [Fact]
    public void AVersionStillRunningWhenTheWindowEndsHasStarted()
    {
        Assert.Equal(RestartOutcome.Started, RestartWatch.Of(null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    public void AVersionThatExitsInsideTheWindowDidNotStart(int seconds)
    {
        Assert.Equal(RestartOutcome.DidNotStart, RestartWatch.Of(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void AVersionThatCouldNotBeLaunchedDidNotStart()
    {
        Assert.Equal(RestartOutcome.DidNotStart, RestartWatch.Of(TimeSpan.Zero));
    }

    [Fact]
    public void AnExitAtTheEdgeOfTheWindowIsNotCountedAgainstIt()
    {
        // A version that has run for the whole window and then exits, someone
        // closing it say, has started.
        Assert.Equal(RestartOutcome.Started, RestartWatch.Of(RestartWatch.Window));
    }
}
