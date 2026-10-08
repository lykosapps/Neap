using Neap.Core.Mix;

namespace Neap.Core.Tests.Mix;

public class CentreCueTests
{
    private const int Rate = 48000;

    private static readonly int Awake = (int)(Rate * CueSound.WakeSeconds);

    private static readonly int BeepLength = CentreCue.Beep(Rate).Length;

    private static float[] Read(CueSound sound, int frames, int channels = 1)
    {
        var samples = new float[frames * channels];
        Assert.Equal(samples.Length, sound.Read(samples));
        return samples;
    }

    private static bool Heard(IEnumerable<float> samples) => samples.Any(s => Math.Abs(s) > 0.01f);

    [Fact]
    public void WithNoBeepAskedForItIsAHissThatIsNotSilenceAndTooQuietToHear()
    {
        var samples = Read(new CueSound(Rate, 1), Rate);

        Assert.Contains(samples, sample => sample != 0);
        Assert.InRange(samples.Max(Math.Abs), 0f, CueSound.HissLevel);
    }

    [Fact]
    public void ABeepOnALineJustOpenedWaitsForTheHissToWakeTheLink()
    {
        var sound = new CueSound(Rate, 1);
        sound.Beep();

        var samples = Read(sound, Awake + BeepLength);

        Assert.False(Heard(samples.Take(Awake)));
        Assert.True(Heard(samples.Skip(Awake)));
    }

    [Fact]
    public void ABeepOnALineAlreadyAwakePlaysAtOnce()
    {
        var sound = new CueSound(Rate, 1);
        Read(sound, Awake);
        sound.Beep();

        Assert.True(Heard(Read(sound, Rate / 20)));
    }

    [Fact]
    public void TheBeepEndsAndTheHissCarriesOn()
    {
        var sound = new CueSound(Rate, 1);
        Read(sound, Awake);
        sound.Beep();
        Read(sound, BeepLength);

        var after = Read(sound, Rate);

        Assert.InRange(after.Max(Math.Abs), 0f, CueSound.HissLevel);
    }

    [Fact]
    public void EveryChannelGetsTheSameSound()
    {
        var sound = new CueSound(Rate, 2);
        sound.Beep();

        var samples = Read(sound, Awake + BeepLength, channels: 2);

        for (int frame = 0; frame < samples.Length / 2; frame++)
            Assert.Equal(samples[frame * 2], samples[frame * 2 + 1]);
    }

    [Fact]
    public void ItFadesInAndOutRatherThanClicking()
    {
        var beep = CentreCue.Beep(Rate);

        Assert.InRange(beep.Max(Math.Abs), 0.2f, 0.26f);
        Assert.True(Math.Abs(beep[0]) < 0.001f);
        Assert.True(Math.Abs(beep[^1]) < 0.01f);
    }

    [Fact]
    public void TheLinuxBeepIsTheSameBeepIn16Bit()
    {
        Assert.Equal(BeepLength * 2, CentreCue.Tone(Rate).Length);
    }
}
