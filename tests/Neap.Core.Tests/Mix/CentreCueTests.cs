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
    public void TheWindowsBeepIsThe16BitBeepWithAHissBeforeIt()
    {
        int beep = CentreCue.Tone(Rate).Length / 2;
        int hiss = (int)(Rate * CentreCue.WakeSeconds);

        Assert.Equal(hiss + beep, Floats(CentreCue.FloatTone(Rate, 1)).Length);
    }

    [Fact]
    public void TheHissBeforeTheBeepIsNotSilenceAndTooQuietToHear()
    {
        var samples = Floats(CentreCue.FloatTone(Rate, 1));
        var hiss = samples.Take((int)(Rate * CentreCue.WakeSeconds)).ToArray();

        Assert.Contains(hiss, sample => sample != 0);
        Assert.InRange(hiss.Max(Math.Abs), 0f, 0.001f);
    }

    [Fact]
    public void ItFadesInAndOutRatherThanClicking()
    {
        var samples = Floats(CentreCue.FloatTone(Rate, 1));
        int start = (int)(Rate * CentreCue.WakeSeconds);

        Assert.True(Math.Abs(samples[start]) < 0.001f);
        Assert.True(Math.Abs(samples[^1]) < 0.01f);
    }
}
