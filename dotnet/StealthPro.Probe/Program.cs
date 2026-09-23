using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using StealthPro.Core;
using StealthPro.Core.Audio;
using StealthPro.Core.Hid;
using StealthPro.Core.Presets;
using StealthPro.Core.Protocol;
using StealthPro.Core.Settings;

// A console harness, not a product: it exercises the library against the real
// hardware and prints what comes back, for checking values and naming new ones.
//
// Only one process can usefully hold the headset's channel at a time, so close
// Swarm II and anything else talking to the headset before using this.

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("""
        stealthprobe — proving harness for the ported library

          devices            list matching HID collections
          who                ask every transmitter plugged in, separately,
                             whether it answers for the headset
          read [category]    read every setting, or one category
          named              read everything, with the registry's names
          registry           print the settings registry (no device needed)
          presets [mic]      list the presets in a bank, as the app would
          slots              read the ten custom preset slots, raw
          transmitters       read the four transmitter slots
          json               read everything and print JSON, for diffing
          watch [seconds]    print notifications as they arrive
          raw [seconds] [usage page]
                             every report as it arrives, decoded or not.
                             Usage page defaults to ff13, where the settings
                             live; 000c is the collection the wheels use
          decode <file.pcap> [address]
                             read a USBPcap capture of Swarm II driving the
                             headset; writes <name>-decoded.txt beside it
          audio              Windows volume, mute and format, both devices
          route              where Windows sends sound and takes the mic from,
                             by role, and which headset device is carrying it
          hear <output> [seconds]
                             beep on one output every 3 s, and meanwhile
                             measure how loud each of the headset's mics is
          formats [mic]      what the endpoint accepts, and what it is on
          setformat B R      set the headset output format, and check it took
          loopback <pid>     does process capture tap before or after session volume?
          mixapp <app> [demo|abandon]
                             the real SessionMix class - demo sweeps, abandon
                             exits mid-mix so recovery can be tested
          recover            put back whatever a previous run left turned down
        """);
    return 0;
}

