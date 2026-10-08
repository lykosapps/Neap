using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Neap.Core.Audio;
using Neap.Core.Audio.Pulse;

namespace Neap.Core.Mix;

/// <summary>A short, soft beep on reaching centre, played straight to the headset.</summary>
/// <remarks>
/// Not the default output: the mix is about the headset, and the person may
/// well be listening on it while the system plays elsewhere. Generated rather
/// than shipped, so there is no audio asset to carry.
/// </remarks>
public static class CentreCue
{
    private const double Seconds = 0.13, Frequency = 620.0;

    /// <summary>How long the hiss before the beep lasts, in seconds.</summary>
    internal const double WakeSeconds = 0.3;

    /// <summary>The loudest the hiss gets: about -66 dB, too quiet to hear.</summary>
    private const float HissLevel = 0.0005f;

    /// <summary>How long the headset stays awake after the last sound it was sent.</summary>
    /// <remarks>
    /// Measured on the headset: a beep after 6.8 s of quiet was heard every
    /// time, and one after 7.5 s was lost every time. A second less, for margin.
    /// </remarks>
    internal static readonly TimeSpan Awake = TimeSpan.FromSeconds(6);

    /// <summary>When the last beep finished, from <see cref="Environment.TickCount64"/>.</summary>
    private static long _lastBeep = long.MinValue / 2;

    /// <summary>Where a cue that would not play is said, for the app's log. Unset, nothing is.</summary>
    public static Action<string>? Trouble { get; set; }

    /// <summary>Plays the beep, and returns once it has finished or two seconds have passed.</summary>
    /// <remarks>A cue that will not play is not worth an error, so nothing is thrown; it is said to <see cref="Trouble"/>.</remarks>
    public static void Play()
    {
        try
        {
            if (OperatingSystem.IsWindows()) PlayOnWindows();
            else if (OperatingSystem.IsLinux()) PlayOnLinux();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trouble?.Invoke($"the centre cue would not play: {ex.Message}");
        }
    }

    /// <summary>Mono, 16-bit, little-endian, at <paramref name="rate"/>.</summary>
    internal static byte[] Tone(int rate)
    {
        float[] beep = Beep(rate);
        var pcm = new byte[beep.Length * 2];
        for (int i = 0; i < beep.Length; i++)
        {
            short sample = (short)(beep[i] * short.MaxValue);
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        return pcm;
    }

    /// <summary>Whether the headset has to be woken before a beep can be heard.</summary>
    /// <param name="playing">Whether anything is playing on the headset now.</param>
    /// <param name="sinceBeep">How long since the last beep finished.</param>
    /// <remarks>
    /// After <see cref="Awake"/> of quiet the headset sleeps, wakes on sound but
    /// not on digital silence, and loses what plays while it wakes. Anything
    /// playing keeps it awake, as a game does, and so does a beep a moment ago.
    /// </remarks>
    internal static bool Asleep(bool playing, TimeSpan sinceBeep) => !playing && sinceBeep >= Awake;

    /// <summary>
    /// 32-bit float at <paramref name="rate"/>, every one of <paramref name="channels"/> given the same
    /// sound: the beep, after a hiss for <see cref="WakeSeconds"/> when <paramref name="wake"/> is set.
    /// </summary>
    /// <remarks>
    /// Measured on the headset, a hiss of 100 ms before the beep clipped its start and 200 ms did not.
    /// </remarks>
    internal static byte[] FloatTone(int rate, int channels, bool wake)
    {
        float[] hiss = wake ? Hiss(rate) : [];
        float[] beep = Beep(rate);
        var samples = new float[(hiss.Length + beep.Length) * channels];
        for (int i = 0; i < hiss.Length + beep.Length; i++)
            for (int channel = 0; channel < channels; channel++)
                samples[i * channels + channel] = i < hiss.Length ? hiss[i] : beep[i - hiss.Length];
        var bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] Hiss(int rate)
    {
        var hiss = new float[(int)(rate * WakeSeconds)];
        for (int i = 0; i < hiss.Length; i++)
            hiss[i] = (Random.Shared.NextSingle() * 2 - 1) * HissLevel;
        return hiss;
    }

    private static float[] Beep(int rate)
    {
        int total = (int)(rate * Seconds), fade = (int)(rate * 0.012);
        var beep = new float[total];
        for (int i = 0; i < total; i++)
        {
            double amplitude = 0.25;
            if (i < fade) amplitude *= (double)i / fade;
            else if (i > total - fade) amplitude *= (double)(total - i) / fade;
            beep[i] = (float)(amplitude * Math.Sin(2 * Math.PI * Frequency * i / rate));
        }
        return beep;
    }

    [SupportedOSPlatform("windows")]
    private static void PlayOnWindows()
    {
        using var devices = new MMDeviceEnumerator();
        using var headset = Routing.Headset(devices, output: true);
        if (headset is null)
        {
            Trouble?.Invoke("the centre cue found no headset output to play to");
            return;
        }

        using var player = new WasapiPlayerBuilder()
            .WithDevice(headset).WithSharedMode().WithPollingSync().WithLatency(60)
            .Build();

        bool wake = Asleep(
            headset.AudioMeterInformation.MasterPeakValue > 0,
            TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _lastBeep)));

        // Made in the device's own format, as the player does not convert: a
        // beep in any other plays as silence, with no error.
        var format = player.DeviceMixFormat;
        using var source = new RawSourceWaveStream(
            new MemoryStream(FloatTone(format.SampleRate, format.Channels, wake)),
            WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate, format.Channels));
        using var finished = new ManualResetEventSlim();
        player.PlaybackStopped += (_, _) => finished.Set();
        player.Init(source);
        player.Play();
        finished.Wait(TimeSpan.FromSeconds(2));
        Interlocked.Exchange(ref _lastBeep, Environment.TickCount64);
    }

    [SupportedOSPlatform("linux")]
    private static void PlayOnLinux()
    {
        // The sound server resamples, so any rate will do.
        const int rate = 48000;
        PulseNative.RequireSimple();
        using var client = new PulseClient();
        string listening = client.DefaultSink();
        if (client.Sinks().FirstOrDefault(s => s.Name == listening) is not { IsHeadset: true })
        {
            Trouble?.Invoke("the centre cue found no headset output to play to");
            return;
        }

        var spec = new PulseNative.SampleSpec { Format = PulseNative.SampleS16Le, Rate = rate, Channels = 1 };
        IntPtr stream = PulseNative.SimpleNew(IntPtr.Zero, PulseNative.Utf8("Neap"), PulseNative.StreamPlayback,
            PulseNative.Utf8(listening), PulseNative.Utf8("Centre of the mix"), ref spec, IntPtr.Zero, IntPtr.Zero, out int error);
        if (stream == IntPtr.Zero) throw new PulseException($"could not play to {listening} (error {error})");
        try
        {
            byte[] pcm = Tone(rate);
            if (PulseNative.SimpleWrite(stream, pcm, (nuint)pcm.Length, out error) < 0)
                throw new PulseException($"the sound server refused the beep (error {error})");
            _ = PulseNative.SimpleDrain(stream, out _);
        }
        finally
        {
            PulseNative.SimpleFree(stream);
        }
    }
}
