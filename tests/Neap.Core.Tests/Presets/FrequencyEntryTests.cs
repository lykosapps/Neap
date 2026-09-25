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

    [Fact]
    public void TextWithNoNumberIsNothing() => Assert.Null(FrequencyEntry.Parse("kHz"));
}
