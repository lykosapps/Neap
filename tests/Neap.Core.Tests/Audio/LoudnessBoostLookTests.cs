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
        // Set to -30 elsewhere: the slider clamps to its lowest, the readout still reports -30.
        Assert.Equal(-30f, LoudnessBoostLook.For(new LoudnessReading(true, -30f), readOnly: false).LevelDb);

    [Fact]
    public void NotChangeableOnAReadOnlyHeadset() =>
        Assert.False(LoudnessBoostLook.For(new LoudnessReading(true, -12f), readOnly: true).Enabled);

    [Fact]
    public void ShowsHowFarBelowStockTheLevelSits()
    {
        var look = LoudnessBoostLook.For(new LoudnessReading(true, -18f), readOnly: false);

        Assert.Equal(-6, look.BoostDb);
        Assert.Equal(-18f, look.LevelDb);
    }

    [Theory]
    [InlineData(-30f, -12)]   // set below the lowest elsewhere: shows the lowest the control offers
    [InlineData(-24f, -12)]   // the lowest offered is itself
    [InlineData(12f, 18)]   // set above the cap elsewhere: shows the highest
    public void HeldToTheControlsRange(float level, int boost) =>
        Assert.Equal(boost, LoudnessBoostLook.For(new LoudnessReading(true, level), readOnly: false).BoostDb);
}
