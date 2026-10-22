using Neap.Core.Settings;

namespace Neap.Core.Tests.Settings;

public class MicrophoneTests
{
    private const int Boom = 1, BuiltIn = 2;

    [Theory]
    [InlineData(0, false, MicState.Live)]
    [InlineData(0, true, MicState.Muted)]
    [InlineData(1, false, MicState.ArmUp)]
    [InlineData(1, true, MicState.ArmUp)]
    public void WithTheBoomTheArmOutranksTheMuteInWindows(int arm, bool mutedInWindows, MicState state) =>
        Assert.Equal(state, Microphone.Of(new HeadsetMic(arm, 0, Boom), mutedInWindows));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(null)]
    public void WithTheBuiltInMicrophonesTheirMuteOutranksTheMuteInWindows(bool? mutedInWindows) =>
        Assert.Equal(MicState.MutedOnHeadset, Microphone.Of(new HeadsetMic(0, 1, BuiltIn), mutedInWindows));

    [Theory]
    [InlineData(false, MicState.Live)]
    [InlineData(true, MicState.Muted)]
    public void WithTheBoomBackInTheBuiltInMicrophonesMuteLeftOnIsNotItsMute(bool mutedInWindows, MicState state) =>
        Assert.Equal(state, Microphone.Of(new HeadsetMic(0, 1, Boom), mutedInWindows));

    [Theory]
    [InlineData(false, MicState.Live)]
    [InlineData(true, MicState.Muted)]
    public void AHeadsetThatSaysNothingOfItsMicrophonesFollowsTheArmAlone(bool mutedInWindows, MicState state) =>
        Assert.Equal(state, Microphone.Of(new HeadsetMic(0, null, null), mutedInWindows));

    [Theory]
    [InlineData(null, 0, Boom, false)]
    [InlineData(2, 0, Boom, false)]
    [InlineData(0, 0, Boom, null)]
    [InlineData(0, 2, BuiltIn, false)]
    [InlineData(0, null, BuiltIn, false)]
    public void AReadingMissingOrNotUnderstoodIsUnknown(int? arm, int? builtIn, int? inUse, bool? mutedInWindows) =>
        Assert.Equal(MicState.Unknown, Microphone.Of(new HeadsetMic(arm, builtIn, inUse), mutedInWindows));

    [Fact]
    public void WithTheArmUpTheArmStillSaysSoWhenWindowsCannotBeRead() =>
        Assert.Equal(MicState.ArmUp, Microphone.Of(new HeadsetMic(1, 0, Boom), null));

    [Theory]
    [InlineData(0, 0, Boom, 0)]
    [InlineData(1, 0, Boom, 1)]
    [InlineData(0, 1, Boom, 0)]
    [InlineData(1, 1, Boom, 1)]
    [InlineData(0, null, null, 0)]
    [InlineData(1, null, null, 1)]
    [InlineData(0, 0, BuiltIn, 0)]
    [InlineData(0, 1, BuiltIn, 1)]
    [InlineData(null, 1, BuiltIn, 1)]
    [InlineData(null, 0, Boom, null)]
    [InlineData(2, 0, Boom, null)]
    [InlineData(0, 2, BuiltIn, null)]
    [InlineData(0, null, BuiltIn, null)]
    public void TheHeadsetsOwnMuteIsTheOneOfTheMicrophoneInUse(int? arm, int? builtIn, int? inUse, int? muted) =>
        Assert.Equal(muted, new HeadsetMic(arm, builtIn, inUse).Mute);

    [Theory]
    [InlineData(BuiltIn, 0, true)]
    [InlineData(BuiltIn, 1, true)]
    [InlineData(Boom, 0, false)]
    [InlineData(null, 0, false)]
    [InlineData(BuiltIn, null, false)]
    [InlineData(3, 0, false)]
    public void TheAppMutesOnTheHeadsetOnlyWhileItsBuiltInMicrophonesAreInUse(int? inUse, int? builtIn, bool mutes) =>
        Assert.Equal(mutes, new HeadsetMic(0, builtIn, inUse).MutesOnHeadset);

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

    [Fact]
    public void TheSystemFollowsEveryMuteOfARecordingThroughTheBoomComingOutAndBackIn()
    {
        // The owner's recording of 22 October, in order: the arm up and down,
        // the boom out, the button three times, the boom back in, and the arm
        // knocked up as it went in, then down.
        HeadsetMic[] seen =
        [
            new(0, 0, Boom), new(1, 0, Boom), new(0, 0, Boom),
            new(0, 0, BuiltIn), new(0, 1, BuiltIn), new(0, 0, BuiltIn), new(0, 1, BuiltIn),
            new(0, 1, Boom), new(1, 1, Boom), new(0, 1, Boom),
        ];
        bool?[] system = [null, true, false, null, true, false, true, false, true, false];

        int? before = null;
        for (int i = 0; i < seen.Length; i++)
        {
            Assert.Equal(system[i], Microphone.SystemMuteFor(before, seen[i].Mute));
            before = seen[i].Mute;
        }
    }

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
