using Neap.Core.Settings;

namespace Neap.Core.Tests.Settings;

public class MonitoringWhileMutedTests
{
    [Fact]
    public void MutingTurnsMonitoringDown() => Assert.Equal(0, MonitoringWhileMuted.LevelFor(muted: true, now: 60, before: null));

    [Fact]
    public void MutingKeepsTheLevelForUnmuting() => Assert.Equal(60, MonitoringWhileMuted.KeptFor(60));

    [Fact]
    public void MonitoringAlreadyOffIsLeftAndNothingIsKept()
    {
        Assert.Null(MonitoringWhileMuted.LevelFor(muted: true, now: 0, before: null));
        Assert.Null(MonitoringWhileMuted.KeptFor(0));
    }

    [Fact]
    public void UnmutingPutsTheLevelBack() => Assert.Equal(60, MonitoringWhileMuted.LevelFor(muted: false, now: 0, before: 60));

    [Fact]
    public void ALevelSetWhileMutedIsThePersonsAndIsLeft() =>
        Assert.Null(MonitoringWhileMuted.LevelFor(muted: false, now: 30, before: 60));

    [Fact]
    public void UnmutingWithNothingKeptLeavesMonitoring() =>
        Assert.Null(MonitoringWhileMuted.LevelFor(muted: false, now: 0, before: null));

    [Fact]
    public void BeforeTheHeadsetReportsMonitoringNothingIsChanged() =>
        Assert.Null(MonitoringWhileMuted.LevelFor(muted: true, now: null, before: null));

    [Fact]
    public void OnlyTheAtlasAirQuietsMonitoringWhileMuted()
    {
        Assert.True(HeadsetModels.AtlasAir.QuietsMonitoringWhileMuted);
        Assert.False(HeadsetModels.StealthProII.QuietsMonitoringWhileMuted);
    }
}
