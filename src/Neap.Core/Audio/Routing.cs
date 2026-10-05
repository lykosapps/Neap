using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NFlow = NAudio.CoreAudioApi.DataFlow;
using NKey = NAudio.CoreAudioApi.PropertyKey;
using NRole = NAudio.CoreAudioApi.Role;
using NState = NAudio.CoreAudioApi.DeviceState;

namespace Neap.Core.Audio;

/// <summary>Where Windows is sending sound, and which transmitter owns it.</summary>
public sealed record Routed(string Endpoint, string Product);

/// <summary>
/// Works out which transmitter each Windows audio endpoint belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Two transmitters give two sets of endpoints that look almost identical.
/// Windows tells them apart with a "2- " in front of the name, which is
/// positional and not reliable. Each endpoint carries the path of the kernel
/// filter behind it, and that path holds the USB product id: the same id the
/// headset uses to name its paired transmitters.
/// </para>
/// <para>
/// Output and input can be routed to different transmitters with nothing
/// reporting it; seen with the output on the Charging Dock and the
/// microphone still on the USB Transmitter the headset had left.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public static class Routing
{
    private static readonly NKey FilterPath =
        new(new Guid("233164c8-1b2c-4c7d-bc68-b671687a2567"), 1);

    /// <summary>The default output or input, and the product id behind it.</summary>
    /// <remarks>
    /// Windows keeps a second default for communications, which chat apps can
    /// use and which does not move when the output is changed in Sound
    /// settings.
    /// </remarks>
    /// <returns>The route, or null if there is no default or it cannot be read.</returns>
    public static Routed? Default(bool output, bool communications = false)
    {
        try
        {
            using var devices = new MMDeviceEnumerator();
            using var device = devices.GetDefaultAudioEndpoint(
                output ? NFlow.Render : NFlow.Capture,
                communications ? NRole.Communications : NRole.Multimedia);
            return new Routed(device.FriendlyName, ProductOf(device));
        }
        catch { return null; }
    }

    /// <summary>Lists the active endpoints belonging to one transmitter, by product id.</summary>
    public static List<string> Belonging(string product, bool output)
    {
        var found = new List<string>();
        try
        {
            using var devices = new MMDeviceEnumerator();
            foreach (var device in devices.EnumerateAudioEndPoints(
                         output ? NFlow.Render : NFlow.Capture, NState.Active))
                using (device)
                    if (string.Equals(ProductOf(device), product, StringComparison.OrdinalIgnoreCase))
                        found.Add(device.FriendlyName);
        }
        catch { }
        return found;
    }

    /// <summary>
    /// The product id of the headset's own audio device when it is plugged in
    /// with the USB-C cable, or empty when it is not.
    /// </summary>
    public static string Cable()
    {
        try
        {
            using var devices = new MMDeviceEnumerator();
            foreach (var device in devices.EnumerateAudioEndPoints(NFlow.Render, NState.Active))
                using (device)
                {
                    string product = ProductOf(device);
                    if (product.Length > 0
                        && Transmitters.PieceOf(product) == Transmitters.Piece.Headset)
                        return product;
                }
        }
        catch { }
        return "";
    }

    /// <summary>
    /// The headset output or microphone Windows is using, if it is using one;
    /// otherwise the first that matches.
    /// </summary>
    /// <remarks>
    /// The one in use comes first for the reason given on
    /// <see cref="Endpoint.FindMatching"/>: there can be several with the same
    /// name, one of them over the USB-C cable.
    /// </remarks>
    public static MMDevice? Headset(MMDeviceEnumerator devices, bool output, string match = "Stealth Pro")
    {
        var flow = output ? NFlow.Render : NFlow.Capture;
        try
        {
            var current = devices.GetDefaultAudioEndpoint(flow, NRole.Multimedia);
            if (current.FriendlyName.Contains(match, StringComparison.OrdinalIgnoreCase)) return current;
            current.Dispose();
        }
        catch { }

        MMDevice? found = null;
        foreach (var device in devices.EnumerateAudioEndPoints(flow, NState.Active))
        {
            if (found is null && device.FriendlyName.Contains(match, StringComparison.OrdinalIgnoreCase))
                found = device;
            else
                device.Dispose();
        }
        return found;
    }

    /// <summary>The process ids of the applications recording from the headset's microphone.</summary>
    /// <returns>Empty as well when there is no headset microphone or Windows cannot be asked.</returns>
    public static IReadOnlyList<uint> Recorders(string match = "Stealth Pro")
    {
        var found = new List<uint>();
        try
        {
            using var devices = new MMDeviceEnumerator();
            using var microphone = Headset(devices, output: false, match);
            if (microphone is null) return found;
            microphone.AudioSessionManager.RefreshSessions();
            var sessions = microphone.AudioSessionManager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
                if (sessions[i].State == AudioSessionState.AudioSessionStateActive) found.Add(sessions[i].GetProcessID);
        }
        catch (COMException) { found.Clear(); }
        return found;
    }

    /// <summary>
    /// Where Windows sends sound and takes the microphone from, for each role,
    /// and the volume, level and programs on each of the headset's devices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A default says where sound is sent, not where it is heard, so each of
    /// the headset's devices is listened to for <paramref name="listen"/> and
    /// its loudest moment kept. One reading is a single instant and says
    /// little about a level that moves.
    /// </para>
    /// <para>
    /// Open but silent programs are listed too, marked apart: a chat app
    /// between calls is where its sound will go.
    /// </para>
    /// </remarks>
    /// <param name="listen">How long to listen to each device's level.</param>
    /// <param name="name">Names the program behind a process id; <see cref="Programs.NameOf"/> when not given.</param>
    /// <exception cref="COMException">Windows' audio devices could not be listed.</exception>
    public static SoundSurvey Survey(TimeSpan listen, Func<uint, string>? name = null)
    {
        name ??= Programs.NameOf;
        using var devices = new MMDeviceEnumerator();

        var defaults = new List<SoundDefault>();
        foreach (var flow in new[] { NFlow.Render, NFlow.Capture })
            foreach (var role in new[] { NRole.Multimedia, NRole.Console, NRole.Communications })
            {
                try
                {
                    using var device = devices.GetDefaultAudioEndpoint(flow, role);
                    defaults.Add(new(flow == NFlow.Render, role.ToString(), device.FriendlyName, ProductOf(device)));
                }
                catch (COMException)
                {
                    // Windows has no device for the role.
                    defaults.Add(new(flow == NFlow.Render, role.ToString(), "", ""));
                }
            }

        var headset = new List<MMDevice>();
        try
        {
            foreach (var flow in new[] { NFlow.Render, NFlow.Capture })
                foreach (var device in devices.EnumerateAudioEndPoints(flow, NState.Active))
                    if (ProductOf(device).Length > 0) headset.Add(device);
                    else device.Dispose();

            var peaks = new float[headset.Count];
            var clock = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                for (int i = 0; i < headset.Count; i++)
                    peaks[i] = Math.Max(peaks[i], headset[i].AudioMeterInformation.MasterPeakValue);
                if (clock.Elapsed < listen) Thread.Sleep(50);
            }
            while (clock.Elapsed < listen);

            var found = headset.Select((device, i) => new SoundDevice(
                device.DataFlow == NFlow.Render, device.FriendlyName, ProductOf(device),
                (int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100),
                device.AudioEndpointVolume.Mute, peaks[i], AppsOn(device, name))).ToList();

            return new SoundSurvey(defaults, found, FormatOf(Flow.Output), FormatOf(Flow.Input));
        }
        finally
        {
            foreach (var device in headset) device.Dispose();
        }
    }

    private static List<SoundApp> AppsOn(MMDevice device, Func<uint, string> name)
    {
        var apps = new List<SoundApp>();
        device.AudioSessionManager.RefreshSessions();
        var sessions = device.AudioSessionManager.Sessions;
        for (int i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            if (session.State == AudioSessionState.AudioSessionStateExpired) continue;
            uint pid = session.GetProcessID;
            string program = pid == 0 ? "system" : name(pid);
            apps.Add(new SoundApp(program.Length > 0 ? program : $"process {pid}",
                (int)Math.Round(session.SimpleAudioVolume.Volume * 100), session.SimpleAudioVolume.Mute,
                session.State == AudioSessionState.AudioSessionStateActive));
        }
        return apps;
    }

    private static string FormatOf(Flow flow)
    {
        try { return DeviceFormat.Current(AudioEndpoints.DefaultMatch, flow)?.Label ?? "not reported"; }
        catch (Exception ex) when (ex is WindowsAudioException or COMException)
        {
            return $"could not be read: {ex.Message}";
        }
    }

    /// <summary>
    /// The product id behind an endpoint if it is a Turtle Beach device, and
    /// empty for anything else.
    /// </summary>
    /// <remarks>
    /// The vendor check (VID 10F5) matters: a USB webcam's microphone has a
    /// product id of its own and would otherwise count as a transmitter the
    /// headset is not on.
    /// </remarks>
    public static string ProductOf(MMDevice device)
    {
        try
        {
            if (!device.Properties.Contains(FilterPath)) return "";
            string path = device.Properties[FilterPath].Value?.ToString() ?? "";
            if (!path.Contains("vid_10f5", StringComparison.OrdinalIgnoreCase)) return "";
            int at = path.IndexOf("pid_", StringComparison.OrdinalIgnoreCase);
            return at >= 0 && path.Length >= at + 8
                ? path.Substring(at + 4, 4).ToUpperInvariant() : "";
        }
        catch { return ""; }
    }
}

