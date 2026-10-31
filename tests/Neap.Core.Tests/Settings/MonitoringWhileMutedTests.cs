using Neap.Core.Settings;

namespace Neap.Core.Tests.Settings;

public class MonitoringWhileMutedTests
{
    [Fact]
    public void MutingTurnsMonitoringDown() => Assert.Equal(0, MonitoringWhileMuted.LevelFor(muted: true, now: 60, before: null));

    [Fact]
    public void MutingKeepsTheLevelForUnmuting() => Assert.Equal(60, MonitoringWhileMuted.KeptAfter(muted: true, now: 60, before: null));

    [Fact]
    public void MonitoringAlreadyOffIsLeftAndNothingIsKept()
    {
        Assert.Null(MonitoringWhileMuted.LevelFor(muted: true, now: 0, before: null));
        Assert.Null(MonitoringWhileMuted.KeptAfter(muted: true, now: 0, before: null));
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
    public void UnmutingBeforeTheHeadsetReportsMonitoringKeepsTheLevelForItsReading()
    {
        Assert.Null(MonitoringWhileMuted.LevelFor(muted: false, now: null, before: 60));
        Assert.Equal(60, MonitoringWhileMuted.KeptAfter(muted: false, now: null, before: 60));
    }

    [Fact]
    public void OnceTheLevelIsPutBackNothingIsKept() =>
        Assert.Null(MonitoringWhileMuted.KeptAfter(muted: false, now: 0, before: 60));

    [Fact]
    public void MutingAgainKeepsTheLevelAlreadyKept() =>
        Assert.Equal(60, MonitoringWhileMuted.KeptAfter(muted: true, now: null, before: 60));

    [Fact]
    public void OnlyTheAtlasAirQuietsMonitoringWhileMuted()
    {
        Assert.True(HeadsetModels.AtlasAir.QuietsMonitoringWhileMuted);
        Assert.False(HeadsetModels.StealthProII.QuietsMonitoringWhileMuted);
    }
}
