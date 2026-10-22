using Neap.Core.Mix;
using Neap.Core.Pretend;

namespace Neap.Core.Tests.Mix;

public class SideLevelsTests
{
    private static readonly string[] Discord = ["Discord"];

    [Fact]
    public void ChatIsTheChosenAppsAndGameIsEverythingElse()
    {
        var level = SideLevels.Of(
        [
            new("Discord", 0.3f), new("DiscordPTB", 0.5f), new("Game", 0.9f), new("Spotify", 0.2f),
        ], Discord);

        Assert.Equal(0.5f, level.Chat);
        Assert.Equal(0.9f, level.Game);
    }

    [Fact]
    public void WithNoChatAppEverythingIsGame()
    {
        var level = SideLevels.Of([new("Discord", 0.4f), new("Game", 0.1f)], []);

        Assert.Equal(new SideLevel(0.4f, 0), level);
    }

    [Fact]
    public void AnApplicationWithNoNameIsGame() =>
        Assert.Equal(new SideLevel(0.6f, 0), SideLevels.Of([new("", 0.6f)], Discord));

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 1f)]
    [InlineData(2f, 1f)]
    [InlineData(0.001f, 0f)]
    public void ASilentPeakFillsNothingAndAFullOneFillsAll(float peak, float share) =>
        Assert.Equal(share, SideLevels.Share(peak), 6);

    [Fact]
    public void AnOrdinaryLevelFillsMoreThanItsPeakSays()
    {
        float share = SideLevels.Share(0.1f);

        Assert.InRange(share, 0.55f, 0.65f);
    }

    [Fact]
    public void AnArcRisesAtOnceAndFallsALittleAtATime()
    {
        Assert.Equal(0.8f, SideLevels.Fall(0.2f, 0.8f));
        Assert.Equal(0.8f - SideLevels.FallPerReading, SideLevels.Fall(0.8f, 0.1f), 6);
        Assert.Equal(0f, SideLevels.Fall(0f, 0f));
    }

    [Fact]
    public void ASideThatGoesQuietEmptiesInAboutASecond()
    {
        var shown = new SideLevel(1, 1);
        int readings = 0;
        while (shown.Chat > 0 && readings < 1000)
        {
            shown = SideLevels.Next(shown, default);
            readings++;
        }

        Assert.InRange(readings, 20, 30);
    }

    [Fact]
    public void AReadingLightsItsShareOfTheBars()
    {
        Assert.Equal(16, SideLevels.Lit(0.5f, 32));
        Assert.Equal(0, SideLevels.Lit(0, 32));
        Assert.Equal(32, SideLevels.Lit(3, 32));
        Assert.Equal(0, SideLevels.Lit(-1, 32));
    }

    [Fact]
    public void ThePretendMeterAlwaysHasGameToShowAndChatSpeaksInBursts()
    {
        var windows = new PretendWindows();
        using var meter = windows.OpenMeter();

        var first = meter.Read();

        Assert.True(first.Game > 0);
        Assert.True(first.Chat > 0);
    }

    [Fact]
    public void ThePretendMeterFollowsTheMix()
    {
        var windows = new PretendWindows();
        var mix = windows.CreateMix(["PretendChat"]);
        mix.Start();
        mix.SetMix(0);
        using var meter = windows.OpenMeter();

        var level = meter.Read();

        Assert.Equal(0, level.Chat);
        Assert.True(level.Game > 0);
    }
}
