using System.Runtime.Versioning;

namespace Neap.Core.Audio.Pulse;

/// <summary>
/// Listens to the headset's microphone through the sound server, for a level
/// meter: <see cref="MicrophoneListener"/>'s counterpart.
/// </summary>
/// <remarks>
/// <para>
/// A recording stream is opened on the headset's microphone with a small
/// fragment, so the meter follows speech rather than lagging it, and only the
/// loudest sample since it was last asked is kept. While it is open the
/// desktop shows its microphone in use indicator, which is why the app opens
/// it only while the screen with the meter is showing.
/// </para>
/// <para>
/// Samples are asked for as 32-bit floats whatever the device's own format,
/// so there is one format to read.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed class PulseListener : IMicrophoneListener
{
    private const int Rate = 48000;

    /// <summary>About twenty milliseconds of one channel of floats.</summary>
    private const int FragmentBytes = Rate / 50 * sizeof(float);

    private readonly object _gate = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly IntPtr _stream;
    private readonly Thread _reader;
    private float _peak;

    private PulseListener(IntPtr stream)
    {
        _stream = stream;
        _reader = new Thread(Read) { IsBackground = true, Name = "microphone meter" };
        _reader.Start();
    }

    public event Action<Exception>? Stopped;

    /// <summary>Starts listening to the headset's microphone.</summary>
    /// <exception cref="PulseException">The sound server could not be asked, or would not open the microphone.</exception>
    public static PulseListener Open()
    {
        string source;
        using (var client = new PulseClient()) source = PulseVolumes.Find(client, Flow.Input).Device.Name;

        var spec = new PulseNative.SampleSpec { Format = PulseNative.SampleFloat32Le, Rate = Rate, Channels = 1 };
        var attributes = new PulseNative.BufferAttributes
        {
            MaxLength = PulseNative.ServerChoice,
            TargetLength = PulseNative.ServerChoice,
            PreBuffer = PulseNative.ServerChoice,
            MinRequest = PulseNative.ServerChoice,
            FragmentSize = FragmentBytes,
        };
        IntPtr stream = PulseNative.SimpleNewWithBuffer(IntPtr.Zero, PulseNative.Utf8("Neap"),
            PulseNative.StreamRecord, PulseNative.Utf8(source), PulseNative.Utf8("Microphone level"),
            ref spec, IntPtr.Zero, ref attributes, out int error);
        return stream == IntPtr.Zero
            ? throw new PulseException($"could not record from {source} (error {error})")
            : new PulseListener(stream);
    }

    /// <summary>Takes the loudest level heard since the last take, from 0 to 1.</summary>
    public float Take()
    {
        lock (_gate)
        {
            float peak = _peak;
            _peak = 0;
            return peak;
        }
    }

    private void Read()
    {
        var buffer = new byte[FragmentBytes];
        while (!_stopping.IsCancellationRequested)
        {
            if (PulseNative.SimpleRead(_stream, buffer, (nuint)buffer.Length, out int error) < 0)
            {
                if (!_stopping.IsCancellationRequested)
                    Stopped?.Invoke(new PulseException($"the microphone stopped being read (error {error})"));
                return;
            }
            float peak = MicLevel.PeakOf(buffer);
            lock (_gate) _peak = Math.Max(_peak, peak);
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();
        // The reader returns within a fragment; the stream is freed after it has.
        _reader.Join(TimeSpan.FromSeconds(2));
        PulseNative.SimpleFree(_stream);
        _stopping.Dispose();
    }
}
