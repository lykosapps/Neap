using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class FrequencyEntryTests
{
    [Theory]
    [InlineData("4915", 4915)]
    [InlineData("4.9k", 4900)]
    [InlineData("4.9 kHz", 4900)]
    [InlineData("16", 16000)]
    [InlineData("4,9", 4900)]
    [InlineData("500", 500)]
    [InlineData("500 Hz", 500)]
    [InlineData("20", 20)]
    [InlineData("12 Hz", 12)]
    public void ReadsWhatWasMeant(string typed, double hertz) =>
        Assert.Equal(hertz, FrequencyEntry.Parse(typed)!.Value, 6);

    [Theory]
    [InlineData("1,500 Hz", 0, 1500)]
    [InlineData("1,500", 0, 1500)]
    [InlineData("12,500 Hz", 0, 12500)]
    [InlineData("1,5 kHz", 0, 1500)]
    [InlineData("19", 19500, 19000)]
    [InlineData("16", 15800, 16000)]
    [InlineData("500", 15800, 500)]
    [InlineData("4900", 15800, 4900)]
    [InlineData("20", 500, 20)]
    [InlineData("20 Hz", 19500, 20)]
    public void ReadsTheUnitTheFieldShowed(string typed, double shown, double hertz) =>
        Assert.Equal(hertz, FrequencyEntry.Parse(typed, shown)!.Value, 6);

    [Fact]
    public void TextWithNoNumberIsNothing() => Assert.Null(FrequencyEntry.Parse("kHz"));
}
