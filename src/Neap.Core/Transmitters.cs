using System.Globalization;
using System.Text.Json;
using Neap.Core.Protocol;

namespace Neap.Core;

public sealed record Transmitter(
    int Slot, bool Paired, bool Active, string Kind, string ProductId,
    string VendorId, string Firmware, string Address,
    IReadOnlyList<string> Info, IReadOnlyList<string> Control);

/// <summary>
/// The headset pairs with up to four transmitters and keeps a slot for each.
/// </summary>
public static class Transmitters
{
    public const int Slots = 4;

    /// <summary>What a Turtle Beach device in this family actually is.</summary>
    public enum Piece { Unknown, Dock, Transmitter, Headset }

    /// <summary>
    /// The whole Stealth Pro II family by product id, read out of Swarm II's
    /// own product catalogue rather than guessed at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The catalogue is in Swarm's settings.json, zlib-compressed behind a
    /// four-byte header. It is the authority for which id is which; the order
    /// of firmware folders is not. It confirms 229B as the white Xbox dock and
    /// 229D as its transmitter, and names 2283 as the black Xbox dock. The
    /// vendor's own files name what captures only show anonymously.
    /// </para>
    /// <para>
    /// Swarm calls the docking device a base; the box calls it a "CrossPlay
    /// 2.0 Transmitter Dock". The box wins for anything a person reads,
    /// because that is the word they were sold.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, Piece> Family =
        new Dictionary<string, Piece>(StringComparer.OrdinalIgnoreCase)
        {
            // Xbox, black
            ["2283"] = Piece.Dock,
            ["2284"] = Piece.Dock,
            ["2285"] = Piece.Transmitter,
            ["2286"] = Piece.Headset,
            ["229F"] = Piece.Transmitter,
            // Xbox, white
            ["229B"] = Piece.Dock,
            ["229C"] = Piece.Dock,
            ["229D"] = Piece.Transmitter,
            ["229E"] = Piece.Headset,
            ["22A0"] = Piece.Transmitter,
            // PC
            ["2287"] = Piece.Dock,
            ["2288"] = Piece.Transmitter,
            ["2289"] = Piece.Headset,
        };

    public static Piece PieceOf(string product) =>
        Family.TryGetValue(product, out var piece) ? piece : Piece.Unknown;

    public static Piece PieceOf(ushort product) => PieceOf(product.ToString("X4", CultureInfo.InvariantCulture));

    /// <summary>What to call it on screen.</summary>
    public static readonly IReadOnlyDictionary<string, string> Hardware =
        Family.ToDictionary(
            entry => entry.Key,
            entry => entry.Value switch
            {
                Piece.Dock => "Charging Dock",
                Piece.Transmitter => "USB Transmitter",
                Piece.Headset => "Headset",
                _ => "Transmitter",
            },
            StringComparer.OrdinalIgnoreCase);

    // Indices into "info" that are confirmed.
    private const int InfoState = 0, InfoVendor = 5, InfoProduct = 6,
                      InfoFirmware = 7, InfoAddress = 8;
    // Indices into "control".
    private const int ControlLed1 = 1, ControlLed2 = 2;

    private const string EmptyAddress = "00:00:00:00:00:00";

    /// <summary>
    /// The transmitter the headset says it is actually using, or null.
    /// </summary>
    /// <remarks>
    /// This is not necessarily the device being talked through. With both
    /// plugged in, the Charging Dock can answer every request while the
    /// headset's own slots report the USB Transmitter as active, and the sound
    /// comes out of the USB Transmitter. Which device answers says nothing
    /// about which one is carrying the headset; only the headset knows that.
    /// </remarks>
    public static Transmitter? Active(HeadsetClient client, TimeSpan? wait = null) =>
        ReadAll(client, wait).FirstOrDefault(t => t.Active);

    public static List<Transmitter> ReadAll(HeadsetClient client, TimeSpan? wait = null)
    {
        var window = wait ?? TimeSpan.FromSeconds(1);
        var found = new List<Transmitter>();
        for (int slot = 1; slot <= Slots; slot++)
        {
            string category = $"TX{slot}";
            var values = client.ReadCategory(category, window);
            values.TryGetValue($"{Verbs.TransmitterKey(category):x}", out var block);
            found.Add(Describe(slot, block));
        }
        return found;
    }

    /// <summary>Turns one slot's reply into a <see cref="Transmitter"/>.</summary>
    public static Transmitter Describe(int slot, JsonElement block)
    {
        var info = StringsOf(block, "info");
        var control = StringsOf(block, "control");

        string address = At(info, InfoAddress);
        string product = At(info, InfoProduct).ToUpperInvariant();
        bool paired = address.Length > 0 && address != EmptyAddress;

        return new Transmitter(
            Slot: slot,
            Paired: paired,
            Active: At(info, InfoState) == "2",
            // An empty slot has no hardware to name, so say nothing rather
            // than reporting "Unrecognised (0)".
            Kind: !paired ? ""
                : Hardware.TryGetValue(product, out var name) ? name
                : $"Unrecognised ({product})",
            ProductId: product,
            VendorId: At(info, InfoVendor).ToUpperInvariant(),
            Firmware: At(info, InfoFirmware),
            Address: paired ? address : "",
            // Kept whole so nothing is silently dropped while most of the
            // array is still unidentified.
            Info: info,
            Control: control);
    }

    /// <summary>Live lighting brightness, from whichever transmitter is active.</summary>
    /// <remarks>
    /// Returned under the registry's own keys so it can be merged straight
    /// into the values an interface already renders.
    /// </remarks>
    public static Dictionary<string, string> Lighting(IEnumerable<Transmitter> transmitters)
    {
        foreach (var entry in transmitters)
        {
            if (!entry.Active || entry.Control.Count <= ControlLed2) continue;
            return new Dictionary<string, string>
            {
                ["401"] = entry.Control[ControlLed1],
                ["402"] = entry.Control[ControlLed2],
            };
        }
        return new Dictionary<string, string>();
    }

    private static List<string> StringsOf(JsonElement block, string property)
    {
        var values = new List<string>();
        if (block.ValueKind != JsonValueKind.Object) return values;
        if (!block.TryGetProperty(property, out var array)
            || array.ValueKind != JsonValueKind.Array) return values;
        foreach (var item in array.EnumerateArray())
            values.Add(item.ValueKind == JsonValueKind.String
                ? item.GetString() ?? "" : item.GetRawText());
        return values;
    }

    private static string At(IReadOnlyList<string> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : "";
}
