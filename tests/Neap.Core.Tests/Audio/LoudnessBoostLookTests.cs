using Neap.Core.Audio;

namespace Neap.Core.Tests.Audio;

public class LoudnessBoostLookTests
{
    [Fact]
    public void NothingToShowWithoutAReading() =>
        Assert.False(LoudnessBoostLook.For(null, readOnly: false).Shown);

    [Fact]
    public void HiddenWhenTheHeadsetGivesNoReading() =>
        // A model without the boost is filed as no reading, never asked.
        Assert.False(LoudnessBoostLook.For(new LoudnessReading(Available: false, 0f), readOnly: false).Shown);

    [Fact]
    public void ShownAtStockAsNoBoost()
    {
        var look = LoudnessBoostLook.For(new LoudnessReading(true, LoudnessBoost.StockDb), readOnly: false);

        Assert.True(look.Shown);
        Assert.True(look.Enabled);
        Assert.Equal(0, look.BoostDb);
        Assert.Equal(-12f, look.LevelDb);
    }

    [Fact]
    public void ShowsHowFarAboveStockTheLevelSits()
    {
        var look = LoudnessBoostLook.For(new LoudnessReading(true, -9f), readOnly: false);

        Assert.Equal(3, look.BoostDb);
        Assert.Equal(-9f, look.LevelDb);
    }

    [Fact]
    public void TheReadoutLevelIsTheReportedLevelEvenPastTheSlidersRange() =>
        // Set to -20 elsewhere: the slider clamps to 0, the readout still reports -20.
        Assert.Equal(-20f, LoudnessBoostLook.For(new LoudnessReading(true, -20f), readOnly: false).LevelDb);

    [Fact]
    public void NotChangeableOnAReadOnlyHeadset() =>
        Assert.False(LoudnessBoostLook.For(new LoudnessReading(true, -12f), readOnly: true).Enabled);

    [Theory]
    [InlineData(-20f, 0)]   // set below stock elsewhere: shows the lowest the control offers
    [InlineData(12f, 18)]   // set above the cap elsewhere: shows the highest
    public void HeldToTheControlsRange(float level, int boost) =>
        Assert.Equal(boost, LoudnessBoostLook.For(new LoudnessReading(true, level), readOnly: false).BoostDb);
}
