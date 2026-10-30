using Neap.Core.Settings;

namespace Neap.Core.Tests.Settings;

public class WriteHoldTests
{
    private const long Length = 1500;

    [Fact]
    public void AReportDuringTheHoldIsIgnored()
    {
        var hold = new WriteHold(Length);
        hold.Hold("401", now: 0);

        Assert.False(hold.Takes("401", now: 1499, authoritative: false));
    }

    [Fact]
    public void AReportAfterTheHoldIsTakenAndEndsIt()
    {
        var hold = new WriteHold(Length);
        hold.Hold("401", now: 0);

        Assert.True(hold.Takes("401", now: 1500, authoritative: false));
        Assert.True(hold.IsEmpty);
    }

    [Fact]
    public void AFullReadOverridesTheHold()
    {
        var hold = new WriteHold(Length);
        hold.Hold("401", now: 0);

        Assert.True(hold.Takes("401", now: 10, authoritative: true));
        Assert.True(hold.Takes("401", now: 20, authoritative: false));
    }

    [Fact]
    public void AHoldCoversOnlyItsOwnKey()
    {
        var hold = new WriteHold(Length);
        hold.Hold("401", now: 0);

        Assert.True(hold.Takes("402", now: 10, authoritative: false));
        Assert.False(hold.Takes("401", now: 10, authoritative: false));
    }

    [Fact]
    public void ASetWithoutAHoldReleasesAnEarlierOne()
    {
        var hold = new WriteHold(Length);
        hold.Hold("401", now: 0);
        hold.Release("401");

        Assert.True(hold.Takes("401", now: 10, authoritative: false));
    }

    [Fact]
    public void ANewSetRestartsTheHold()
    {
        var hold = new WriteHold(Length);
        hold.Hold("401", now: 0);
        hold.Hold("401", now: 1000);

        Assert.False(hold.Takes("401", now: 2000, authoritative: false));
    }
}
