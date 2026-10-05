using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Neap.Core.Audio;

/// <summary>
/// Plays a test tone on the headset, for finding frequencies that stand out.
/// </summary>
/// <remarks>
/// <para>
/// The tone goes to the headset's own output whatever Windows' default is,
/// since the equaliser it is testing lives in the headset. It is a stream
/// like any application's, not a change to anyone else's sound; the mix
/// leaves Neap's own sessions alone.
/// </para>
/// <para>
/// It opens silent and fades in when given a loudness, and fades out before
/// the stream closes, so it never starts or stops with a click.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class TestTone : IPlayingTone
{
    private readonly MMDeviceEnumerator _devices;
    private readonly MMDevice _device;
    private readonly WasapiPlayer _player;
    private readonly ToneGenerator _tone;
    private int _closed;

    private TestTone(MMDeviceEnumerator devices, MMDevice device, double frequency)
    {
        _devices = devices;
        _device = device;
        _player = new WasapiPlayerBuilder()
            .WithDevice(device).WithSharedMode().WithLatency(60)
            .Build();
        var mix = _player.DeviceMixFormat;
        _tone = new ToneGenerator(mix.SampleRate, mix.Channels, frequency);
        _player.PlaybackStopped += (_, args) =>
        {
            if (args.Exception is not null) Stopped?.Invoke(args.Exception);
        };
        _player.Init(new SampleToWaveProvider(_tone));
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
    /// <exception cref="WindowsAudioException">The headset has no output Windows can play to.</exception>
    public static TestTone Open(double frequency, string match = AudioEndpoints.DefaultMatch)
    {
        MMDeviceEnumerator? devices = null;
        MMDevice? device;
        try
        {
            devices = new MMDeviceEnumerator();
            device = Routing.Headset(devices, output: true, match);
        }
        catch (COMException e)
        {
            devices?.Dispose();
            throw new WindowsAudioException($"could not list playback devices: {e.Message}");
        }
        if (device is null)
        {
            devices.Dispose();
            throw new WindowsAudioException($"no playback device matching '{match}'");
        }

        TestTone? tone = null;
        try
        {
            tone = new TestTone(devices, device, frequency);
            tone._player.Play();
            return tone;
        }
        catch (Exception e) when (e is not WindowsAudioException)
        {
            string name = device.FriendlyName;
            if (tone is not null) tone.Dispose();
            else { device.Dispose(); devices.Dispose(); }
            throw new WindowsAudioException($"could not play to {name}: {e.Message}");
        }
    }

    /// <summary>Fades the tone out, then closes the stream.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1) return;
        _tone.Amplitude = 0;
        // Long enough for the fade and what is already queued to play out.
        Task.Delay(TimeSpan.FromSeconds(ToneGenerator.FadeSeconds) + TimeSpan.FromMilliseconds(120))
            .ContinueWith(_ =>
            {
                _player.Stop();
                _player.Dispose();
                _device.Dispose();
                _devices.Dispose();
            }, TaskScheduler.Default);
    }
}