try
{
    switch (args[0])
    {
        case "devices": return Devices();
        case "who": return Who();
        case "read": return Read(args.Length > 1 ? args[1] : null);
        case "named": return Named();
        case "registry": return PrintRegistry();
        case "presets":
            return ShowPresets(
                args.Length > 1 && args[1].StartsWith("mic", StringComparison.Ordinal) ? Bank.Mic : Bank.Game);
        case "slots": return Group(Verbs.PresetSlotCategories);
        case "transmitters": return Group(Verbs.TransmitterCategories);
        case "json": return Json();
        case "watch": return Watch(args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 10);
        case "raw":
            return Raw(
                args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 20,
                args.Length > 2 ? Convert.ToUInt16(args[2], 16) : HidTransport.VendorUsagePage);
        case "decode":
            return StealthPro.Probe.Capture.Decode(
                args[1], args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 1);
        case "diff": return Diff(args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 180);
        case "audio": return Audio();
        case "route": return Route();
        case "hear": return Hear(args[1], args.Length > 2 ? double.Parse(args[2], CultureInfo.InvariantCulture) : 60);
        case "loopback": return Loopback(uint.Parse(args[1], CultureInfo.InvariantCulture));
        case "mixapp": return MixApp(args[1], args.Length > 2 ? args[2] : "demo");
        case "recover": return RecoverMix();
        case "setformat":
            return SetFormat(int.Parse(args[1], CultureInfo.InvariantCulture),
                int.Parse(args[2], CultureInfo.InvariantCulture));
        case "formats":
            return Formats(
                args.Length > 1 && args[1].StartsWith("mic", StringComparison.Ordinal) ? Flow.Input : Flow.Output);
        default:
            Console.Error.WriteLine($"unknown command '{args[0]}'");
            return 2;
    }
}
catch (DeviceNotFoundException error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
catch (TransportException error)
{
    Console.Error.WriteLine($"{error.Message} — is Swarm II or Neap running?");
    return 1;
}

/// <summary>
/// Opens the device the headset is actually behind, not just the first
/// collection Windows offers.
/// </summary>
/// <remarks>
/// With two transmitters plugged in only one has the headset on it; the other
/// answers nothing, so reading from it reports empty values for a connected
/// headset.
/// </remarks>
static HeadsetClient Ask()
{
    var client = HeadsetClient.Behind(allowWrites: false, out int present);
    if (client is not null) return client;
    throw new DeviceNotFoundException(
        present > 1
            ? $"{present} control collections are plugged in and none of them has the "
              + "headset — switch it on, or bring it closer"
            : "it is plugged in, but the headset is not answering — switch it on");
}

/// <summary>
/// Asks every device separately whether it answers for the headset.
/// </summary>
/// <remarks>
/// The other commands open the first device that answers, which is right for
/// talking to the headset but cannot say which transmitter carries its
/// controls. That matters when two are plugged in, because the headset's
/// sound and controls can be on different ones. Each device is asked for the
/// general-state block and its transmitter slots.
/// </remarks>
static int Who()
{
    var candidates = HidTransport.Candidates();
    if (candidates.Count == 0) { Console.WriteLine("nothing plugged in"); return 1; }

    foreach (var device in candidates)
    {
        string product = device.ProductId.ToString("X4", CultureInfo.InvariantCulture);
        string name = Transmitters.Hardware.TryGetValue(product, out var called)
            ? called : $"0x{device.ProductId:x4}";
        try
        {
            using var client = new HeadsetClient(false, new HidTransport(device.Path), ownsTransport: true);
            client.Drain();
            var state = client.ReadCategory("GSI", TimeSpan.FromMilliseconds(1500));
            if (state.Count == 0)
            {
                Console.WriteLine($"{name,-16} answers nothing");
                continue;
            }
            string battery = state.TryGetValue("240", out var b) ? DeviceEvent.Render(b) + "%" : "?";
            var slots = Transmitters.ReadAll(client, TimeSpan.FromMilliseconds(700));
            string selected = slots.FirstOrDefault(t => t.Active)?.Kind ?? "none reported";
            Console.WriteLine($"{name,-16} answers: {state.Count} values, battery {battery}, "
                            + $"headset has selected: {selected}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{name,-16} could not be asked ({ex.Message})");
        }
    }
    return 0;
}

static int Devices()
{
    var found = HidTransport.ListDevices();
    if (found.Count == 0) { Console.WriteLine("none found"); return 1; }
    foreach (var d in found)
        Console.WriteLine($"0x{d.VendorId:x4}:0x{d.ProductId:x4}  usage page 0x{d.UsagePage:x4}  "
                        + $"in {d.InputLength}  out {d.OutputLength}\n  {d.Path}");
    return 0;
}

static int Read(string? category)
{
    using var client = Ask();
    Console.WriteLine($"device: {client.Device}");
    var clock = Stopwatch.StartNew();
    var values = category is null
        ? client.ReadAll()
        : client.ReadCategory(category, TimeSpan.FromMilliseconds(1200));
    clock.Stop();

    foreach (var pair in values.OrderBy(p => Convert.ToInt32(p.Key, 16)))
        Console.WriteLine($"  0x{pair.Key,-5} {DeviceEvent.Render(pair.Value)}");
    Console.WriteLine($"{values.Count} values in {clock.ElapsedMilliseconds}ms");
    return values.Count == 0 ? 1 : 0;
}

static int Named()
{
    using var client = Ask();
    var values = client.ReadAll();
    // The lighting brightnesses live inside the active transmitter's block,
    // so they arrive with the inventory rather than with the settings.
    foreach (var pair in Transmitters.Lighting(Transmitters.ReadAll(client)))
        // Serialised rather than quoted by hand: the value comes off the
        // device, and a quote or backslash in it would make unparseable JSON.
        values[pair.Key] = JsonSerializer.SerializeToElement(pair.Value);

    int named = 0;
    foreach (var pair in values.OrderBy(p => Convert.ToInt32(p.Key, 16)))
    {
        string label = "not yet identified";
        try { label = Registry.Resolve(pair.Key).Name; named++; }
        catch (KeyNotFoundException) { }
        Console.WriteLine($"  0x{pair.Key,-5} {label,-26} {DeviceEvent.Render(pair.Value)}");
    }
    Console.WriteLine($"{values.Count} values, {named} named, "
                    + $"{values.Count - named} not yet identified");
    return 0;
}

static int PrintRegistry()
{
    foreach (var key in Registry.All.OrderBy(k => k.Key))
        Console.WriteLine($"  {key.Hex,-7} {key.Name,-26} {key.Category,-4} "
                        + $"{key.Kind,-6} {(key.Writable ? "" : "read-only")}");
    Console.WriteLine($"{Registry.All.Count} settings");
    return 0;
}

static int ShowPresets(Bank bank)
{
    using var client = Ask();
    var spec = PresetStore.Spec(bank);
    var presets = PresetStore.ReadBank(client, bank);
    var current = PresetStore.ReadCurrent(client, bank);

    Console.WriteLine($"{spec.Label} equaliser  ({string.Join("  ", spec.Frequencies)})");
    foreach (var preset in presets)
    {
        string mark = current is not null && current.Id == preset.Id ? "*" : " ";
        Console.WriteLine($" {mark} {preset.Id,3}  {preset.Name,-22} "
                        + $"{(preset.Custom ? "custom  " : "built in")} "
                        + $"[{string.Join(" ", preset.Bands.Select(b => $"{b / 10.0,5:+0.0;-0.0;0.0}"))}]");
    }
    Console.WriteLine($"free slots: {string.Join(", ", PresetStore.FreeSlots(presets))}");
    var counted = PresetStore.Count(client, bank);
    Console.WriteLine($"headset says it holds {counted} custom; we found "
                    + $"{presets.Count(p => p.Custom)}");
    return 0;
}

static int Group(IReadOnlyList<string> categories)
{
    using var client = Ask();
    foreach (var category in categories)
    {
        var values = client.ReadCategory(category, TimeSpan.FromMilliseconds(1200));
        if (values.Count == 0) { Console.WriteLine($"{category}: (empty)"); continue; }
        foreach (var pair in values)
            Console.WriteLine($"{category}: 0x{pair.Key} {DeviceEvent.Render(pair.Value)}");
    }
    return 0;
}

static int Json()
{
    using var client = Ask();
    var values = client.ReadAll();
    var ordered = values.OrderBy(p => Convert.ToInt32(p.Key, 16))
                        .ToDictionary(p => p.Key, p => DeviceEvent.Render(p.Value));
    Console.WriteLine(JsonSerializer.Serialize(ordered, Indented));
    return 0;
}

static int Audio()
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

// Where the sound is going. Windows keeps a default for each role, and the
// headset can offer several outputs of its own (one per transmitter, and one
// for the headset itself over the USB-C cable) that differ by little more than
// a word in the name. Each is named by the device behind it and its level
// sampled, because a default says where sound is sent, not where it is heard.
static int Route()
{
    using var devices = new MMDeviceEnumerator();
    foreach (var flow in new[] { DataFlow.Render, DataFlow.Capture })
        foreach (var role in new[] { Role.Multimedia, Role.Console, Role.Communications })
        {
            string said;
            try
            {
                using var device = devices.GetDefaultAudioEndpoint(flow, role);
                string owner = Owner(device);
                said = $"{device.FriendlyName}  [{(owner.Length > 0 ? owner : "not the headset")}]";
            }
            catch { said = "(none)"; }
            Console.WriteLine($"{(flow == DataFlow.Render ? "output" : "input"),-7} {role,-15} {said}");
        }

    Console.WriteLine();
    foreach (var flow in new[] { DataFlow.Render, DataFlow.Capture })
        foreach (var device in devices.EnumerateAudioEndPoints(flow, DeviceState.Active))
            using (device)
            {
                string owner = Owner(device);
                if (owner.Length == 0) continue;

                float peak = 0;
                for (int i = 0; i < 20; i++)
                {
                    peak = Math.Max(peak, device.AudioMeterInformation.MasterPeakValue);
                    Thread.Sleep(50);
                }

                var playing = new List<string>();
                if (flow == DataFlow.Render)
                {
                    device.AudioSessionManager.RefreshSessions();
                    var sessions = device.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        // Open but silent sessions too, marked apart: a chat
                        // app between calls is where its sound will go.
                        var state = sessions[i].State;
                        if (state == AudioSessionState.AudioSessionStateExpired) continue;
                        uint pid = sessions[i].GetProcessID;
                        string who;
                        try { who = pid == 0 ? "system" : Process.GetProcessById((int)pid).ProcessName; }
                        catch { who = $"pid {pid}"; }
                        playing.Add(state == AudioSessionState.AudioSessionStateActive ? who + "*" : who);
                    }
                }
                Console.WriteLine(
                    $"{(flow == DataFlow.Render ? "output" : "input"),-7} {device.FriendlyName,-45} "
                    + $"[{owner}]  level {device.AudioEndpointVolume.MasterVolumeLevelScalar * 100:0}%"
                    + $"  peak {peak:0.000}"
                    + (playing.Count > 0 ? $"  sessions (* playing): {string.Join(", ", playing.Distinct())}" : ""));
            }
    return 0;
}

