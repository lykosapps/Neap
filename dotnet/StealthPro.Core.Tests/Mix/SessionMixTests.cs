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

    [Fact]
    public void ChatPlayingOnAnotherDeviceIsReported()
    {
        var found = SessionMix.Elsewhere([
            new("Discord", "Speakers (2- Stealth Pro II Xbox)", Playing: true),
        ]);

        Assert.Equal(new ChatElsewhere("Discord", "Speakers (2- Stealth Pro II Xbox)"), found);
    }

    [Fact]
    public void AnIdleSessionLeftOnAnotherDeviceIsNotReported()
    {
        // Applications keep idle sessions on every device they have played to.
        Assert.Null(SessionMix.Elsewhere([
            new("Discord", "Speakers (Realtek(R) Audio)", Playing: false),
        ]));
    }

    [Fact]
    public void ThePlayingDeviceIsReportedOverAnIdleOne()
    {
        var found = SessionMix.Elsewhere([
            new("Discord", "Speakers (Realtek(R) Audio)", Playing: false),
            new("Discord", "Speakers (2- Stealth Pro II Xbox)", Playing: true),
        ]);

        Assert.Equal("Speakers (2- Stealth Pro II Xbox)", found?.Device);
    }
}
