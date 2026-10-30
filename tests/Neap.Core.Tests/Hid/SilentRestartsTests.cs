using Neap.Core.Hid;

namespace Neap.Core.Tests.Hid;

public class SilentRestartsTests
{
    [Fact]
    public void ASilentDeviceIsRestartedOnce()
    {
        var restarts = new SilentRestarts();

        Assert.Equal(["/dev/hidraw0"], restarts.Due(["/dev/hidraw0"], ["/dev/hidraw0"]));
        Assert.Empty(restarts.Due(["/dev/hidraw0"], ["/dev/hidraw0"]));
    }

    [Fact]
    public void ADeviceRestartedSaysSoUntilItIsPluggedInAgain()
    {
        var restarts = new SilentRestarts();
        restarts.Due(["/dev/hidraw0"], ["/dev/hidraw0"]);

        Assert.True(restarts.Restarted("/dev/hidraw0"));
        restarts.Due([], []);
        Assert.False(restarts.Restarted("/dev/hidraw0"));
    }

    [Fact]
    public void ADevicePluggedInAgainCanBeRestartedAgain()
    {
        var restarts = new SilentRestarts();
        restarts.Due(["/dev/hidraw0"], ["/dev/hidraw0"]);

        restarts.Due([], []);

        Assert.Equal(["/dev/hidraw0"], restarts.Due(["/dev/hidraw0"], ["/dev/hidraw0"]));
    }

    [Fact]
    public void ARestartThatCouldNotBeTriedIsDueAgain()
    {
        var restarts = new SilentRestarts();
        restarts.Due(["/dev/hidraw0"], ["/dev/hidraw0"]);

        restarts.Retry("/dev/hidraw0");

        Assert.Equal(["/dev/hidraw0"], restarts.Due(["/dev/hidraw0"], ["/dev/hidraw0"]));
    }

    [Fact]
    public void EachSilentDeviceIsRestartedOnItsOwnAccount()
    {
        var restarts = new SilentRestarts();
        restarts.Due(["/dev/hidraw0"], ["/dev/hidraw0", "/dev/hidraw1"]);

        Assert.Equal(["/dev/hidraw1"], restarts.Due(["/dev/hidraw0", "/dev/hidraw1"], ["/dev/hidraw0", "/dev/hidraw1"]));
    }

    [Theory]
    [InlineData(LinkAnswer.None, true)]
    [InlineData(LinkAnswer.Linked, true)]
    [InlineData(LinkAnswer.NotLinked, false)]
    [InlineData(LinkAnswer.Unreadable, false)]
    public void OnlyNoAnswerOrLinkedAndSilentIsRestarted(LinkAnswer answer, bool restart) =>
        Assert.Equal(restart, SilentRestarts.Needed(answer, ruleOutOfDate: false));

    [Theory]
    [InlineData(LinkAnswer.None, false)]
    [InlineData(LinkAnswer.Linked, true)]
    [InlineData(LinkAnswer.NotLinked, false)]
    [InlineData(LinkAnswer.Unreadable, false)]
    public void AStoppedChannelUnderAnOlderRuleWaitsForTheNewOne(LinkAnswer answer, bool restart) =>
        Assert.Equal(restart, SilentRestarts.Needed(answer, ruleOutOfDate: true));

    [Theory]
    [InlineData(LinkAnswer.None, true)]
    [InlineData(LinkAnswer.Linked, false)]
    [InlineData(LinkAnswer.NotLinked, false)]
    [InlineData(LinkAnswer.Unreadable, false)]
    public void OnlyNoAnswerAtAllIsAStoppedChannel(LinkAnswer answer, bool stopped) =>
        Assert.Equal(stopped, SilentRestarts.ChannelStopped(answer));
}
