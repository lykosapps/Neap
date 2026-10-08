using NAudio.Wave;

namespace Neap.Core.Mix;

/// <summary>A hiss too quiet to hear, without end, with the centre beep laid over it when asked.</summary>
/// <remarks>
/// <para>
/// The headset's wireless link wakes on sound but not on digital silence, loses
/// what plays while it wakes, and sleeps again after about three seconds of
/// quiet. The hiss keeps it awake, so a beep laid over it is heard at once.
/// </para>
/// <para>
/// <see cref="Beep"/> is called from a worker thread and <see cref="Read"/> from
/// the audio thread, so both take the same lock.
/// </para>
/// </remarks>
/// <param name="rate">Samples per second.</param>
/// <param name="channels">Channels, each given the same sound.</param>
internal sealed class CueSound(int rate, int channels) : ISampleProvider
{
    /// <summary>How long the hiss plays before a beep can be heard, in seconds.</summary>
    /// <remarks>Measured on the headset: 100 ms clipped the start of the beep, 200 ms did not.</remarks>
    internal const double WakeSeconds = 0.3;

    /// <summary>The loudest the hiss gets: about -66 dB, too quiet to hear.</summary>
    internal const float HissLevel = 0.0005f;

    private readonly float[] _beep = CentreCue.Beep(rate);
    private readonly long _awake = (long)(rate * WakeSeconds);
    private readonly Lock _gate = new();
    private long _frame;
    private long? _beepAt;

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(rate, channels);

    /// <summary>Sounds the beep in the next frames read, or once the hiss has woken the link.</summary>
    public void Beep()
    {
        lock (_gate) _beepAt = Math.Max(_frame, _awake);
    }

    public int Read(Span<float> buffer)
    {
        lock (_gate)
        {
            int frames = buffer.Length / channels;
            for (int i = 0; i < frames; i++)
            {
                long into = _beepAt is long at ? _frame + i - at : -1;
                float sample = into >= 0 && into < _beep.Length
                    ? _beep[into]
                    : (Random.Shared.NextSingle() * 2 - 1) * HissLevel;
                for (int channel = 0; channel < channels; channel++)
                    buffer[i * channels + channel] = sample;
            }
            _frame += frames;
            return frames * channels;
        }
    }
}
