using StealthPro.Core.Mix;

namespace StealthPro.Core.Tests.Mix;

public class SessionMixTests
{
    [Theory]
    [InlineData(0, 0f, 1f)]
    [InlineData(25, 0.5f, 1f)]
    [InlineData(50, 1f, 1f)]
    [InlineData(75, 1f, 0.5f)]
    [InlineData(100, 1f, 0f)]
    public void CrossfadesChatAgainstGame(int mix, float chat, float game)
    {
        Assert.Equal(chat, SessionMix.ChatScale(mix), 3);
        Assert.Equal(game, SessionMix.GameScale(mix), 3);
    }
}
