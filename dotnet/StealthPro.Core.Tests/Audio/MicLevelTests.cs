using StealthPro.Core.Audio;

namespace StealthPro.Core.Tests.Audio;

public class MicLevelTests
{
    private static byte[] Floats(params float[] samples) =>
        samples.SelectMany(BitConverter.GetBytes).ToArray();

    [Fact]
    public void ThePeakIsTheLoudestSampleEitherSideOfZero() =>
        Assert.Equal(0.8f, MicLevel.PeakOf(Floats(0.1f, -0.8f, 0.5f)), 6);

    [Fact]
    public void SilenceHasNoPeak() =>
        Assert.Equal(0f, MicLevel.PeakOf(Floats(0f, 0f)));

    [Fact]
    public void APartialSampleAtTheEndIsIgnored()
    {
        var bytes = Floats(0.25f).Concat(new byte[] { 0xFF, 0xFF }).ToArray();
        Assert.Equal(0.25f, MicLevel.PeakOf(bytes), 6);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, 20)]
    [InlineData(0.001f, 0)]
    [InlineData(0.01f, 7)]
    [InlineData(0.1f, 13)]
    public void TheBarsFollowDecibels(float peak, int lit) =>
        Assert.Equal(lit, MicLevel.Lit(peak, 20));

    [Fact]
    public void AnOverloadLightsEveryBarAndNoMore() =>
        Assert.Equal(20, MicLevel.Lit(1.5f, 20));

    [Theory]
    [InlineData(10, 2, 9)]
    [InlineData(3, 12, 12)]
    [InlineData(0, 0, 0)]
    public void TheMeterFallsABarAtATime(int shown, int heard, int next) =>
        Assert.Equal(next, MicLevel.Fall(shown, heard));
}
