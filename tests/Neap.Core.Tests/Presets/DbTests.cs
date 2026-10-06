using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class DbTests
{
    [Theory]
    [InlineData(45, "+4.5")]
    [InlineData(-120, "−12.0")]
    [InlineData(0, "0.0")]
    [InlineData(5, "+0.5")]
    public void ABandIsShownInSignedDecibels(int tenths, string shown) =>
        Assert.Equal(shown, Db.Text(tenths));

    [Theory]
    [InlineData("3", 3.0)]
    [InlineData("3.5", 3.5)]
    [InlineData("-3", -3.0)]
    [InlineData("−3", -3.0)]
    [InlineData("+3 dB", 3.0)]
    [InlineData("3,5", 3.5)]
    public void AValueIsReadHoweverItWasTyped(string typed, double db) =>
        Assert.Equal(db, Db.Parse(typed));

    [Theory]
    [InlineData("")]
    [InlineData("loud")]
    public void ThingsThatAreNotValuesAreRefused(string typed) =>
        Assert.Null(Db.Parse(typed));
}
