using StealthPro.Core.Presets;

namespace StealthPro.Core.Tests.Presets;

public class PresetLabelTests
{
    [Theory]
    [InlineData("Bass Boost", false, PresetShown.Named)]
    [InlineData("Bass Boost", true, PresetShown.Edited)]
    [InlineData("", false, PresetShown.Unsaved)]
    [InlineData("", true, PresetShown.Unsaved)]
    public void NamesTheCurve(string baseline, bool edited, PresetShown shown) =>
        Assert.Equal(shown, PresetLabel.Of(baseline, edited));
}
