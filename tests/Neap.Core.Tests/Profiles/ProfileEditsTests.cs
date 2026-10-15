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

    /// <summary>An Atlas Air has no noise cancellation, dial or Mode button, and reads them as nothing.</summary>
    private static ProfileSettings OnAnAtlasAir(int autoShutoff = 0) => new(
        NoiseMode.Off, 0, false, 0, 0, false, 0, false, 0,
        "Bass Boost", "Clarity", SpatialFormat.Off, autoShutoff, new ModeChoice(0, false), 0);

    [Fact]
    public void AStealthProIIsProfileIsNotEditedByWhatTheAtlasAirLacks()
    {
        var saved = With("Bass Boost", "Clarity");

        var live = ProfileEdits.Keeping(OnAnAtlasAir(), saved, HeadsetModels.AtlasAir);

        Assert.False(ProfileEdits.IsEdited(saved, live));
        Assert.Equal((NoiseMode.Cancelling, 80, 1), (live.NoiseMode, live.NoiseLevel, live.DialFunction));
    }

    [Fact]
    public void WhatTheAtlasAirHasStillCountsAsAnEdit() =>
        Assert.True(ProfileEdits.IsEdited(With("Bass Boost", "Clarity"),
            ProfileEdits.Keeping(OnAnAtlasAir(autoShutoff: 2), With("Bass Boost", "Clarity"), HeadsetModels.AtlasAir)));

    [Fact]
    public void AHeadsetThatHoldsEverythingKeepsItsOwnSettings() =>
        Assert.Equal(OnAnAtlasAir(), ProfileEdits.Keeping(OnAnAtlasAir(), With("Bass Boost", "Clarity"), HeadsetModels.StealthProII));

    [Fact]
    public void WithNoProfileTheLiveSettingsStand() =>
        Assert.Equal(OnAnAtlasAir(), ProfileEdits.Keeping(OnAnAtlasAir(), null, HeadsetModels.AtlasAir));

    [Theory]
    [InlineData(Link.Connected, true, ProfileReadiness.Ready)]
    [InlineData(Link.Connected, false, ProfileReadiness.Reading)]
    [InlineData(Link.Quiet, true, ProfileReadiness.HeadsetNotAnswering)]
    [InlineData(Link.Silent, false, ProfileReadiness.HeadsetNotAnswering)]
    [InlineData(Link.Absent, false, ProfileReadiness.HeadsetNotAnswering)]
    [InlineData(Link.Connecting, false, ProfileReadiness.HeadsetNotAnswering)]
    public void AProfileNeedsTheHeadsetAnsweringAndRead(Link link, bool read, ProfileReadiness expected) =>
        Assert.Equal(expected, ProfileGate.Of(link, read));

    /// <summary>A profile that waited on a setting the headset lacks or Neap cannot read would never be ready.</summary>
    [Theory]
    [InlineData("auto_shutoff", true)]
    [InlineData("ai_noise_reduction", true)]
    [InlineData("anc", false)]
    [InlineData("mode_button_function", false)]
    [InlineData("dial_function", false)]
    [InlineData("superhuman_hearing", false)]
    [InlineData("noise_gate", false)]
    [InlineData("mic_monitoring", false)]
    public void AnAtlasAirsProfileHoldsOnlyWhatItCanRead(string setting, bool held) =>
        Assert.Equal(held, ProfileGate.Holds(HeadsetModels.AtlasAir, setting));

    [Theory]
    [InlineData("anc")]
    [InlineData("mode_button_function")]
    [InlineData("superhuman_hearing")]
    [InlineData("mic_monitoring")]
    public void AStealthProIIsProfileHoldsEverySetting(string setting)
    {
        Assert.True(ProfileGate.Holds(HeadsetModels.StealthProII, setting));
        Assert.True(ProfileGate.Holds(null, setting));
    }

    [Theory]
    [InlineData("Games", "witcher3.exe", "witcher3")]
    [InlineData("Steam/steamapps/common/Some Game", "Some Game.exe", "Some Game")]
    [InlineData("", "chrome.exe", "chrome")]
    public void APickedProgramIsNamedByItsProcess(string folder, string file, string process) =>
        Assert.Equal(process, ProgramName.Of(Path.Combine(folder.Length == 0 ? "" : Path.GetTempPath(), folder, file)));
}
