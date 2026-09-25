using System.Globalization;
using Neap.Core.Settings;

namespace Neap.Core.Tests.Settings;

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

    [Theory]
    [InlineData("mic_monitoring", true)]
    [InlineData("noise_gate_threshold", true)]
    [InlineData("voice_prompt_volume", true)]
    [InlineData("eq_band_3", false)]
    [InlineData("anc", false)]
    [InlineData("dial_function", false)]
    public void ALevelFromZeroToAHundredIsAPercentage(string name, bool percent) =>
        Assert.Equal(percent, Registry.Resolve(name).IsPercent);
}
