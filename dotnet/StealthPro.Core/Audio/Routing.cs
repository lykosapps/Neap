using NAudio.CoreAudioApi;
using NFlow = NAudio.CoreAudioApi.DataFlow;
using NRole = NAudio.CoreAudioApi.Role;
using NKey = NAudio.CoreAudioApi.PropertyKey;
using NState = NAudio.CoreAudioApi.DeviceState;

namespace StealthPro.Core.Audio;

/// <summary>Where Windows is sending sound, and which transmitter owns it.</summary>
public sealed record Routed(string Endpoint, string Product);

/// <summary>
/// Which transmitter each audio endpoint belongs to.
///
/// <b>Two transmitters give two sets of endpoints and they look almost
/// identical.</b> Windows tells them apart with a "2- " in front of the
/// name, which is positional and not something to depend on; the endpoint
/// itself carries the path of the kernel filter behind it, and that path
/// holds the USB product id. That is the honest way to ask which device an
/// endpoint is, and it is the same product id the headset uses to name its
/// paired transmitters.
///
/// This exists because the two can disagree without anything saying so:
/// found on the test machine with the output correctly on the dock and the
/// <i>microphone</i> still on the USB transmitter the headset had left.
/// </summary>
public static class Routing
{
    private static readonly NKey FilterPath =
        new(new Guid("233164c8-1b2c-4c7d-bc68-b671687a2567"), 1);

    /// <summary>
    /// The default output or input, and the product id behind it. Windows
    /// keeps a second default for communications, which chat apps can use
    /// and which does not move when the output is changed in Sound settings.
    /// </summary>
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

    /// <summary>Endpoints belonging to one transmitter, by product id.</summary>
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
    /// The headset's own audio device, when it is plugged in with a USB-C
    /// cable: its product id, or empty when it is not.
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
    /// The headset's output or microphone that Windows is using, if it is
    /// using one; otherwise the first of them. The one in use comes first for
    /// the reason given on <see cref="AudioEndpoints"/>: there can be several,
    /// all with the same name, one of them over a cable.
    /// </summary>
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
    /// The product id behind an endpoint, if it is one of Turtle Beach's; empty
    /// for anything else. Without the vendor check a USB webcam's microphone,
    /// which has a product id of its own, would count as a transmitter the
    /// headset is not on.
    /// </summary>
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

