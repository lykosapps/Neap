using Neap.Core.Audio;

namespace Neap.Core.Tests.Audio;

public class ToneGeneratorTests
{
    private const int Rate = 48000;

    private static float[] Play(ToneGenerator tone, double seconds, int channels = 1)
    {
        var buffer = new float[(int)(Rate * seconds) * channels];
        tone.Read(buffer);
        return buffer;
    }

    [Fact]
    public void ItIsSilentUntilGivenALoudness()
    {
        var tone = new ToneGenerator(Rate, 1, 1000);
        Assert.All(Play(tone, 0.05), sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void ItFadesInRatherThanStartingAtFullLoudness()
    {
        var tone = new ToneGenerator(Rate, 1, 1000) { Amplitude = 0.5 };
        var samples = Play(tone, 0.1);
        float early = samples.Take(Rate / 1000).Max(Math.Abs);
        float later = samples.Skip(Rate / 20).Max(Math.Abs);
        Assert.True(early < 0.05f);
        Assert.Equal(0.5, later, 2);
    }

    [Fact]
    public void ItFadesOutRatherThanStopping()
    {
        var tone = new ToneGenerator(Rate, 1, 1000) { Amplitude = 0.5 };
        Play(tone, 0.1);
        tone.Amplitude = 0;
        var samples = Play(tone, 0.1);
        Assert.True(samples.Take(Rate / 1000).Max(Math.Abs) > 0.3f);
        Assert.All(samples.Skip(Rate / 20), sample => Assert.Equal(0, sample, 4));
    }

    [Fact]
    public void AFrequencyChangeDoesNotJump()
    {
        var tone = new ToneGenerator(Rate, 1, 1000) { Amplitude = 0.5 };
        var before = Play(tone, 0.1);
        tone.Frequency = 4000;
        var after = Play(tone, 0.1);

        // Unbroken phase: no step between one sample and the next is bigger
        // than the higher frequency can make.
        var all = before.Concat(after).ToArray();
        double largest = 0.5 * 2 * Math.PI * 4000 / Rate;
        for (int i = 1; i < all.Length; i++)
            Assert.True(Math.Abs(all[i] - all[i - 1]) <= largest + 1e-3);
    }

    [Fact]
    public void ItPlaysAtTheFrequencyAsked()
    {
        var tone = new ToneGenerator(Rate, 1, 1000) { Amplitude = 0.5 };
        var samples = Play(tone, 1);
        int rising = 0;
        for (int i = 1; i < samples.Length; i++)
            if (samples[i - 1] < 0 && samples[i] >= 0) rising++;
        Assert.InRange(rising, 998, 1001);
    }

    [Fact]
    public void EveryChannelGetsTheSameTone()
    {
        var tone = new ToneGenerator(Rate, 2, 1000) { Amplitude = 0.5 };
        var samples = Play(tone, 0.05, channels: 2);
        for (int i = 0; i < samples.Length; i += 2) Assert.Equal(samples[i], samples[i + 1]);
    }
}
