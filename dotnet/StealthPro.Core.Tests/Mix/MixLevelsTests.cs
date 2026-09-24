using StealthPro.Core.Mix;

namespace StealthPro.Core.Tests.Mix;

public class MixLevelsTests
{
    [Theory]
    [InlineData(0, 0f, 1f)]
    [InlineData(25, 0.5f, 1f)]
    [InlineData(50, 1f, 1f)]
    [InlineData(75, 1f, 0.5f)]
    [InlineData(100, 1f, 0f)]
    public void CrossfadesChatAgainstGame(int mix, float chat, float game)
    {
        Assert.Equal(chat, MixLevels.ChatScale(mix), 3);
        Assert.Equal(game, MixLevels.GameScale(mix), 3);
    }

    [Theory]
    [InlineData(-5, MixLean.GameOnly)]
    [InlineData(0, MixLean.GameOnly)]
    [InlineData(1, MixLean.TowardGame)]
    [InlineData(49, MixLean.TowardGame)]
    [InlineData(50, MixLean.Balanced)]
    [InlineData(51, MixLean.TowardChat)]
    [InlineData(99, MixLean.TowardChat)]
    [InlineData(100, MixLean.ChatOnly)]
    [InlineData(120, MixLean.ChatOnly)]
    public void NamesWhichWayTheMixLeans(int mix, MixLean lean) =>
        Assert.Equal(lean, MixLevels.Lean(mix));
}
