using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class ParametricOffTests
{
    [Fact]
    public void TheGainCorrectionBackOnIsAReset() =>
        Assert.True(ParametricOff.ResetBySwarm("1.2.3", "1.2.3", gainCorrectionOn: true));

    [Fact]
    public void ADifferentFirmwareIsAReset() =>
        Assert.True(ParametricOff.ResetBySwarm("1.2.3", "1.2.4", gainCorrectionOn: null));

    [Theory]
    [InlineData("1.2.3", "1.2.3", false)]
    [InlineData("1.2.3", "1.2.3", null)]
    [InlineData(null, "1.2.4", null)]
    [InlineData("1.2.3", null, null)]
    public void NothingKnownToHaveChangedIsNoReset(string? whenOn, string? now, bool? correctionOn) =>
        Assert.False(ParametricOff.ResetBySwarm(whenOn, now, correctionOn));

    [Theory]
    [InlineData(true, ParametricOffSaying.ResetBySwarm)]
    [InlineData(false, ParametricOffSaying.IsOff)]
    public void EachStateHasItsSaying(bool reset, ParametricOffSaying expected) =>
        Assert.Equal(expected, ParametricOff.Say(reset));
}
