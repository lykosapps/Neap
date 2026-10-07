using Neap.Core.Connection;

namespace Neap.Core.Tests.Connection;

public class BatteryTests
{
    [Theory]
    [InlineData(Link.Connected, Route.ChargingDock, true)]
    [InlineData(Link.Quiet, Route.DirectUsb, true)]
    [InlineData(Link.Quiet, Route.ChargingDock, false)]
    [InlineData(Link.Silent, Route.UsbTransmitter, false)]
    [InlineData(Link.Connecting, Route.Unknown, false)]
    [InlineData(Link.Absent, Route.Unknown, false)]
    public void OnlyARealReadingIsShown(Link link, Route route, bool shown) =>
        Assert.Equal(shown, Battery.Of(new HeadsetStatus(link, route, "", ""), 76, 0) is not null);

    [Fact]
    public void NoReportNoReading() =>
        Assert.Null(Battery.Of(new HeadsetStatus(Link.Connected, Route.ChargingDock, "", ""), null, 1));

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(null, false)]
    public void SaysWhetherItIsCharging(int? power, bool charging) =>
        Assert.Equal(new BatteryReading(76, charging),
            Battery.Of(new HeadsetStatus(Link.Connected, Route.ChargingDock, "", ""), 76, power));

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(null, true, true)]
    [InlineData(1, true, false)]
    [InlineData(0, false, false)]
    public void ASettlingReadingIsOnlyAFallOffPower(int? power, bool falling, bool settling) =>
        Assert.Equal(settling,
            Battery.Of(new HeadsetStatus(Link.Connected, Route.ChargingDock, "", ""), 35, power, falling)!.Value.Settling);
}

public class BatteryTrendTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);

    private static BatteryTrend Of(params (int Minutes, int Percent)[] readings)
    {
        var trend = new BatteryTrend();
        foreach (var (minutes, percent) in readings) trend.Add(Start.AddMinutes(minutes), percent);
        return trend;
    }

    [Fact]
    public void NothingSeenIsNotSettling() =>
        Assert.False(new BatteryTrend().IsSettling(Start));

    [Fact]
    public void ASteadyReadingIsNotSettling() =>
        Assert.False(Of((0, 80), (1, 80), (4, 80)).IsSettling(Start.AddMinutes(4)));

    [Fact]
    public void TheFallAfterABatteryGoesInIsSettling() =>
        Assert.True(Of((0, 49), (1, 46), (3, 35), (4, 32)).IsSettling(Start.AddMinutes(4)));

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void MoreThanTwoPointsInFiveMinutesIsAFall(int points, bool settling) =>
        Assert.Equal(settling, Of((0, 60), (4, 60 - points)).IsSettling(Start.AddMinutes(4)));

    [Fact]
    public void OrdinaryDrainIsNotSettling() =>
        Assert.False(Of((0, 60), (12, 59), (24, 58), (36, 57)).IsSettling(Start.AddMinutes(36)));

    [Fact]
    public void ARiseIsNotSettling() =>
        Assert.False(Of((0, 40), (2, 44), (4, 49)).IsSettling(Start.AddMinutes(4)));

    [Fact]
    public void AFallEndsOnceItIsOlderThanTheWindow()
    {
        var trend = Of((0, 49), (1, 35));
        Assert.True(trend.IsSettling(Start.AddMinutes(2)));
        Assert.False(trend.IsSettling(Start.AddMinutes(7)));
    }

    [Fact]
    public void ForgettingEndsIt()
    {
        var trend = Of((0, 49), (1, 35));
        trend.Reset();
        Assert.False(trend.IsSettling(Start.AddMinutes(1)));
    }
}
