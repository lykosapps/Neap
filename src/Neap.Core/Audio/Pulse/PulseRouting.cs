using System.Runtime.Versioning;

namespace Neap.Core.Audio.Pulse;

/// <summary>
/// Where Linux's sound server sends sound and takes the microphone from, and
/// which transmitter owns each device: <see cref="Routing"/>'s counterpart.
/// </summary>
/// <remarks>
/// <para>
/// A device is the headset's when the USB ids behind it say so, the same
/// rule Windows' filter path is read for. The sound server has one default
/// output and one default microphone, with no second pair for calls, so the
/// calls answer is the same device.
/// </para>
/// <para>
/// Every call asks the server afresh. Each one that cannot reach it says so
/// with null or empty, as <see cref="Routing"/> does, rather than throwing;
/// a missing sound server is an ordinary state to report, not a crash.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
public static class PulseRouting
{
    /// <summary>The default output or microphone, and the product id behind it.</summary>
    /// <returns>The route, or null if there is no default or the server cannot be asked.</returns>
    public static Routed? Default(bool output)
    {
        try
        {
            using var client = new PulseClient();
            string name = output ? client.DefaultSink() : client.DefaultSource();
            var device = (output ? client.Sinks() : client.Sources()).FirstOrDefault(d => d.Name == name);
            return device is null ? null : new Routed(device.Description, ProductOf(device));
        }
        catch (PulseException) { return null; }
    }

    /// <summary>The active devices belonging to one transmitter, by product id.</summary>
    public static List<string> Belonging(string product, bool output)
    {
        try
        {
            using var client = new PulseClient();
            return (output ? client.Sinks() : client.Sources())
                .Where(d => string.Equals(ProductOf(d), product, StringComparison.OrdinalIgnoreCase))
                .Select(d => d.Description).ToList();
        }
        catch (PulseException) { return []; }
    }

    /// <summary>
    /// The product id of the headset's own audio device when it is plugged in
    /// with the USB-C cable, or empty when it is not.
    /// </summary>
    public static string Cable()
    {
        try
        {
            using var client = new PulseClient();
            foreach (var sink in client.Sinks())
            {
                string product = ProductOf(sink);
                if (product.Length > 0 && Transmitters.PieceOf(product) == Transmitters.Piece.Headset) return product;
            }
        }
        catch (PulseException) { }
        return "";
    }

    /// <summary>The product id behind a device if it is a Turtle Beach one, and empty for anything else.</summary>
    private static string ProductOf(PulseSink device) => device.Vendor == "10F5" ? device.Product : "";
}
