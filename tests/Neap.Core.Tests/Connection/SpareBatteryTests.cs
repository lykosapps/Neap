using Neap.Core.Connection;

namespace Neap.Core.Tests.Connection;

public class SpareBatteryTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("37", 37)]
    [InlineData("100", 100)]
    public void AChargeIsABatteryInTheSlot(string reported, int percent)
    {
        Assert.Equal(new SpareReading(SpareState.InSlot, percent), SpareBattery.Parse(reported));
    }

    [Fact]
    public void TwoFiftyFiveIsAnEmptySlot()
    {
        Assert.Equal(SpareState.Empty, SpareBattery.Parse("255").State);
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
