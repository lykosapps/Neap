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
        Assert.Equal(state, Microphone.Of(arm, 0, mutedInWindows));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(null)]
    public void TheBuiltInMicrophonesMutedOnTheHeadsetOutrankTheMuteInWindows(bool? mutedInWindows) =>
        Assert.Equal(MicState.MutedOnHeadset, Microphone.Of(0, 1, mutedInWindows));

    [Fact]
    public void TheArmUpOutranksTheBuiltInMicrophonesMuted() =>
        Assert.Equal(MicState.ArmUp, Microphone.Of(1, 1, false));

    [Theory]
    [InlineData(false, MicState.Live)]
    [InlineData(true, MicState.Muted)]
    public void AHeadsetWithoutBuiltInMicrophonesFollowsTheArmAlone(bool mutedInWindows, MicState state) =>
        Assert.Equal(state, Microphone.Of(0, null, mutedInWindows));

    [Theory]
    [InlineData(null, 0, false)]
    [InlineData(2, 0, false)]
    [InlineData(0, 0, null)]
    [InlineData(0, 2, false)]
    public void AReadingMissingOrNotUnderstoodIsUnknown(int? arm, int? builtIn, bool? mutedInWindows) =>
        Assert.Equal(MicState.Unknown, Microphone.Of(arm, builtIn, mutedInWindows));

    [Fact]
    public void WithTheArmUpTheArmStillSaysSoWhenWindowsCannotBeRead() =>
        Assert.Equal(MicState.ArmUp, Microphone.Of(1, 0, null));

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(0, 1, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(0, null, 0)]
    [InlineData(1, null, 1)]
    [InlineData(null, 0, null)]
    [InlineData(2, 0, null)]
    [InlineData(0, 2, null)]
    public void TheHeadsetIsMutedByTheArmOrByItsBuiltInMicrophones(int? arm, int? builtIn, int? muted) =>
        Assert.Equal(muted, Microphone.HeadsetMute(arm, builtIn));

    [Theory]
    [InlineData(MicState.Live, false, MicChange.MuteSystem)]
    [InlineData(MicState.Live, true, MicChange.MuteHeadset)]
    [InlineData(MicState.Muted, false, MicChange.UnmuteSystem)]
    [InlineData(MicState.Muted, true, MicChange.UnmuteSystem)]
    [InlineData(MicState.MutedOnHeadset, true, MicChange.UnmuteHeadset)]
    [InlineData(MicState.MutedOnHeadset, false, MicChange.None)]
    [InlineData(MicState.ArmUp, true, MicChange.None)]
    [InlineData(MicState.ArmUp, false, MicChange.None)]
    [InlineData(MicState.Unknown, true, MicChange.None)]
    public void TheAppMutesOnTheHeadsetWhereItCanAndInTheSystemOtherwise(MicState state, bool headsetMutes, MicChange change) =>
        Assert.Equal(change, Microphone.ChangeFor(state, headsetMutes));

    [Theory]
    [InlineData(MicState.Live, true)]
    [InlineData(MicState.Muted, true)]
    [InlineData(MicState.MutedOnHeadset, false)]
    [InlineData(MicState.ArmUp, false)]
    [InlineData(MicState.Unknown, false)]
    public void WithoutTheHeadsetsMuteOnlyTheArmDownLetsTheAppChangeTheMute(MicState state, bool can) =>
        Assert.Equal(can, Microphone.CanChange(state, headsetMutes: false));

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

    [Theory]
    [InlineData(null, 0, false)] // the arm down on the first reading undoes the headset's mute
    [InlineData(null, 1, true)]
    [InlineData(1, 0, false)]
    [InlineData(0, 0, null)]
    public void AMuteTheHeadsetSetIsUndoneOnceItsArmIsSeenDown(int? before, int? now, bool? mute) =>
        Assert.Equal(mute, Microphone.SystemMuteFor(before, now, mutedByHeadset: true));

    [Fact]
    public void TheArmIsAReadingNotASetting()
    {
        Assert.True(Registry.ByName.TryGetValue(Microphone.ArmSetting, out var arm));
        Assert.False(arm.Writable);
    }

    [Theory]
    [InlineData(2, 0, true)]
    [InlineData(2, 1, true)]
    [InlineData(1, 0, false)]
    [InlineData(null, 0, false)]
    [InlineData(2, null, false)]
    [InlineData(3, 0, false)]
    public void TheHeadsetMutesItselfOnlyWhileItsBuiltInMicrophonesAreInUse(int? inUse, int? builtIn, bool mutes) =>
        Assert.Equal(mutes, Microphone.HeadsetMutes(inUse, builtIn));

    [Fact]
    public void WhichMicrophoneIsInUseIsAReadingNotASetting()
    {
        Assert.True(Registry.ByName.TryGetValue(Microphone.InUseSetting, out var inUse));
        Assert.Equal(0x640, inUse.Key);
        Assert.False(inUse.Writable);
    }

    [Fact]
    public void TheBuiltInMicrophonesMuteIsASetting()
    {
        Assert.True(Registry.ByName.TryGetValue(Microphone.BuiltInSetting, out var builtIn));
        Assert.Equal(0x650, builtIn.Key);
        Assert.True(builtIn.Writable);
    }
}
