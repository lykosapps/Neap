using Neap.Core.Audio;
using Neap.Core.Profiles;
using Neap.Core.Settings;

namespace Neap.Core.Tests.Profiles;

public class ProfileAssignmentTests
{
    private static readonly ProfileSettings Blank = new(
        NoiseMode.Off, 0, false, 0, 0, false, 0, false, 0, null, null,
        SpatialFormat.Off, 0, new ModeChoice(0, false), 1);

    private static Profile With(string id, string name, params string[] apps) => new(id, name, apps, [], Blank);

    [Fact]
    public void AssigningAddsTheAppToThatProfile()
    {
        var profiles = new[] { With("1", "Gaming"), With("2", "Chill") };
        var after = ProfileAssignment.Assign(profiles, "1", "witcher3.exe");
        Assert.Equal(["witcher3.exe"], after.Single(p => p.Id == "1").AssignedApps);
    }

    [Fact]
    public void AssigningTakesTheAppAwayFromItsOldProfile()
    {
        var profiles = new[] { With("1", "Gaming", "witcher3.exe"), With("2", "Chill") };
        var after = ProfileAssignment.Assign(profiles, "2", "witcher3.exe");
        Assert.Empty(after.Single(p => p.Id == "1").AssignedApps);
        Assert.Equal(["witcher3.exe"], after.Single(p => p.Id == "2").AssignedApps);
    }

    [Fact]
    public void AssigningAnAppItAlreadyHasChangesNothing()
    {
        var profiles = new[] { With("1", "Gaming", "witcher3.exe") };
        var after = ProfileAssignment.Assign(profiles, "1", "witcher3.exe");
        Assert.Equal(["witcher3.exe"], after.Single().AssignedApps);
    }

    [Fact]
    public void AssigningIsCaseInsensitive()
    {
        var profiles = new[] { With("1", "Gaming", "Witcher3.exe") };
        var after = ProfileAssignment.Assign(profiles, "1", "witcher3.exe");
        Assert.Equal(["Witcher3.exe"], after.Single().AssignedApps);
    }

    [Fact]
    public void UnassigningTakesTheAppAwayFromWhicheverProfileHasIt()
    {
        var profiles = new[] { With("1", "Gaming", "witcher3.exe", "eldenring.exe"), With("2", "Chill") };
        var after = ProfileAssignment.Unassign(profiles, "witcher3.exe");
        Assert.Equal(["eldenring.exe"], after.Single(p => p.Id == "1").AssignedApps);
    }

    [Fact]
    public void UnassigningAnAppNobodyHasChangesNothing()
    {
        var profiles = new[] { With("1", "Gaming", "witcher3.exe") };
        var after = ProfileAssignment.Unassign(profiles, "eldenring.exe");
        Assert.Equal(["witcher3.exe"], after.Single().AssignedApps);
    }

    [Fact]
    public void AnAppCanBeSetToSwitchOnlyOnTheMicrophone()
    {
        var profiles = new[] { With("1", "Calls", "ms-teams") };
        var after = ProfileAssignment.SwitchOnMicrophone(profiles, "MS-Teams", true);
        Assert.Equal(["ms-teams"], after.Single().MicrophoneApps);
        Assert.Empty(ProfileAssignment.SwitchOnMicrophone(after, "ms-teams", false).Single().MicrophoneApps);
    }

    [Fact]
    public void AnAppNobodyHasCannotBeSetToTheMicrophone()
    {
        var profiles = new[] { With("1", "Calls") };
        var after = ProfileAssignment.SwitchOnMicrophone(profiles, "ms-teams", true);
        Assert.Empty(after.Single().MicrophoneApps);
    }

    [Fact]
    public void AssigningAgainKeepsWhenItSwitches()
    {
        var profiles = ProfileAssignment.SwitchOnMicrophone([With("1", "Calls", "ms-teams")], "ms-teams", true);
        var after = ProfileAssignment.Assign(profiles, "1", "ms-teams");
        Assert.Equal(["ms-teams"], after.Single().MicrophoneApps);
    }

    [Fact]
    public void MovingAnAppToAnotherProfileSwitchesItWhileOpen()
    {
        var profiles = ProfileAssignment.SwitchOnMicrophone([With("1", "Calls", "ms-teams"), With("2", "Work")], "ms-teams", true);
        var after = ProfileAssignment.Assign(profiles, "2", "ms-teams");
        Assert.All(after, p => Assert.Empty(p.MicrophoneApps));
        Assert.Equal(["ms-teams"], after.Single(p => p.Id == "2").AssignedApps);
    }

    [Fact]
    public void UnassigningForgetsWhenItSwitched()
    {
        var profiles = ProfileAssignment.SwitchOnMicrophone([With("1", "Calls", "ms-teams")], "ms-teams", true);
        Assert.Empty(ProfileAssignment.Unassign(profiles, "ms-teams").Single().MicrophoneApps);
    }
}
