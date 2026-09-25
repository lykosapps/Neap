using NAudio.Wave;

namespace Neap.Core.Audio;

/// <summary>
/// A sine tone whose frequency and loudness can be changed while it plays,
/// without clicks.
/// </summary>
/// <remarks>
/// <para>
/// Loudness moves in a short ramp rather than a step, so starting, stopping
/// and changing level fade instead of clicking. Frequency glides over about
/// ten milliseconds, on a logarithmic scale as heard, so a sweep or a drag
/// sounds continuous, and the phase carries on unbroken through every change.
/// </para>
/// <para>
/// The setters are called from the UI thread and <see cref="Read"/> from the
/// audio thread; each is a single value read whole, so no lock is needed.
/// </para>
/// </remarks>
public sealed class ToneGenerator : ISampleProvider
{
    /// <summary>How long loudness takes to go from silence to full scale.</summary>
    public const double FadeSeconds = 0.03;

    private const double GlideSeconds = 0.01;

    private readonly int _rate;
    private readonly int _channels;
    private double _phase;
    private double _frequency;
    private double _amplitude;
    private volatile float _targetFrequency;
    private volatile float _targetAmplitude;

    /// <param name="rate">Samples per second.</param>
    /// <param name="channels">Channels, each given the same tone.</param>
    /// <param name="frequency">The frequency to start at, in hertz.</param>
    public ToneGenerator(int rate, int channels, double frequency)
    {
        _rate = rate;
        _channels = channels;
        _frequency = frequency;
        _targetFrequency = (float)frequency;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, channels);
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Gets or sets the frequency to play, in hertz.</summary>
    public double Frequency
    {
        get => _targetFrequency;
        set => _targetFrequency = (float)value;
    }

    /// <summary>Gets or sets the loudness to play at, from 0 (silent) to 1 (full scale).</summary>
    public double Amplitude
    {
        get => _targetAmplitude;
        set => _targetAmplitude = (float)Math.Clamp(value, 0, 1);
    }

    public int Read(Span<float> buffer)
    {
        int frames = buffer.Length / _channels;
        double ramp = 1 / (FadeSeconds * _rate);
        double glide = 1 - Math.Exp(-1 / (GlideSeconds * _rate));
        double targetFrequency = _targetFrequency, targetAmplitude = _targetAmplitude;

        for (int frame = 0; frame < frames; frame++)
        {
            _amplitude += Math.Clamp(targetAmplitude - _amplitude, -ramp, ramp);
            _frequency *= Math.Pow(targetFrequency / _frequency, glide);
            _phase += 2 * Math.PI * _frequency / _rate;
            if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;

            float sample = (float)(_amplitude * Math.Sin(_phase));
            for (int channel = 0; channel < _channels; channel++)
                buffer[frame * _channels + channel] = sample;
        }
        return frames * _channels;
    }
}
