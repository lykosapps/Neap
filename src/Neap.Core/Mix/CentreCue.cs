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

    /// <summary>Plays the beep, and returns once it has finished or two seconds have passed.</summary>
    /// <remarks>A cue that will not play is not worth an error, so nothing is thrown.</remarks>
    public static void Play()
    {
        try
        {
            if (OperatingSystem.IsWindows()) PlayOnWindows();
            else if (OperatingSystem.IsLinux()) PlayOnLinux();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Nothing to tell anybody: see the remarks.
        }
    }

    /// <summary>Mono, 16-bit, little-endian, at <paramref name="rate"/>.</summary>
    internal static byte[] Tone(int rate)
    {
        int total = (int)(rate * Seconds), fade = (int)(rate * 0.012);
        var pcm = new byte[total * 2];
        for (int i = 0; i < total; i++)
        {
            double amplitude = 0.25;
            if (i < fade) amplitude *= (double)i / fade;
            else if (i > total - fade) amplitude *= (double)(total - i) / fade;
            short sample = (short)(amplitude * short.MaxValue * Math.Sin(2 * Math.PI * Frequency * i / rate));
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        return pcm;
    }

    [SupportedOSPlatform("windows")]
    private static void PlayOnWindows()
    {
        using var devices = new MMDeviceEnumerator();
        using var headset = Routing.Headset(devices, output: true);
        if (headset is null) return;

        using var player = new WasapiPlayerBuilder()
            .WithDevice(headset).WithSharedMode().WithPollingSync().WithLatency(60)
            .Build();

        // Made at the device's own rate, so nothing has to convert it.
        int rate = player.DeviceMixFormat.SampleRate;
        using var source = new RawSourceWaveStream(new MemoryStream(Tone(rate)), new WaveFormat(rate, 16, 1));
        using var finished = new ManualResetEventSlim();
        player.PlaybackStopped += (_, _) => finished.Set();
        player.Init(source);
        player.Play();
        finished.Wait(TimeSpan.FromSeconds(2));
    }

    [SupportedOSPlatform("linux")]
    private static void PlayOnLinux()
    {
        // The sound server resamples, so any rate will do.
        const int rate = 48000;
        PulseNative.RequireSimple();
        using var client = new PulseClient();
        string listening = client.DefaultSink();
        if (client.Sinks().FirstOrDefault(s => s.Name == listening) is not { IsHeadset: true }) return;

        var spec = new PulseNative.SampleSpec { Format = PulseNative.SampleS16Le, Rate = rate, Channels = 1 };
        IntPtr stream = PulseNative.SimpleNew(IntPtr.Zero, PulseNative.Utf8("Neap"), PulseNative.StreamPlayback,
            PulseNative.Utf8(listening), PulseNative.Utf8("Centre of the mix"), ref spec, IntPtr.Zero, IntPtr.Zero, out _);
        if (stream == IntPtr.Zero) return;
        try
        {
            byte[] pcm = Tone(rate);
            if (PulseNative.SimpleWrite(stream, pcm, (nuint)pcm.Length, out _) >= 0)
                _ = PulseNative.SimpleDrain(stream, out _);
        }
        finally
        {
            PulseNative.SimpleFree(stream);
        }
    }
}
