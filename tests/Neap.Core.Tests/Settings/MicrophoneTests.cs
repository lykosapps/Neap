using Neap.Core.Settings;

namespace Neap.Core.Tests.Settings;

public class MicrophoneTests
{
    [Theory]
    [InlineData(0, false, MicState.Live)]
    [InlineData(0, true, MicState.Muted)]
    [InlineData(1, false, MicState.ArmUp)]
    [InlineData(1, true, MicState.ArmUp)]
    public void TheArmOutranksTheMuteInWindows(int arm, bool mutedInWindows, MicState state) =>
        Assert.Equal(state, Microphone.Of(arm, mutedInWindows));

    [Theory]
    [InlineData(null, false)]
    [InlineData(2, false)]
    [InlineData(0, null)]
    public void AReadingMissingOrNotUnderstoodIsUnknown(int? arm, bool? mutedInWindows) =>
        Assert.Equal(MicState.Unknown, Microphone.Of(arm, mutedInWindows));

    [Fact]
    public void WithTheArmUpTheArmStillSaysSoWhenWindowsCannotBeRead() =>
        Assert.Equal(MicState.ArmUp, Microphone.Of(1, null));

    [Theory]
    [InlineData(MicState.Live, true)]
    [InlineData(MicState.Muted, true)]
    [InlineData(MicState.ArmUp, false)]
    [InlineData(MicState.Unknown, false)]
    public void OnlyTheArmDownLetsTheAppChangeTheMute(MicState state, bool can) =>
        Assert.Equal(can, Microphone.CanChange(state));

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 0, false)]
    [InlineData(null, 1, true)]
    [InlineData(null, 0, null)]
    [InlineData(0, 0, null)]
    [InlineData(1, 1, null)]
    [InlineData(1, null, null)]
    [InlineData(0, 2, null)]
    public void TheSystemFollowsTheHeadsetMutingItself(int? before, int? now, bool? mute) =>
        Assert.Equal(mute, Microphone.SystemMuteFor(before, now));

    [Fact]
    public void TheArmIsAReadingNotASetting()
    {
        Assert.True(Registry.ByName.TryGetValue(Microphone.ArmSetting, out var arm));
        Assert.False(arm.Writable);
    }
}
