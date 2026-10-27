using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class FilterImportLookTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   \n\n")]
    public void NothingPastedSaysNothingAndCannotBeApplied(string text)
    {
        var saying = FilterImportLook.Say(FilterImport.Parse(text));
        Assert.Equal(ImportSaying.NothingYet, saying);
        Assert.False(FilterImportLook.CanApply(saying));
    }

    [Theory]
    [InlineData("some words")]
    [InlineData("Filter 1: ON AP Fc 300 Hz Gain 0 dB Q 1")]
    [InlineData("Preamp: -6 dB")]
    public void TextWithNoFilterThatFitsSaysSoAndCannotBeApplied(string text)
    {
        var saying = FilterImportLook.Say(FilterImport.Parse(text));
        Assert.Equal(ImportSaying.NothingFits, saying);
        Assert.False(FilterImportLook.CanApply(saying));
    }

    [Theory]
    [InlineData(1, ImportSaying.SetsOne)]
    [InlineData(4, ImportSaying.SetsSome)]
    [InlineData(10, ImportSaying.SetsAll)]
    [InlineData(12, ImportSaying.SetsAll)]
    public void FiltersThatFitSayHowManyBandsAreSet(int filters, ImportSaying expected)
    {
        string text = string.Join('\n', Enumerable.Range(1, filters).Select(i => $"Filter {i}: ON PK Fc {i * 100} Hz Gain 1 dB Q 1"));
        var saying = FilterImportLook.Say(FilterImport.Parse(text));
        Assert.Equal(expected, saying);
        Assert.True(FilterImportLook.CanApply(saying));
    }
}
