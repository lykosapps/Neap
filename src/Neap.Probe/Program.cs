using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Neap.Core;
using Neap.Core.Audio;
using Neap.Core.Hid;
using Neap.Core.Presets;
using Neap.Core.Protocol;
using Neap.Core.Settings;
using Neap.Probe;

// A console harness, not a product: it exercises the library against the real
// hardware and prints what comes back, for checking values and naming new ones.
//
// Only one process can usefully hold the headset's channel at a time, so close
// Swarm II and anything else talking to the headset before using this.

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("""
        neap-probe — proving harness for the ported library

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
          micmute on|off     mute or unmute the headset's microphone in the system;
                             with neither, say whether it is muted
          route              where Windows sends sound and takes the mic from,
                             by role, and which headset device is carrying it
          hear <output> [seconds]
                             beep on one output every 3 s, and meanwhile
                             measure how loud each of the headset's mics is
          formats [mic]      what the endpoint accepts, and what it is on
          setformat B R      set the headset output format, and check it took
          switch B R [mic]   the app's way: hold a stream open, set, and check
          loopback <pid>     does process capture tap before or after session volume?
          mixapp <app> [demo|abandon]
                             the real SessionMix class - demo sweeps, abandon
                             exits mid-mix so recovery can be tested
          recover            put back whatever a previous run left turned down
          sound              Linux: every output and application stream, as the
                             mix sees them
        """);
    return 0;
}

