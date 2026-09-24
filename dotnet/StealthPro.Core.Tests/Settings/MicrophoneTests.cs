using StealthPro.Core.Settings;

namespace StealthPro.Core.Tests.Settings;

public class MicrophoneTests
{
    [Theory]
    [InlineData(null, MicState.Unknown)]
    [InlineData(0, MicState.Live)]
    [InlineData(1, MicState.Muted)]
    [InlineData(2, MicState.Unknown)]
    public void ReadsTheMuteAsWhetherTheMicrophoneIsLive(int? muted, MicState state) =>
        Assert.Equal(state, Microphone.Of(muted));

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void WritesLiveAsNotMuted(bool live, int value) =>
        Assert.Equal(value, Microphone.ValueFor(live));

    [Fact]
    public void NamesARegistrySetting() =>
        Assert.True(Registry.ByName.ContainsKey(Microphone.Setting));
}
