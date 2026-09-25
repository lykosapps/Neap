using NAudio.CoreAudioApi;
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

    /// <summary>
    /// The product id behind an endpoint if it is a Turtle Beach device, and
    /// empty for anything else.
    /// </summary>
    /// <remarks>
    /// The vendor check (VID 10F5) matters: a USB webcam's microphone has a
    /// product id of its own and would otherwise count as a transmitter the
    /// headset is not on.
    /// </remarks>
    private static string ProductOf(MMDevice device)
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