// Whether an output reaches the headset, and whether each mic hears the person.
// A default says where Windows sends sound and takes the mic from, not whether
// anything arrives. With several headset devices at once, whether it plays one
// while another is playing, or sends the voice down each, is only known by
// trying. This plays a soft beep on one output every three seconds, for a
// person to listen for, and prints the loudest moment on each headset mic,
// second by second. Only the level is kept, never the audio.
static int Hear(string match, double seconds)
{
    using var devices = new MMDeviceEnumerator();
    // "-" listens without beeping.
    using var output = match == "-" ? null : devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
        .FirstOrDefault(d => d.FriendlyName.Contains(match, StringComparison.OrdinalIgnoreCase));
    if (output is null && match != "-") { Console.Error.WriteLine($"no output matching '{match}'"); return 1; }

    // A mic's meter reads nothing unless something is recording from it, so
    // each is opened and its loudest sample kept per second.
    var mics = devices.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
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

// Which of the headset's devices an endpoint belongs to, from the product id
// in the path of the kernel filter behind it, as Routing does in Core.
static string Owner(MMDevice device)
{
    var filterPath = new PropertyKey(new Guid("233164c8-1b2c-4c7d-bc68-b671687a2567"), 1);
    try
    {
        if (!device.Properties.Contains(filterPath)) return "";
        string path = device.Properties[filterPath].Value?.ToString() ?? "";
        if (!path.Contains("vid_10f5", StringComparison.OrdinalIgnoreCase)) return "";
        int at = path.IndexOf("pid_", StringComparison.OrdinalIgnoreCase);
        if (at < 0 || path.Length < at + 8) return "";
        string product = path.Substring(at + 4, 4).ToUpperInvariant();
        return Transmitters.Hardware.TryGetValue(product, out var name)
            ? $"{name} {product}" : product;
    }
    catch { return ""; }
}

static int Formats(Flow flow)
{
    var report = DeviceFormat.Describe(AudioEndpoints.DefaultMatch, flow);
    Console.WriteLine($"{report.Device}");
    Console.WriteLine($"  current: {report.Current?.Label ?? "not reported"}");
    foreach (var option in report.Options)
        Console.WriteLine($"  offers : {option.Label}");
    return 0;
}

// The chat/game crossfade as the app runs it: Core's SessionMix, driven from
// here so a sweep can be watched session by session. "abandon" exits mid-mix,
// as a crash would, so that "recover" can be tested against a real journal.
static int MixApp(string chatApp, string what)
{
    StealthPro.Core.Mix.SessionMix.Recover();      // clean up after any previous run
    using var devices = new MMDeviceEnumerator();
    using var headset = devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
        .FirstOrDefault(d => d.FriendlyName.Contains("Stealth Pro", StringComparison.OrdinalIgnoreCase));
    if (headset is null) { Console.WriteLine("headset not found"); return 1; }

    StealthPro.Core.Mix.SessionMix.Diagnostics = true;
    var mix = new StealthPro.Core.Mix.SessionMix(chatApp);
    mix.Start();
    Console.WriteLine($"  {mix.Status}");

    foreach (int value in what == "abandon" ? new[] { 100 } : new[] { 50, 100, 0, 50 })
    {
        mix.SetMix(value);
        Thread.Sleep(600);
        // Session volumes, not the endpoint meter: the meter on an MMDevice
        // held across a sweep reports a constant value while the volumes
        // change.
        Console.WriteLine($"  mix {value,3}  chat {Levels(headset, chatApp, true)}"
                        + $"   game {Levels(headset, chatApp, false)}"
                        + $"   held {mix.Status.Held}");
    }

    if (what == "abandon")
    {
        // Exit without stopping, as a crash would. The journal is then the
        // only record of the original volumes.
        Console.WriteLine("  leaving without restoring - run 'recover' next");
        Environment.Exit(0);
    }

    mix.Stop();
    Console.WriteLine($"  stopped: {mix.Status}");
    return 0;
}

// What the sessions on a side are actually set to.
static string Levels(MMDevice headset, string chatApp, bool wantChat)
{
    headset.AudioSessionManager.RefreshSessions();
    var sessions = headset.AudioSessionManager.Sessions;
    var levels = new List<string>();
    for (int i = 0; i < sessions.Count; i++)
    {
        var session = sessions[i];
        if (session.State == AudioSessionState.AudioSessionStateExpired) continue;
        if (string.IsNullOrEmpty(session.GetSessionIdentifier)) continue;
        bool isChat = ProcessName(session.GetProcessID)
            .Contains(chatApp, StringComparison.OrdinalIgnoreCase);
        if (isChat == wantChat) levels.Add($"{session.SimpleAudioVolume.Volume:0.00}");
    }
    return levels.Count == 0 ? "(none)" : string.Join(" ", levels);
}

static int RecoverMix()
{
    StealthPro.Core.Mix.SessionMix.Recover();
    using var devices = new MMDeviceEnumerator();
    using var headset = devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
        .FirstOrDefault(d => d.FriendlyName.Contains("Stealth Pro", StringComparison.OrdinalIgnoreCase));
    if (headset is null) return 1;
    headset.AudioSessionManager.RefreshSessions();
    var sessions = headset.AudioSessionManager.Sessions;
    int low = 0;
    for (int i = 0; i < sessions.Count; i++)
        if (sessions[i].SimpleAudioVolume.Volume < 0.99f) low++;
    Console.WriteLine($"recovered; {sessions.Count} sessions, {low} still below full");
    return low == 0 ? 0 : 1;
}

static string ProcessName(uint pid)
{
    try { return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; }
    catch { return pid == 0 ? "system" : "?"; }
}

// Whether process loopback taps a stream before or after session volume.
//
// Capturing a chat app at the process level and rendering our own copy to the
// headset means hearing it twice unless the app's own session is silenced.
// That only works if loopback taps the stream before session volume is
// applied; if it taps after, silencing the app silences the capture too.
// Measured: it taps after, which is why the mix uses session volume alone.
static int Loopback(uint pid)
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

static float CapturePeak(uint pid, string label)
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

static AudioSessionControl? FindSession(uint pid)
{
    using var devices = new MMDeviceEnumerator();
    foreach (var device in devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
    {
        device.AudioSessionManager.RefreshSessions();
        var sessions = device.AudioSessionManager.Sessions;
        for (int i = 0; i < sessions.Count; i++)
            if (sessions[i].GetProcessID == pid) return sessions[i];
    }
    return null;
}

static int SetFormat(int bits, int rate)
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

/// <summary>
/// Reads everything the headset will answer, repeatedly, and prints only what
/// changes.
/// </summary>
/// <remarks>
/// <para>
/// For naming an unidentified value: start it, operate the hardware, and each
/// change is printed with the time it happened. Nothing is filtered, because a
/// value assumed understood can be the one that carries the flag.
/// </para>
/// <para>
/// The transmitter slots are read as well as the settings categories, because
/// dock and charge state may sit in either.
/// </para>
/// </remarks>
static int Diff(double seconds)
{
    using var client = Ask();
    Console.WriteLine($"device: {client.Device}");
    Console.WriteLine($"watching every value for {seconds:0}s.");
    Console.WriteLine("operate the hardware — anything that changes prints here.\n");

    var seen = new Dictionary<string, string>();
    var clock = Stopwatch.StartNew();
    bool first = true;
    bool quiet = false;
    int cycles = 0;

    while (clock.Elapsed.TotalSeconds < seconds)
    {
        var now = new Dictionary<string, string>();
        try
        {
            foreach (var pair in client.ReadAll())
                now[pair.Key] = pair.Value.ToString();
            foreach (var category in Verbs.TransmitterCategories)
                foreach (var pair in client.ReadCategory(category, TimeSpan.FromMilliseconds(500)))
                    now[$"{category}.{pair.Key}"] = pair.Value.ToString();
        }
        catch (Exception ex)
        {
            // Docking, undocking or walking out of range can drop the link
            // mid-pass. Report it and keep going: the interesting change is
            // often on the way back.
            Console.WriteLine($"  [{clock.Elapsed:mm\\:ss}] link trouble: {ex.Message}");
            Thread.Sleep(1000);
            continue;
        }

        // A pass where nothing answered means the headset is gone, not that
        // every value changed. Say so once and keep the baseline for when it
        // comes back.
        if (now.Count == 0)
        {
            if (!quiet) Console.WriteLine($"  [{clock.Elapsed:mm\\:ss}] no answer");
            quiet = true;
            Thread.Sleep(500);
            continue;
        }
        if (quiet)
        {
            Console.WriteLine($"  [{clock.Elapsed:mm\\:ss}] answering again");
            quiet = false;
        }

        cycles++;
        if (first)
        {
            Console.WriteLine($"baseline: {now.Count} values\n");
            first = false;
        }
        else
        {
            foreach (var pair in now)
            {
                if (!seen.TryGetValue(pair.Key, out var was))
                    Console.WriteLine($"  [{clock.Elapsed:mm\\:ss}] {pair.Key,-12} appeared  = {Short(pair.Value)}");
                else if (was != pair.Value)
                    Console.WriteLine(
                        $"  [{clock.Elapsed:mm\\:ss}] {pair.Key,-12} {Short(was)} -> {Short(pair.Value)}");
            }
            foreach (var key in seen.Keys.Where(k => !now.ContainsKey(k)).ToList())
                Console.WriteLine($"  [{clock.Elapsed:mm\\:ss}] {key,-12} stopped answering (was {Short(seen[key])})");
        }

        seen.Clear();
        foreach (var pair in now) seen[pair.Key] = pair.Value;
        Console.Out.Flush();
        Thread.Sleep(150);
    }

    Console.WriteLine($"\ndone — {cycles} passes, {seen.Count} values on the last one");
    return 0;
}

static string Short(string value) =>
    value.Length <= 60 ? value : value[..57] + "...";

/// <summary>
/// Prints every report, pushed or asked for; see <see cref="StealthPro.Probe.RawListener"/>.
/// </summary>
static int Raw(double seconds, ushort usagePage) =>
    StealthPro.Probe.RawListener.Run(seconds, usagePage);

static int Watch(double seconds)
{
    using var client = Ask();
    Console.WriteLine($"watching {client.Device} for {seconds}s — move a wheel or press a button");
    var clock = Stopwatch.StartNew();
    while (clock.Elapsed.TotalSeconds < seconds)
    {
        foreach (var evt in client.ReadOnce()) Console.WriteLine($"  {evt}");
        Thread.Sleep(5);
    }
    if (EventParser.Unrecognised.Count > 0)
    {
        Console.WriteLine("\nevent-shaped but not decoded:");
        foreach (var fragment in EventParser.Unrecognised) Console.WriteLine($"  {fragment}");
    }
    return 0;
}

partial class Program
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
}
