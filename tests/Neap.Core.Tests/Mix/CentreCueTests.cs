using Neap.Core.Mix;

namespace Neap.Core.Tests.Mix;

public class CentreCueTests
{
    private const int Rate = 48000;

    private static float[] Floats(byte[] bytes)
    {
        var samples = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
        return samples;
    }

    [Fact]
    public void TheWindowsBeepIsMadeInTheDevicesOwnFormat()
    {
        var samples = Floats(CentreCue.FloatTone(Rate, 2));

        Assert.Equal(0, samples.Length % 2);
        Assert.InRange(samples.Max(Math.Abs), 0.2f, 0.26f);
    }

    [Fact]
    public void EveryChannelGetsTheSameBeep()
    {
        var samples = Floats(CentreCue.FloatTone(Rate, 2));

        for (int frame = 0; frame < samples.Length / 2; frame++)
            Assert.Equal(samples[frame * 2], samples[frame * 2 + 1]);
    }

    [Fact]
    public void ItIsTheSameLengthWhateverTheFormat()
    {
        Assert.Equal(CentreCue.Tone(Rate).Length / 2, Floats(CentreCue.FloatTone(Rate, 1)).Length);
    }

    [Fact]
    public void ItFadesInAndOutRatherThanClicking()
    {
        var samples = Floats(CentreCue.FloatTone(Rate, 1));

        Assert.True(Math.Abs(samples[0]) < 0.001f);
        Assert.True(Math.Abs(samples[^1]) < 0.01f);
    }
}
