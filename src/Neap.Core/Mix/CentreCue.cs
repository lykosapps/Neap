using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Neap.Core.Audio;
using Neap.Core.Audio.Pulse;

namespace Neap.Core.Mix;

/// <summary>A short, soft beep on reaching centre, played straight to the headset.</summary>
/// <remarks>
/// <para>
/// Not the default output: the mix is about the headset, and the person may
/// well be listening on it while the system plays elsewhere. Generated rather
/// than shipped, so there is no audio asset to carry.
/// </para>
/// <para>
/// On Windows the beep is laid over a hiss too quiet to hear, on a line to the
/// headset that opens as soon as the mix moves and closes once it has been
/// still for a few seconds. By the time the mix reaches centre the headset's
/// link is awake, so the beep is heard at once; see <see cref="CueSound"/>.
/// </para>
/// </remarks>
public static class CentreCue
{
    private const double Seconds = 0.13, Frequency = 620.0;

    /// <summary>How long the line to the headset stays open after the mix last moved.</summary>
    private static readonly TimeSpan KeepOpen = TimeSpan.FromSeconds(5);

    private static readonly Lock Gate = new();
    private static readonly Timer Idle = new(_ => Close());
    private static IDisposable? _line;
    private static CueSound? _sound;
    private static long _usedAt;

    /// <summary>Where a cue that would not play is said, for the app's log. Unset, nothing is.</summary>
    public static Action<string>? Trouble { get; set; }

    /// <summary>The mix moved: opens the line to the headset, if it is not open, so a beep at centre is heard at once.</summary>
    public static void Wake()
    {
        try
        {
            if (OperatingSystem.IsWindows()) lock (Gate) Ready();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trouble?.Invoke($"the line for the centre cue would not open: {ex.Message}");
        }
    }

    /// <summary>Plays the beep.</summary>
    /// <remarks>
    /// On Linux, returns once it has finished. A cue that will not play is not
    /// worth an error, so nothing is thrown; it is said to <see cref="Trouble"/>.
    /// </remarks>
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

    internal static float[] Beep(int rate)
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
        lock (Gate)
        {
            if (Ready() is CueSound sound) sound.Beep();
            else Trouble?.Invoke("the centre cue found no headset output to play to");
        }
    }

    /// <summary>Keeps the line open for another <see cref="KeepOpen"/>, opening it if need be.</summary>
    /// <returns>The sound on the line, or null when there is no headset to play to.</returns>
    [SupportedOSPlatform("windows")]
    private static CueSound? Ready()
    {
        _usedAt = Environment.TickCount64;
        Idle.Change(KeepOpen, Timeout.InfiniteTimeSpan);
        if (_sound is null) Open();
        return _sound;
    }

    [SupportedOSPlatform("windows")]
    private static void Open()
    {
        var devices = new MMDeviceEnumerator();
        var headset = Routing.Headset(devices, output: true);
        if (headset is null)
        {
            devices.Dispose();
            return;
        }

        var player = new WasapiPlayerBuilder()
            .WithDevice(headset).WithSharedMode().WithLatency(60)
            .Build();
        try
        {
            // Made in the device's own format, as the player does not convert:
            // a sound in any other plays as silence, with no error.
            var format = player.DeviceMixFormat;
            var sound = new CueSound(format.SampleRate, format.Channels);
            player.PlaybackStopped += (_, args) =>
            {
                if (args.Exception is not null)
                    Trouble?.Invoke($"the line for the centre cue stopped: {args.Exception.Message}");
            };
            player.Init(sound.ToWaveProvider());
            player.Play();
            _sound = sound;
            _line = new Line(player, headset, devices);
        }
        catch
        {
            player.Dispose();
            headset.Dispose();
            devices.Dispose();
            throw;
        }
    }

    private static void Close()
    {
        IDisposable? line;
        lock (Gate)
        {
            // A move since the timer fired keeps the line open.
            if (Environment.TickCount64 - _usedAt < KeepOpen.TotalMilliseconds) return;
            line = _line;
            _line = null;
            _sound = null;
        }
        line?.Dispose();
    }

    [SupportedOSPlatform("windows")]
    private sealed class Line(WasapiPlayer player, MMDevice headset, MMDeviceEnumerator devices) : IDisposable
    {
        public void Dispose()
        {
            player.Stop();
            player.Dispose();
            headset.Dispose();
            devices.Dispose();
        }
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
