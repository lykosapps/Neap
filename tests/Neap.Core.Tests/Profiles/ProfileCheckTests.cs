using Neap.Core.Audio;
using Neap.Core.Profiles;
using Neap.Core.Settings;

namespace Neap.Core.Tests.Profiles;

public class ProfileCheckTests
{
    private static ProfileSettings With(string? gamePreset, string? micPreset) => new(
        NoiseMode.Cancelling, 80, false, 0, 0, false, 0, false, 50,
        gamePreset, micPreset, SpatialFormat.Off, 0, new ModeChoice(0, false), 1);

    [Fact]
    public void NothingIsMissingWhenBothPresetsAreStillThere() =>
        Assert.Empty(ProfileCheck.Missing(With("Bass Boost", "Clarity"), ["Bass Boost"], ["Clarity"]));

    [Fact]
    public void ADeletedGamePresetIsReportedByName() =>
        Assert.Equal(["Bass Boost"], ProfileCheck.Missing(With("Bass Boost", "Clarity"), [], ["Clarity"]));

    [Fact]
    public void ADeletedMicPresetIsReportedByName() =>
        Assert.Equal(["Clarity"], ProfileCheck.Missing(With("Bass Boost", "Clarity"), ["Bass Boost"], []));

    [Fact]
    public void BothCanBeMissingAtOnce() =>
        Assert.Equal(["Bass Boost", "Clarity"], ProfileCheck.Missing(With("Bass Boost", "Clarity"), [], []));

    [Fact]
    public void AProfileThatNeverSavedAPresetHasNothingToMiss() =>
        Assert.Empty(ProfileCheck.Missing(With(null, null), [], []));
}
