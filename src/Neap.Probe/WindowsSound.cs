using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Neap.Core;
using Neap.Core.Audio;

namespace Neap.Probe;

/// <summary>The commands that ask Windows about sound: volumes, formats, routes and what the mics hear.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsSound
{
    internal static int MicMute(bool muted)
    {
        AudioEndpoints.SetMuted(muted, flow: Flow.Input);
        Console.WriteLine($"microphone {(AudioEndpoints.GetMuted(flow: Flow.Input) ? "muted" : "not muted")} in Windows");
        return 0;
    }

    internal static int Audio()
    {
        var output = AudioEndpoints.Describe();
        var input = AudioEndpoints.Describe(flow: Flow.Input);
        Console.WriteLine($"output : {output.Name}");
        Console.WriteLine($"         {output.Percent}%  {(output.Muted ? "muted" : "not muted")}"
                        + $"  {DeviceFormat.Current(AudioEndpoints.DefaultMatch)?.Label}");
        Console.WriteLine($"input  : {input.Name}");
        Console.WriteLine($"         {input.Percent}%  {(input.Muted ? "muted" : "not muted")}"
                        + $"  {DeviceFormat.Current(AudioEndpoints.DefaultMatch, Flow.Input)?.Label}");

        // Stored against live shows whether a format change took effect. When
        // they disagree, the stored setting is not what the device is running.
        var stored = DeviceFormat.Current(AudioEndpoints.DefaultMatch);
        var live = DeviceFormat.MixFormat(AudioEndpoints.DefaultMatch);
        Console.WriteLine($"stored {stored?.Rate}Hz vs engine {live?.Rate}Hz  "
                        + $"{(stored?.Rate == live?.Rate ? "agree" : "DISAGREE")}");
        return 0;
    }

    // Where the sound is going, as the app's recordings report it: Windows'
    // device for each role, and the volume, loudest moment and programs on each
    // of the headset's devices over a second.
    internal static int Route()
    {
        var survey = Routing.Survey(TimeSpan.FromSeconds(1));
        foreach (var role in survey.Defaults)
            Console.WriteLine($"{(role.Output ? "output" : "input"),-7} {role.Role,-15} "
                            + (role.Device.Length == 0 ? "(none)"
                                : $"{role.Device}  [{(role.Product.Length > 0 ? Called(role.Product) : "not the headset")}]"));

        Console.WriteLine();
        foreach (var device in survey.Devices)
            Console.WriteLine(
                $"{(device.Output ? "output" : "input"),-7} {device.Name,-45} [{Called(device.Product)}]  "
                + $"level {device.Percent}%  peak {device.Peak:0.000}"
                + (device.Apps.Count > 0
                    ? $"  sessions (* playing): {string.Join(", ", device.Apps.Select(a => a.Playing ? a.Name + "*" : a.Name).Distinct())}"
                    : ""));
        return 0;
    }

    // Whether an output reaches the headset, and whether each mic hears the person.
    // A default says where Windows sends sound and takes the mic from, not whether
    // anything arrives. With several headset devices at once, whether it plays one
    // while another is playing, or sends the voice down each, is only known by
    // trying. This plays a soft beep on one output every three seconds, for a
    // person to listen for, and prints the loudest moment on each headset mic,
    // second by second. Only the level is kept, never the audio.
    internal static int Hear(string match, double seconds)
    {
        using var devices = new MMDeviceEnumerator();
        // "-" listens without beeping.
        using var output = match == "-" ? null : devices.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, DeviceState.Active)
            .FirstOrDefault(d => HeadsetModels.SoundMatches(d.FriendlyName, match));
        if (output is null && match != "-") { Console.Error.WriteLine($"no output matching '{match}'"); return 1; }

        // A mic's meter reads nothing unless something is recording from it, so
        // each is opened and its loudest sample kept per second.
        var mics = devices.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Capture, DeviceState.Active)
            .Where(d => Owner(d).Length > 0).ToList();
        var loudest = new float[mics.Count];
        // Bytes arrived, counted separately from level: a mic that delivers
        // silence and one that delivers nothing both read as zero level.
        var arrived = new long[mics.Count];
        var captures = new List<WasapiRecorder>();
        for (int m = 0; m < mics.Count; m++)
        {
            int index = m;
            try
            {
                var capture = new WasapiRecorderBuilder().WithDevice(mics[m]).Build();
                bool floats = capture.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat
                              || capture.WaveFormat.BitsPerSample == 32;
                capture.DataAvailable += (buffer, _, _, _) =>
                {
                    float peak = 0;
                    if (floats)
                        for (int i = 0; i + 3 < buffer.Length; i += 4)
                            peak = Math.Max(peak, Math.Abs(BitConverter.ToSingle(buffer[i..])));
                    else
                        for (int i = 0; i + 1 < buffer.Length; i += 2)
                            peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(buffer[i..]) / 32768f));
                    lock (loudest)
                    {
                        loudest[index] = Math.Max(loudest[index], peak);
                        arrived[index] += buffer.Length;
                    }
                };
                capture.StartRecording();
                captures.Add(capture);
            }
            catch (Exception ex) { Console.WriteLine($"could not open {mics[m].FriendlyName}: {ex.Message}"); }
        }

        Console.WriteLine(output is null
            ? $"listening for {seconds:0} s"
            : $"beeping on {output.FriendlyName} [{Owner(output)}] every 3 s for {seconds:0} s");
        Console.WriteLine("mics: " + string.Join("  |  ", mics.Select(m => $"{m.FriendlyName} [{Owner(m)}]")));

        // The beep: 880 Hz for a third of a second, faded in and out, made at
        // the device's own rate so nothing has to convert it.
        static byte[] Beep(int rate)
        {
            int total = rate / 3, fade = rate / 80;
            var pcm = new byte[total * 2];
            for (int i = 0; i < total; i++)
            {
                double amplitude = 0.25;
                if (i < fade) amplitude *= (double)i / fade;
                else if (i > total - fade) amplitude *= (double)(total - i) / fade;
                short sample = (short)(amplitude * short.MaxValue * Math.Sin(2 * Math.PI * 880 * i / rate));
                pcm[i * 2] = (byte)(sample & 0xFF);
                pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return pcm;
        }

        var started = DateTime.Now;
        long lastBeep = -3;
        while ((DateTime.Now - started).TotalSeconds < seconds)
        {
            long second = (long)(DateTime.Now - started).TotalSeconds;
            string beep = "";
            if (output is not null && second - lastBeep >= 3)
            {
                lastBeep = second;
                beep = "  beep";
                try
                {
                    var player = new WasapiPlayerBuilder()
                        .WithDevice(output).WithSharedMode().WithPollingSync().WithLatency(60)
                        .Build();
                    int rate = player.DeviceMixFormat.SampleRate;
                    var source = new RawSourceWaveStream(
                        new MemoryStream(Beep(rate)), new WaveFormat(rate, 16, 1));
                    player.PlaybackStopped += (_, _) => { player.Dispose(); source.Dispose(); };
                    player.Init(source);
                    player.Play();
                }
                catch (Exception ex) { beep = $"  beep failed: {ex.Message}"; }
            }
            Thread.Sleep(1000);
            string levels;
            lock (loudest)
            {
                levels = string.Join("  ", loudest.Select((l, i) => $"{l:0.000} ({arrived[i] / 1024} KB)"));
                Array.Clear(loudest);
                Array.Clear(arrived);
            }
            Console.WriteLine($"{DateTime.Now:HH:mm:ss}  mics {levels}{beep}");
        }

        foreach (var capture in captures)
        {
            try { capture.StopRecording(); capture.Dispose(); } catch { }
        }
        foreach (var mic in mics) mic.Dispose();
        return 0;
    }

    // Which of the headset's devices an endpoint belongs to, empty for anything else.
    private static string Owner(MMDevice device) => Routing.ProductOf(device) is { Length: > 0 } product ? Called(product) : "";

    private static string Called(string product) =>
        Transmitters.Hardware.TryGetValue(product, out var name) ? $"{name} {product}" : product;

    internal static int Formats(Flow flow)
    {
        var report = DeviceFormat.Describe(AudioEndpoints.DefaultMatch, flow);
        Console.WriteLine($"{report.Device}");
        Console.WriteLine($"  current: {report.Current?.Label ?? "not reported"}");
        foreach (var option in report.Options)
            Console.WriteLine($"  offers : {option.Label}");
        return 0;
    }

    // Whether process loopback taps a stream before or after session volume.
    //
    // Capturing a chat app at the process level and rendering our own copy to the
    // headset means hearing it twice unless the app's own session is silenced.
    // That only works if loopback taps the stream before session volume is
    // applied; if it taps after, silencing the app silences the capture too.
    // Measured: it taps after, which is why the mix uses session volume alone.
    [SupportedOSPlatform("windows10.0.19041")]
    internal static int Loopback(uint pid)
    {
        Console.WriteLine($"pid {pid}: capturing at full volume, then with its session muted");

        float before = CapturePeak(pid, "session at its own level");
        if (before <= 0.0001f)
        {
            Console.WriteLine("  nothing captured - is that process actually playing audio?");
            return 1;
        }

        var session = FindSession(pid);
        if (session is null) { Console.WriteLine("  no session for that pid"); return 1; }

        float original = session.SimpleAudioVolume.Volume;
        float after;
        try
        {
            session.SimpleAudioVolume.Volume = 0f;
            Thread.Sleep(400);
            after = CapturePeak(pid, "session volume 0");
        }
        finally
        {
            session.SimpleAudioVolume.Volume = original;
        }

        Console.WriteLine();
        bool preVolume = after > before * 0.5f;
        Console.WriteLine(preVolume
            ? "PRE-VOLUME: capture survives the session being silenced. The cable can go."
            : "POST-VOLUME: silencing the app silences our capture too. The cable stays.");
        return preVolume ? 0 : 2;
    }

    [SupportedOSPlatform("windows10.0.19041")]
    private static float CapturePeak(uint pid, string label)
    {
        float peak = 0f;
        var builder = new WasapiRecorderBuilder()
            .WithProcessLoopback(pid, ProcessLoopbackMode.IncludeTargetProcessTree)
            .WithFormat(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));

        using var recorder = builder.BuildAsync().GetAwaiter().GetResult();
        // Zero-copy: the span is only valid inside the callback, so the peak is
        // taken here rather than the buffer being kept.
        recorder.DataAvailable += (buffer, flags, devicePosition, qpcPosition) =>
        {
            var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(buffer);
            foreach (float sample in samples)
            {
                float magnitude = Math.Abs(sample);
                if (magnitude > peak) peak = magnitude;
            }
        };
        recorder.StartRecording();
        Thread.Sleep(2500);
        recorder.StopRecording();
        Thread.Sleep(200);
        Console.WriteLine($"  {label,-28} peak {peak:0.0000}");
        return peak;
    }

    private static AudioSessionControl? FindSession(uint pid)
    {
        using var devices = new MMDeviceEnumerator();
        foreach (var device in devices.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, DeviceState.Active))
        {
            device.AudioSessionManager.RefreshSessions();
            var sessions = device.AudioSessionManager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
                if (sessions[i].GetProcessID == pid) return sessions[i];
        }
        return null;
    }

    internal static int SetFormat(int bits, int rate)
    {
        var before = DeviceFormat.MixFormat(AudioEndpoints.DefaultMatch);
        DeviceFormat.Apply(AudioEndpoints.DefaultMatch, bits, rate);
        Thread.Sleep(1500);
        var stored = DeviceFormat.Current(AudioEndpoints.DefaultMatch);
        var live = DeviceFormat.MixFormat(AudioEndpoints.DefaultMatch);
        Console.WriteLine($"engine was {before?.Rate}Hz");
        Console.WriteLine($"stored {stored?.Bits}-bit/{stored?.Rate}Hz  engine {live?.Rate}Hz  "
                        + $"{(stored?.Rate == live?.Rate ? "AGREE" : "DISAGREE - the setting is a decoration")}");
        return stored?.Rate == live?.Rate ? 0 : 1;
    }

    internal static int Switch(int bits, int rate, Flow flow)
    {
        var before = DeviceFormat.MixFormat(AudioEndpoints.DefaultMatch, flow);
        try { DeviceFormat.Switch(AudioEndpoints.DefaultMatch, bits, rate, flow); }
        catch (Neap.Core.Audio.FormatException ex) { Console.WriteLine($"refused: {ex.Message}"); return 1; }
        var live = DeviceFormat.MixFormat(AudioEndpoints.DefaultMatch, flow);
        Console.WriteLine($"engine was {before?.Rate}Hz, now {live?.Rate}Hz");
        return 0;
    }
}
