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
    [InlineData(null, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void OnlyATransmitterThatSaysNoHeadsetIsLinkedIsLeftAlone(bool? linked, bool restart) =>
        Assert.Equal(restart, SilentRestarts.Needed(linked, ruleOutOfDate: false));

    [Theory]
    [InlineData(null, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void AStoppedChannelUnderAnOlderRuleWaitsForTheNewOne(bool? linked, bool restart) =>
        Assert.Equal(restart, SilentRestarts.Needed(linked, ruleOutOfDate: true));

    [Theory]
    [InlineData(null, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void OnlyNoAnswerAtAllIsAStoppedChannel(bool? linked, bool stopped) =>
        Assert.Equal(stopped, SilentRestarts.ChannelStopped(linked));
}
