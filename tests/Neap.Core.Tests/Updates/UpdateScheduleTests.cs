using Neap.Core.Updates;

namespace Neap.Core.Tests.Updates;

public class UpdateScheduleTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NeverCheckedIsDue() => Assert.True(UpdateSchedule.Due(null, Noon));

    [Fact]
    public void ACheckWithinTheDayHoldsOffTheNext()
    {
        Assert.False(UpdateSchedule.Due(Noon, Noon.AddHours(23)));
    }

    [Fact]
    public void ADayAfterTheLastCheckIsDue()
    {
        Assert.True(UpdateSchedule.Due(Noon, Noon.AddDays(1)));
    }

    [Fact]
    public void ACheckDatedAfterNowIsDue()
    {
        // The clock was moved back since.
        Assert.True(UpdateSchedule.Due(Noon.AddDays(30), Noon));
    }
}