Neap.Core.Mix.SessionMix.Trouble = line => Console.Error.WriteLine($"[mix] {line}");
SystemDevices.Trouble = line => Console.Error.WriteLine($"[hid] {line}");

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
                args.Length > 2 ? Convert.ToUInt16(args[2], 16) : HidControl.UsagePage);
        case "decode":
            return Neap.Probe.Capture.Decode(
                args[1], args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 1);
        case "diff": return Diff(args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 180);
        case "audio" when OperatingSystem.IsWindows(): return WindowsSound.Audio();
        case "micmute" when OperatingSystem.IsWindows(): return WindowsSound.MicMute(args.Length > 1 && args[1] == "on");
        case "route" when OperatingSystem.IsWindows(): return WindowsSound.Route();
        case "hear" when OperatingSystem.IsWindows(): return WindowsSound.Hear(args[1], args.Length > 2 ? double.Parse(args[2], CultureInfo.InvariantCulture) : 60);
        case "loopback" when OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041): return WindowsSound.Loopback(uint.Parse(args[1], CultureInfo.InvariantCulture));
        case "tone": return Tone(args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 5);
        case "mixapp": return MixApp(args[1], args.Length > 2 ? args[2] : "demo");
        case "recover": return RecoverMix();
        case "sound" when OperatingSystem.IsLinux(): return LinuxSound();
        case "micmute" when OperatingSystem.IsLinux(): return LinuxMicMute(args.Length > 1 ? args[1] : "");
        case "setformat" when OperatingSystem.IsWindows():
            return WindowsSound.SetFormat(int.Parse(args[1], CultureInfo.InvariantCulture),
                int.Parse(args[2], CultureInfo.InvariantCulture));
        case "switch" when OperatingSystem.IsWindows():
            return WindowsSound.Switch(int.Parse(args[1], CultureInfo.InvariantCulture),
                int.Parse(args[2], CultureInfo.InvariantCulture),
                args.Length > 3 && args[3] == "mic" ? Flow.Input : Flow.Output);
        case "formats" when OperatingSystem.IsWindows():
            return WindowsSound.Formats(
                args.Length > 1 && args[1].StartsWith("mic", StringComparison.Ordinal) ? Flow.Input : Flow.Output);
        case "audio" or "micmute" or "route" or "hear" or "loopback" or "setformat" or "switch" or "formats":
            Console.Error.WriteLine($"'{args[0]}' asks Windows about sound, so it runs only on Windows");
            return 2;
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
catch (Neap.Core.Audio.Pulse.PulseException error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

// Opens the device the headset is actually behind, not just the first
// collection Windows offers.
//
// With two transmitters plugged in only one has the headset on it; the other
// answers nothing, so reading from it reports empty values for a connected
// headset.
static HeadsetClient Ask()
{
    var client = HeadsetClient.Behind(allowWrites: false, out int present,
        failed: (device, ex) => Console.Error.WriteLine($"{device}: {ex.GetType().Name}: {ex.Message}"));
    if (client is not null) return client;
    throw new DeviceNotFoundException(
        present > 1
            ? $"{present} control collections are plugged in and none of them has the "
              + "headset — switch it on, or bring it closer"
            : "it is plugged in, but the headset is not answering — switch it on");
}

// Asks every device separately whether it answers for the headset.
//
// The other commands open the first device that answers, which is right for
// talking to the headset but cannot say which transmitter carries its
// controls. That matters when two are plugged in, because the headset's
// sound and controls can be on different ones. Each device is asked for the
// general-state block and its transmitter slots.
static int Who()
{
    var candidates = SystemDevices.Instance.Candidates();
    if (candidates.Count == 0) { Console.WriteLine("nothing plugged in"); return 1; }

    foreach (var device in candidates)
    {
        string product = device.ProductId.ToString("X4", CultureInfo.InvariantCulture);
        string name = Transmitters.Hardware.TryGetValue(product, out var called)
            ? called : $"0x{device.ProductId:x4}";
        try
        {
            using var client = new HeadsetClient(false, SystemDevices.Instance.Open(device), ownsTransport: true);
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
    var found = SystemDevices.List(HidControl.UsagePage);
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

// The chat/game crossfade as the app runs it: Core's SessionMix, driven from
// here so a sweep can be watched session by session. "abandon" exits mid-mix,
// as a crash would, so that "recover" can be tested against a real journal.
static int MixApp(string chatApp, string what)
{
    Neap.Core.Mix.SessionMix.Recover();      // clean up after any previous run
    using var playback = Neap.Core.Mix.Playback.ForThisSystem();
    using var headset = playback.Headset();
    if (headset is null) { Console.WriteLine("the headset is not the output in use"); return 1; }

    Neap.Core.Mix.SessionMix.Diagnostics = true;
    var mix = new Neap.Core.Mix.SessionMix([chatApp]);
    mix.Start();
    Console.WriteLine($"  {mix.Status}");

    foreach (int value in what == "abandon" ? new[] { 100 } : new[] { 50, 100, 0, 50 })
    {
        mix.SetMix(value);
        Thread.Sleep(600);
        // Session volumes, not a level meter: Windows' meter on a device held
        // across a sweep reports a constant value while the volumes change.
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
static string Levels(Neap.Core.Mix.IPlaybackDevice headset, string chatApp, bool wantChat)
{
    var levels = headset.Sessions()
        .Where(s => !string.IsNullOrEmpty(s.Id)
                    && s.Program.Contains(chatApp, StringComparison.OrdinalIgnoreCase) == wantChat)
        .Select(s => $"{s.Program} {s.Volume:0.00}")
        .ToList();
    return levels.Count == 0 ? "(none)" : string.Join(" ", levels);
}

// Whether the system has the headset's microphone muted; with "on" or "off",
// changes it first, the way the microphone tile does, then reads it back.
[SupportedOSPlatform("linux")]
static int LinuxMicMute(string change)
{
    var before = Neap.Core.Audio.Pulse.PulseVolumes.Describe(Flow.Input);
    Console.WriteLine($"{before.Name}: headset {before.MatchedHeadset}, volume {before.Percent}%, muted {before.Muted}");
    if (change is not ("on" or "off")) return 0;
    Neap.Core.Audio.Pulse.PulseVolumes.SetMuted(change == "on", Flow.Input);
    var after = Neap.Core.Audio.Pulse.PulseVolumes.Describe(Flow.Input);
    Console.WriteLine($"after:  volume {after.Percent}%, muted {after.Muted}");
    return after.Muted == (change == "on") ? 0 : 1;
}

// Plays the test tone on the headset, sweeping up from 200 Hz for the given
// seconds, the way the parametric equaliser's Sweep does.
static int Tone(double seconds)
{
    try
    {
        using var tone = ToneOutput.Open(200);
        tone.Stopped += fault => Console.WriteLine($"stopped: {fault.Message}");
        tone.Amplitude = 0.3;
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            tone.Frequency = 200 * Math.Pow(20, clock.Elapsed.TotalSeconds / seconds);
            Thread.Sleep(33);
        }
        Console.WriteLine("played");
        return 0;
    }
    catch (Exception ex) when (ToneOutput.IsRefusal(ex))
    {
        Console.WriteLine($"could not play: {ex.Message}");
        return 1;
    }
}

// The outputs and application streams the sound server reports, and which
// output the mix takes for the headset.
[SupportedOSPlatform("linux")]
static int LinuxSound()
{
    using var pulse = new Neap.Core.Audio.Pulse.PulseClient();
    string listening = pulse.DefaultSink();
    Console.WriteLine($"default output: {listening}");
    foreach (var sink in pulse.Sinks())
        Console.WriteLine($"  output {sink.Index,3}  {sink.Name}  \"{sink.Description}\"  usb {sink.Vendor}:{sink.Product}"
                        + (sink.IsHeadset ? "  headset" : ""));
    foreach (var stream in pulse.Streams())
        Console.WriteLine($"  stream {stream.Index,3}  on {stream.Sink,3}  {stream.Program}  pid {stream.ProcessId}  "
                        + $"volume {stream.Volume:0.00} x{stream.Channels}  {(stream.Playing ? "playing" : "paused")}");
    return 0;
}

static int RecoverMix()
{
    Neap.Core.Mix.SessionMix.Recover();
    using var playback = Neap.Core.Mix.Playback.ForThisSystem();
    using var headset = playback.Headset();
    if (headset is null) return 1;
    var sessions = headset.Sessions();
    int low = sessions.Count(s => s.Volume < 0.99f);
    Console.WriteLine($"recovered; {sessions.Count} sessions, {low} still below full");
    return low == 0 ? 0 : 1;
}

// Reads everything the headset will answer, repeatedly, and prints only what
// changes.
//
// For naming an unidentified value: start it, operate the hardware, and each
// change is printed with the time it happened. Nothing is filtered, because a
// value assumed understood can be the one that carries the flag.
//
// The transmitter slots are read as well as the settings categories, because
// dock and charge state may sit in either.
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

// Prints every report, pushed or asked for; see Neap.Probe.RawListener.
static int Raw(double seconds, ushort usagePage) =>
    Neap.Probe.RawListener.Run(seconds, usagePage);

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
