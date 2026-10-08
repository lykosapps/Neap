using Neap.Core.Audio;
using Neap.Core.Profiles;
using Neap.Core.Settings;

namespace Neap.Core.Tests.Profiles;

public class AutoSwitchTests
{
    private static readonly ProfileSettings Blank = new(
        NoiseMode.Off, 0, false, 0, 0, false, 0, false, 0, null, null,
        SpatialFormat.Off, 0, new ModeChoice(0, false), 1);

    private static readonly IReadOnlyList<Profile> Profiles =
    [
        new("desk", "Desktop", [], [], Blank),
        new("game", "Gaming", ["witcher3"], [], Blank),
        new("chat", "Chat", ["Discord"], [], Blank),
        new("tune", "Music", [], [], Blank),
        new("call", "Calls", ["ms-teams", "zoom"], ["ms-teams"], Blank),
    ];

    private readonly AutoSwitch _switch = new();

    private string? See(string? defaultId, string? activeId, params string[] running) =>
        _switch.Observe(["explorer", .. running], [], Profiles, defaultId, activeId);

    /// <summary>With ms-teams open throughout, and on the microphone or not.</summary>
    private string? Call(bool onMicrophone, string? defaultId, string? activeId, params string[] running) =>
        _switch.Observe(["explorer", "ms-teams", .. running], onMicrophone ? ["ms-teams"] : [], Profiles, defaultId, activeId);

    [Fact]
    public void NothingAssignedRunningChangesNothing() =>
        Assert.Null(See("desk", "desk"));

    [Fact]
    public void AnAppStartingSwitchesToItsProfile() =>
        Assert.Equal("game", See("desk", "desk", "witcher3"));

    [Fact]
    public void StillRunningChangesNothing()
    {
        See("desk", "desk", "witcher3");
        Assert.Null(See("desk", "game", "witcher3"));
    }

    [Fact]
    public void ClosingTheAppReturnsToTheDefault()
    {
        See("desk", "tune", "witcher3");
        Assert.Equal("desk", See("desk", "game"));
    }

    [Fact]
    public void WithNoDefaultClosingReturnsToTheOneBefore()
    {
        See(null, "tune", "witcher3");
        Assert.Equal("tune", See(null, "game"));
    }

    [Fact]
    public void WithNoDefaultAndNothingBeforeClosingLeavesItAlone()
    {
        See(null, null, "witcher3");
        Assert.Null(See(null, "game"));
    }

    [Fact]
    public void TheNewestAppWins()
    {
        See("desk", "desk", "witcher3");
        Assert.Equal("chat", See("desk", "game", "witcher3", "Discord"));
    }

    [Fact]
    public void ClosingTheNewestReturnsToTheOtherStillRunning()
    {
        See("desk", "desk", "witcher3");
        See("desk", "game", "witcher3", "Discord");
        Assert.Equal("game", See("desk", "chat", "witcher3"));
    }

    [Fact]
    public void ClosingTheOlderLeavesTheNewestsProfile()
    {
        See("desk", "desk", "witcher3");
        See("desk", "game", "witcher3", "Discord");
        Assert.Equal("chat", See("desk", "chat", "Discord"));
    }

    [Fact]
    public void TheProfileBeforeIsTheOneBeforeTheFirstApp()
    {
        See(null, "tune", "witcher3");
        See(null, "game", "witcher3", "Discord");
        See(null, "chat", "Discord");
        Assert.Equal("tune", See(null, "chat"));
    }

    [Fact]
    public void AChoiceByHandHoldsWhenTheAppCloses()
    {
        See("desk", "desk", "witcher3");
        _switch.Chose();
        Assert.Null(See("desk", "tune"));
    }

    [Fact]
    public void AChoiceByHandHoldsWhileOthersClose()
    {
        See("desk", "desk", "witcher3");
        See("desk", "game", "witcher3", "Discord");
        _switch.Chose();
        Assert.Null(See("desk", "tune", "witcher3"));
        Assert.Null(See("desk", "tune"));
    }

    [Fact]
    public void AChoiceByHandIsForgottenOnceEveryAppHasClosed()
    {
        See("desk", "desk", "witcher3");
        _switch.Chose();
        See("desk", "tune");
        See("desk", "tune", "witcher3");
        Assert.Equal("desk", See("desk", "game"));
    }

    [Fact]
    public void TheNextAppStartingSwitchesDespiteAChoiceByHand()
    {
        See("desk", "desk", "witcher3");
        _switch.Chose();
        Assert.Equal("chat", See("desk", "tune", "witcher3", "Discord"));
        Assert.Equal("game", See("desk", "chat", "witcher3"));
    }

    [Fact]
    public void AChoiceByHandWithNoAppRunningChangesNothingLater()
    {
        _switch.Chose();
        See("desk", "tune", "witcher3");
        Assert.Equal("desk", See("desk", "game"));
    }

    [Fact]
    public void ProcessNamesMatchWhateverTheirCase() =>
        Assert.Equal("chat", See("desk", "desk", "DISCORD"));

    [Fact]
    public void SeveralFoundAtOnceSettleOnTheSameOneEveryTime() =>
        Assert.Equal("game", See("desk", "desk", "witcher3", "Discord"));

    [Fact]
    public void AssigningARunningAppCountsAsItStarting()
    {
        See("desk", "desk", "spotify");
        IReadOnlyList<Profile> assigned = [.. Profiles.Select(p => p.Id == "tune" ? p with { AssignedApps = ["spotify"] } : p)];
        Assert.Equal("tune", _switch.Observe(["spotify"], [], assigned, "desk", "desk"));
    }

    [Fact]
    public void UnassigningARunningAppCountsAsItClosing()
    {
        See("desk", "tune", "witcher3");
        IReadOnlyList<Profile> unassigned = [.. Profiles.Select(p => p with { AssignedApps = [] })];
        Assert.Equal("desk", _switch.Observe(["witcher3"], [], unassigned, "desk", "game"));
    }

    [Fact]
    public void AMicrophoneAppOpenChangesNothing() =>
        Assert.Null(Call(false, "desk", "desk"));

    [Fact]
    public void AMicrophoneAppUsingTheMicrophoneSwitchesToItsProfile()
    {
        Call(false, "desk", "desk");
        Assert.Equal("call", Call(true, "desk", "desk"));
    }

    [Fact]
    public void AMicrophoneAppLettingGoOfTheMicrophoneReturnsToTheDefault()
    {
        Call(true, "desk", "desk");
        Assert.Equal("desk", Call(false, "desk", "call"));
    }

    [Fact]
    public void AnotherAppOnTheMicrophoneCountsForNothing()
    {
        Call(false, "desk", "desk");
        Assert.Null(_switch.Observe(["ms-teams"], ["witcher3", "obs64"], Profiles, "desk", "desk"));
    }

    [Fact]
    public void AnAppSetToSwitchWhileOpenStillDoesOnTheMicrophone() =>
        Assert.Equal("call", _switch.Observe(["zoom"], ["zoom"], Profiles, "desk", "desk"));

    [Fact]
    public void ACallDuringAGameTakesOverAndHandsBack()
    {
        See("desk", "desk", "witcher3");
        Assert.Equal("call", Call(true, "desk", "game", "witcher3"));
        Assert.Equal("game", Call(false, "desk", "call", "witcher3"));
    }

    [Fact]
    public void MicrophoneAppsMatchWhateverTheirCase() =>
        Assert.Equal("call", _switch.Observe([], ["MS-Teams"], Profiles, "desk", "desk"));
}
