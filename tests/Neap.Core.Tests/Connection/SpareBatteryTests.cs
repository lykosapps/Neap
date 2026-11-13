using Neap.Core.Connection;

namespace Neap.Core.Tests.Connection;

public class SpareBatteryTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData("37", 37)]
    [InlineData("100", 100)]
    public void AChargeIsABatteryInTheSlot(string reported, int percent)
    {
        Assert.Equal(new SpareReading(SpareState.InSlot, percent), SpareBattery.Parse(reported));
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("57", true)]
    [InlineData("99", true)]
    [InlineData("100", false)]
    [InlineData("255", false)]
    [InlineData("half", false)]
    public void ABatteryBelowFullIsCharging(string reported, bool charging)
    {
        Assert.Equal(charging, SpareBattery.Parse(reported).Charging);
    }

    [Fact]
    public void TwoFiftyFiveIsAnEmptySlot()
    {
        Assert.Equal(SpareState.Empty, SpareBattery.Parse("255").State);
    }

    // GitHub issue 13: switched on with the slot empty, the dock reported 0 and Home said Charging.
    [Fact]
    public void ZeroIsAnEmptySlotNotABatteryCharging()
    {
        var reading = SpareBattery.Parse("0");
        Assert.Equal(SpareState.Empty, reading.State);
        Assert.False(reading.Charging);
    }

    [Theory]
    [InlineData("")]
    [InlineData("101")]
    [InlineData("254")]
    [InlineData("-1")]
    [InlineData(" 50")]
    [InlineData("half")]
    public void AnythingElseIsUnreadableNotEmpty(string reported)
    {
        Assert.Equal(SpareState.Unreadable, SpareBattery.Parse(reported).State);
    }
}
