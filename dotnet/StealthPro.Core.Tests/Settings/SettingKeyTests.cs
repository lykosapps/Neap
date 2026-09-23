using System.Globalization;
using StealthPro.Core.Settings;

namespace StealthPro.Core.Tests.Settings;

public class SettingKeyTests
{
    private static readonly SettingKey Band = Registry.Resolve("eq_band_3");
    private static readonly SettingKey Anc = Registry.Resolve("anc");
    private static readonly SettingKey Dial = Registry.Resolve("dial_function");

    [Theory]
    [InlineData(-90, "-90")]
    [InlineData(0, "0")]
    [InlineData(90, "90")]
    public void AcceptsABandInRange(int value, string wire)
    {
        Assert.Equal(wire, Band.Validate(value));
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void RefusesABandOutOfRange(int value)
    {
        Assert.Throws<ArgumentException>(() => Band.Validate(value));
    }

    [Fact]
    public void AToggleIsZeroOrOne()
    {
        Assert.Equal("1", Anc.Validate(1));
        Assert.Throws<ArgumentException>(() => Anc.Validate(2));
    }

    [Fact]
    public void AChoiceMustBeOneOfItsOptions()
    {
        Assert.Equal("2", Dial.Validate(2));
        Assert.Throws<ArgumentException>(() => Dial.Validate(0));
    }

    [Fact]
    public void TheWireFormIsTheSameOnEveryLanguage()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
            Assert.Equal("-30", Band.Validate(-30));
        }
        finally { CultureInfo.CurrentCulture = before; }
    }
}
