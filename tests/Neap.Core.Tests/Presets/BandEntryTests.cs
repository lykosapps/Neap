using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class BandEntryTests
{
    [Theory]
    [InlineData("3", 3)]
    [InlineData("+3", 3)]
    [InlineData("-2.5", -2.5)]
    [InlineData("-2,5", -2.5)]
    [InlineData("4 dB", 4)]
    [InlineData("  -1.25dB ", -1.3)]
    [InlineData("+20", 12)]
    [InlineData("-40", -12)]
    public void AGainIsReadLeniently(string typed, double expected) =>
        Assert.Equal(expected, BandEntry.Gain(typed)!.Value, 6);

    [Theory]
    [InlineData("1.4", 1.4)]
    [InlineData("1,4", 1.4)]
    [InlineData("Q 0.7", 0.7)]
    [InlineData("50", 10)]
    [InlineData("0.01", 0.1)]
    public void AWidthIsReadLeniently(string typed, double expected) =>
        Assert.Equal(expected, BandEntry.Q(typed)!.Value, 6);

    [Theory]
    [InlineData("")]
    [InlineData("loud")]
    [InlineData("dB")]
    public void WhatHoldsNoNumberIsNotAReading(string typed)
    {
        Assert.Null(BandEntry.Gain(typed));
        Assert.Null(BandEntry.Q(typed));
    }
}
