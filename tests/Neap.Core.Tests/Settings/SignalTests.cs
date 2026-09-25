using Neap.Core.Settings;

namespace Neap.Core.Tests.Settings;

public class SignalTests
{
    [Theory]
    [InlineData(-50, -50)]
    [InlineData(206, -50)]
    [InlineData(0, 0)]
    [InlineData(127, 127)]
    public void ReadsTheSameDbmSignedOrUnsigned(int raw, int dbm) =>
        Assert.Equal(dbm, Signal.Dbm(raw));

    [Theory]
    [InlineData(-55, SignalStrength.Strong)]
    [InlineData(-56, SignalStrength.Good)]
    [InlineData(-65, SignalStrength.Good)]
    [InlineData(-66, SignalStrength.Ok)]
    [InlineData(-73, SignalStrength.Ok)]
    [InlineData(-74, SignalStrength.Weak)]
    [InlineData(196, SignalStrength.Good)]
    public void NamesTheStrength(int raw, SignalStrength strength) =>
        Assert.Equal(strength, Signal.Strength(raw));
}
