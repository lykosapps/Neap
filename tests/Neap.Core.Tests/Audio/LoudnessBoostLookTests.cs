using Neap.Core.Audio;

namespace Neap.Core.Tests.Audio;

public class LoudnessBoostLookTests
{
    [Fact]
    public void NothingToShowWithoutAReading() =>
        Assert.False(LoudnessBoostLook.For(null, readOnly: false).Shown);

    [Fact]
    public void HiddenWhenTheHeadsetDoesNotAnswer() =>
        Assert.False(LoudnessBoostLook.For(new LoudnessReading(Available: false, 0f), readOnly: false).Shown);

    [Fact]
    public void ShownAtStockAsNoBoost()
    {
        var look = LoudnessBoostLook.For(new LoudnessReading(true, LoudnessBoost.StockDb), readOnly: false);

        Assert.True(look.Shown);
        Assert.True(look.Enabled);
        Assert.Equal(0, look.BoostDb);
    }

    [Fact]
    public void ShowsHowFarAboveStockTheLevelSits() =>
        Assert.Equal(3, LoudnessBoostLook.For(new LoudnessReading(true, -9f), readOnly: false).BoostDb);

    [Fact]
    public void NotChangeableOnAReadOnlyHeadset() =>
        Assert.False(LoudnessBoostLook.For(new LoudnessReading(true, -12f), readOnly: true).Enabled);

    [Theory]
    [InlineData(-20f, 0)]   // set below stock elsewhere: shows the lowest the control offers
    [InlineData(6f, 12)]    // set above the cap elsewhere: shows the highest
    public void HeldToTheControlsRange(float level, int boost) =>
        Assert.Equal(boost, LoudnessBoostLook.For(new LoudnessReading(true, level), readOnly: false).BoostDb);
}
