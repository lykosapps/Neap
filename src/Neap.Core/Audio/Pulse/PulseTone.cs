using System.Runtime.Versioning;

namespace Neap.Core.Audio.Pulse;

/// <summary>
/// Plays a test tone on the headset through the sound server:
/// <see cref="TestTone"/>'s counterpart.
/// </summary>
/// <remarks>
/// <para>
/// The tone goes to the headset's own output whatever the desktop's default
/// is, since the equaliser it is testing lives in the headset. It is a stream
/// like any application's, so the mix leaves it alone.
/// </para>
/// <para>
/// A thread writes it in twenty-millisecond pieces, held back by the sound
/// server as it fills, so the frequency set from the screen is heard within
/// a few pieces. It opens silent, fades in when given a loudness and fades
/// out before the stream closes, so it never starts or stops with a click.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed class PulseTone : IPlayingTone
{
    private const int Rate = 48000;
    private const int Frames = Rate / 50;

    private readonly ToneGenerator _tone;
    private readonly IntPtr _stream;
    private readonly Thread _writer;
    private volatile bool _closing;
    private int _closed;

    private PulseTone(IntPtr stream, double frequency)
    {
        _stream = stream;
        _tone = new ToneGenerator(Rate, 1, frequency);
        _writer = new Thread(Write) { IsBackground = true, Name = "test tone" };
        _writer.Start();
    }

    public event Action<Exception>? Stopped;

    public double Frequency
    {
        get => _tone.Frequency;
        set => _tone.Frequency = value;
    }

    public double Amplitude
    {
        get => _tone.Amplitude;
        set => _tone.Amplitude = value;
    }

    /// <summary>Starts a silent tone on the headset's output.</summary>
    /// <exception cref="PulseException">The sound server could not be asked, or would not open the headset's output.</exception>
    public static PulseTone Open(double frequency)
    {
        string sink;
        using (var client = new PulseClient()) sink = PulseVolumes.Find(client, Flow.Output).Device.Name;

        var spec = new PulseNative.SampleSpec { Format = PulseNative.SampleFloat32Le, Rate = Rate, Channels = 1 };
        var attributes = new PulseNative.BufferAttributes
        {
            MaxLength = PulseNative.ServerChoice,
            TargetLength = Frames * 3 * sizeof(float),
            PreBuffer = PulseNative.ServerChoice,
            MinRequest = PulseNative.ServerChoice,
            FragmentSize = PulseNative.ServerChoice,
        };
        IntPtr stream = PulseNative.SimpleNewWithBuffer(IntPtr.Zero, PulseNative.Utf8("Neap"),
            PulseNative.StreamPlayback, PulseNative.Utf8(sink), PulseNative.Utf8("Test tone"),
            ref spec, IntPtr.Zero, ref attributes, out int error);
        return stream == IntPtr.Zero
            ? throw new PulseException($"could not play to {sink} (error {error})")
            : new PulseTone(stream, frequency);
    }

    private void Write()
    {
        var samples = new float[Frames];
        var bytes = new byte[Frames * sizeof(float)];
        while (!_closing)
        {
            _tone.Read(samples);
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            if (PulseNative.SimpleWrite(_stream, bytes, (nuint)bytes.Length, out int error) < 0)
            {
                if (!_closing) Stopped?.Invoke(new PulseException($"the test tone stopped playing (error {error})"));
                return;
            }
        }
    }

    /// <summary>Fades the tone out, then closes the stream.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1) return;
        _tone.Amplitude = 0;
        // Long enough for the fade and what is already queued to play out.
        Task.Delay(TimeSpan.FromSeconds(ToneGenerator.FadeSeconds) + TimeSpan.FromMilliseconds(150))
            .ContinueWith(_ =>
            {
                _closing = true;
                _writer.Join(TimeSpan.FromSeconds(2));
                PulseNative.SimpleFree(_stream);
            }, TaskScheduler.Default);
    }
}
