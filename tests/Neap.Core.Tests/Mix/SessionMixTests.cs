using Neap.Core.Mix;

namespace Neap.Core.Tests.Mix;

public class SessionMixTests
{
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
