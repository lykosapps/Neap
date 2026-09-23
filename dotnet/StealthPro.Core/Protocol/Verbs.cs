namespace StealthPro.Core.Protocol;

/// <summary>
/// The verbs the device answers to, and which category each one returns.
///
/// Every one is confirmed: either captured from Swarm II or probed and
/// answered. Two things here cost a day each and are worth stating plainly.
///
/// A request verb is always <b>four characters</b>. SBT, SEQ and other
/// three-letter forms get no reply at all. And the prefix is not always "S"
/// — Bluetooth is <c>RBT</c>, which is why every SBT* guess drew a blank.
/// </summary>
public static class Verbs
{
    /// <summary>Settings categories. A full read is these and only these.</summary>
    public static readonly IReadOnlyList<string> SettingCategories = new[]
    {
        "GSI",  // general state,                0x2xx
        "BT",   // bluetooth,                    0x3xx
        "Mic",  // microphone,                   0x6xx
        "SAF",  // ANC / gate / Superhuman,      0x7xx
        "Enc",  // dial assignments,             0xaxx
        "Btn",  // button assignments,           0xbxx
        "AQG",  // game equaliser,               0x12xx
        "AQM",  // microphone equaliser,         0x13xx
        "Ver",  // protocol version
        "Inf",  // identity and firmware,        0x1xx
        "CEC",  // preset counts and deletes,    0x16xx
        "3DT",  // game/chat mix,                0x5xx
    };

    /// <summary>
    /// The five custom equaliser slots per bank, each its own category.
    /// Each answers with that slot's name and all ten bands in one reply,
    /// which is how the presets are listed without selecting them.
    /// </summary>
    public static readonly IReadOnlyList<string> PresetSlotCategories =
        Enumerable.Range(1, 5).Select(n => $"CG{n}")
            .Concat(Enumerable.Range(1, 5).Select(n => $"CM{n}")).ToArray();

    /// <summary>The headset pairs with up to four transmitters, one slot each.</summary>
    public static readonly IReadOnlyList<string> TransmitterCategories =
        Enumerable.Range(1, 4).Select(n => $"TX{n}").ToArray();

    /// <summary>category -> request verb, for everything readable.</summary>
    public static readonly IReadOnlyDictionary<string, string> Readers =
        BuildReaders();

    private static Dictionary<string, string> BuildReaders()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GSI"] = "SGSI",
            ["BT"] = "RBT",       // not SBT — see the class remarks
            ["Mic"] = "SMic",
            ["SAF"] = "SSAF",
            ["Enc"] = "SEnc",
            ["Btn"] = "SBtn",
            ["AQG"] = "SAQG",
            ["AQM"] = "SAQM",
            ["Ver"] = "SVer",
            ["Inf"] = "SInf",
            ["CEC"] = "SCEC",
            ["3DT"] = "S3DT",
        };
        foreach (var category in PresetSlotCategories) map[category] = "S" + category;
        foreach (var category in TransmitterCategories) map[category] = "S" + category;
        return map;
    }

    /// <summary>Custom slot n reads at 0x1700 (game) / 0x1800 (mic) + 0x20*(n-1).</summary>
    public static int PresetSlotKey(string category)
    {
        int n = category[2] - '0';
        return (category[1] == 'G' ? 0x1700 : 0x1800) + 0x20 * (n - 1);
    }

    /// <summary>Transmitter slot n reads at 0x400 + 0x20*(n-1).</summary>
    public static int TransmitterKey(string category) =>
        0x400 + 0x20 * (category[2] - '1');

    internal static bool TakesArgument(string verb) => verb == "set_kvp";

    internal static byte[] Tag(string verb)
    {
        if (verb == "set_kvp") return Frames.TagFor(true);
        if (Readers.Values.Contains(verb)) return Frames.TagFor(false);
        throw new ProtocolException(
            $"unknown verb '{verb}' — anything not confirmed against the "
            + "device is refused rather than guessed at");
    }
}
