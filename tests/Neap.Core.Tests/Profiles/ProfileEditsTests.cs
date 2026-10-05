using Neap.Core.Audio;
using Neap.Core.Connection;
using Neap.Core.Profiles;
using Neap.Core.Settings;

namespace Neap.Core.Tests.Profiles;

public class ProfileEditsTests
{
    private static ProfileSettings With(string? game, string? mic, int monitoring = 50) => new(
        NoiseMode.Cancelling, 80, false, 0, 0, false, 0, false, monitoring,
        game, mic, SpatialFormat.Off, 0, new ModeChoice(0, false), 1);

    [Fact]
    public void TheSameSettingsAreNotEdited() =>
        Assert.False(ProfileEdits.IsEdited(With("Bass Boost", "Clarity"), With("Bass Boost", "Clarity")));

    [Fact]
    public void AChangedSettingIsEdited() =>
        Assert.True(ProfileEdits.IsEdited(With("Bass Boost", "Clarity"), With("Bass Boost", "Clarity", monitoring: 60)));

    [Fact]
    public void ADifferentPresetIsEdited() =>
        Assert.True(ProfileEdits.IsEdited(With("Bass Boost", "Clarity"), With("Vocal Boost", "Clarity")));

    [Fact]
    public void APresetTheProfileNeverSavedIsNotAnEdit() =>
        Assert.False(ProfileEdits.IsEdited(With(null, null), With("Signature Sound", "Clarity")));

    [Fact]
    public void ASavedPresetThatIsNowUnnamedIsAnEdit() =>
        Assert.True(ProfileEdits.IsEdited(With("Bass Boost", "Clarity"), With(null, "Clarity")));

    [Theory]
    [InlineData(Link.Connected, true, ProfileReadiness.Ready)]
    [InlineData(Link.Connected, false, ProfileReadiness.Reading)]
    [InlineData(Link.Quiet, true, ProfileReadiness.HeadsetNotAnswering)]
    [InlineData(Link.Silent, false, ProfileReadiness.HeadsetNotAnswering)]
    [InlineData(Link.Absent, false, ProfileReadiness.HeadsetNotAnswering)]
    [InlineData(Link.Connecting, false, ProfileReadiness.HeadsetNotAnswering)]
    public void AProfileNeedsTheHeadsetAnsweringAndRead(Link link, bool read, ProfileReadiness expected) =>
        Assert.Equal(expected, ProfileGate.Of(link, read));

    [Theory]
    [InlineData("Games", "witcher3.exe", "witcher3")]
    [InlineData("Steam/steamapps/common/Some Game", "Some Game.exe", "Some Game")]
    [InlineData("", "chrome.exe", "chrome")]
    public void APickedProgramIsNamedByItsProcess(string folder, string file, string process) =>
        Assert.Equal(process, ProgramName.Of(Path.Combine(folder.Length == 0 ? "" : Path.GetTempPath(), folder, file)));
}
