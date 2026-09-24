using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace StealthPro.Core.Audio;

/// <summary>
/// Listens to the headset's microphone, for a level meter, for as long as it
/// is open.
/// </summary>
/// <remarks>
/// <para>
/// Windows' own meter on a recording device reads zero unless something is
/// recording from it, so a meter has to record. This opens a shared stream,
/// which leaves other applications' use of the microphone alone, and keeps
/// only the loudest sample since it was last asked; the sound itself is not
/// kept or sent anywhere. While it is open Windows shows its microphone in
/// use indicator, which is why the app opens it only while the page with the
/// meter is on screen.
/// </para>
/// <para>
/// The stream is asked for 32-bit float samples, which Windows converts to
/// in shared mode whatever the device's own format, so there is one sample
/// format to read.
/// </para>
/// </remarks>
public sealed class MicrophoneListener : IDisposable
{
    private readonly object _gate = new();
    private readonly MMDeviceEnumerator _devices;
    private readonly MMDevice _device;
    private readonly WasapiRecorder _capture;
    private float _peak;

    private MicrophoneListener(MMDeviceEnumerator devices, MMDevice device)
    {
        _devices = devices;
        _device = device;
        WaveFormat mix;
        using (var client = device.CreateAudioClient()) mix = client.MixFormat;
        _capture = new WasapiRecorderBuilder()
            .WithDevice(device)
            .WithSharedMode()
            .WithFormat(WaveFormat.CreateIeeeFloatWaveFormat(mix.SampleRate, mix.Channels))
            .Build();
        _capture.DataAvailable += (buffer, _, _, _) =>
        {
            float peak = MicLevel.PeakOf(buffer);
            lock (_gate) _peak = Math.Max(_peak, peak);
        };
        _capture.RecordingStopped += (_, args) =>
        {
            if (args.Exception is not null) Stopped?.Invoke(args.Exception);
        };
    }

    /// <summary>Raised, on the recording thread, when recording stops on its own because of a fault.</summary>
    public event Action<Exception>? Stopped;

    /// <summary>Starts listening to the headset's microphone.</summary>
    /// <exception cref="WindowsAudioException">The headset has no microphone Windows can record from.</exception>
    public static MicrophoneListener Open(string match = AudioEndpoints.DefaultMatch)
    {
        var devices = new MMDeviceEnumerator();
        var device = Routing.Headset(devices, output: false, match);
        if (device is null)
        {
            devices.Dispose();
            throw new WindowsAudioException($"no recording device matching '{match}'");
        }

        MicrophoneListener? listener = null;
        try
        {
            listener = new MicrophoneListener(devices, device);
            listener._capture.StartRecording();
            return listener;
        }
        catch (Exception e) when (e is not WindowsAudioException)
        {
            string name = device.FriendlyName;
            if (listener is not null) listener.Dispose();
            else { device.Dispose(); devices.Dispose(); }
            throw new WindowsAudioException($"could not record from {name}: {e.Message}");
        }
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

    public void Dispose()
    {
        _capture.StopRecording();
        _capture.Dispose();
        _device.Dispose();
        _devices.Dispose();
    }
}
