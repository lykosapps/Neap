using System.Globalization;
using System.Text.Json;
using StealthPro.Core.Protocol;

namespace StealthPro.Core.Presets;

public class PresetException : Exception
{
    public PresetException(string message) : base(message) { }
}

public enum Bank { Game, Mic }

public sealed record Preset(
    int Id, string Name, IReadOnlyList<int> Bands, Bank Bank, bool Custom);

/// <summary>Which keys and categories a bank lives at.</summary>
public sealed record BankSpec(
    Bank Bank, string Label, int Select, int NameKey, IReadOnlyList<int> Bands,
    string Category, IReadOnlyList<string> Slots, int DeleteKey, int CountKey,
    IReadOnlyList<string> Frequencies);

/// <summary>
/// Equaliser presets stored on the headset.
///
/// Listing them takes ten reads and makes no sound: each custom slot is its
/// own category — SCG1..SCG5 for the game bank, SCM1..SCM5 for the
/// microphone — and each answers with that slot's name and all ten bands in
/// one reply. This is exactly what Swarm II does; it keeps no list of its own.
///
/// Saving is three writes and no command: a slot id, then the ten bands,
/// then the name. Writing the name commits it. Deleting is one write of the
/// preset's *name* to the delete register.
///
/// Ported from stealthpro/presets.py.
/// </summary>
public static class PresetStore
{
    /// <summary>Custom slot n is preset id 15 + n.</summary>
    public const int FirstCustomId = 16;
    public const int CustomSlots = 5;

    /// <summary>
    /// The longest name the headset will take.
    ///
    /// A name is written as a single set_kvp frame and the protocol has no
    /// continuation, so the whole thing has to fit one 62-byte output
    /// report. Everything wrapped around the name — report id, the two
    /// lengths, the RACE header, the verb, and the JSON braces and quotes —
    /// costs 43 bytes. Turtle Beach's own longest preset name, "Bass &amp;
    /// Treble Boost", is exactly 19 characters, which is good evidence this
    /// is the ceiling Swarm works to as well.
    /// </summary>
    public const int MaxNameLength = 19;

    /// <summary>
    /// Whether this particular name fits, escaping included. Counting
    /// characters is not enough on its own: a quotation mark costs two and
    /// anything outside plain ASCII costs six.
    /// </summary>
    public static bool NameFits(string name, Bank bank) =>
        Frames.SetKey(Spec(bank).NameKey, name.Trim()).Length <= Frames.ReportLength;

    public static readonly BankSpec Game = new(
        Bank.Game, "Game", 0x1210, 0x12C0,
        Enumerable.Range(0, 10).Select(i => 0x1220 + 0x10 * i).ToArray(),
        "AQG", Enumerable.Range(1, 5).Select(n => $"CG{n}").ToArray(),
        0x1610, 0x1600,
        new[] { "32 Hz", "63 Hz", "125 Hz", "250 Hz", "500 Hz",
                "1 kHz", "2 kHz", "4 kHz", "8 kHz", "16 kHz" });

    public static readonly BankSpec Microphone = new(
        Bank.Mic, "Microphone", 0x1310, 0x13C0,
        Enumerable.Range(0, 10).Select(i => 0x1320 + 0x10 * i).ToArray(),
        "AQM", Enumerable.Range(1, 5).Select(n => $"CM{n}").ToArray(),
        0x1630, 0x1620,
        // Eight of these appear literally in Swarm's resources and the bank
        // has ten bands. Qt pools identical string literals, so "250 Hz" and
        // "1 kHz" — already in the game list — would appear only once for
        // both banks. Those two placements are inferred, not read.
        new[] { "100 Hz", "160 Hz", "250 Hz", "400 Hz", "630 Hz",
                "1 kHz", "1.6 kHz", "2.5 kHz", "4 kHz", "6.3 kHz" });

    public static BankSpec Spec(Bank bank) => bank == Bank.Game ? Game : Microphone;

    /// <summary>
    /// The factory presets are fixed in firmware and are not in the custom
    /// slots, so there is nothing to read them from — Swarm II has them built
    /// in too. These were measured off the headset by selecting each one and
    /// reading it back, not copied from anywhere.
    /// </summary>
    public static IReadOnlyList<Preset> Factory(Bank bank) =>
        bank == Bank.Game ? GameFactory : MicFactory;

    private static readonly Preset[] GameFactory =
    [
        new(1, "Signature Sound", new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, Bank.Game, false),
        new(2, "Bass Boost", new[] { 50, 50, 35, 0, 0, 0, 0, 0, 0, 0 }, Bank.Game, false),
        new(3, "Bass & Treble Boost", new[] { 50, 50, 35, 0, 0, 0, 30, 50, 50, 50 }, Bank.Game, false),
        new(4, "Vocal Boost", new[] { 0, 0, 20, 35, 35, 35, 20, 0, 0, 0 }, Bank.Game, false),
    ];

    private static readonly Preset[] MicFactory =
    [
        new(1, "Signature Sound", new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, Bank.Mic, false),
        new(2, "Full", new[] { -30, 50, 50, 30, 0, -11, 0, 0, 0, 0 }, Bank.Mic, false),
        new(3, "Clarity", new[] { -30, -30, -20, 0, 0, 0, 30, 40, 40, 0 }, Bank.Mic, false),
        new(4, "Smooth", new[] { 0, 0, 0, 0, -20, -20, 0, 20, 20, 0 }, Bank.Mic, false),
    ];

    // -- reading -----------------------------------------------------------

    /// <summary>
    /// The custom slots, read straight off the headset. Silent.
    ///
    /// An empty slot answers with an empty name, which is how it is told
    /// apart from a used one. A slot that does not answer <i>at all</i> is
    /// skipped rather than reported as empty — those are different, and only
    /// one of them means "you may write here".
    /// </summary>
    public static List<Preset> ReadCustoms(
        HeadsetClient client, Bank bank, TimeSpan? wait = null)
    {
        var spec = Spec(bank);
        var window = wait ?? TimeSpan.FromSeconds(1);
        var found = new List<Preset>();

        for (int index = 0; index < spec.Slots.Count; index++)
        {
            string category = spec.Slots[index];
            var values = client.ReadCategory(category, window);
            string key = $"{Verbs.PresetSlotKey(category):x}";
            if (!values.TryGetValue(key, out var slot)
                || slot.ValueKind != JsonValueKind.Object) continue;

            string name = slot.TryGetProperty("name", out var n)
                ? (n.GetString() ?? "").Trim() : "";
            if (name.Length == 0) continue;           // genuinely empty

            var bands = new List<int>();
            if (slot.TryGetProperty("bands", out var b) && b.ValueKind == JsonValueKind.Array)
                foreach (var value in b.EnumerateArray())
                    bands.Add(TryNumber(value.GetString(), out int parsed) ? parsed : 0);

            found.Add(new Preset(FirstCustomId + index, name, bands, bank, true));
        }
        return found;
    }

    /// <summary>Everything in one bank: the factory presets plus whatever is stored.</summary>
    public static List<Preset> ReadBank(HeadsetClient client, Bank bank) =>
        Factory(bank).Concat(ReadCustoms(client, bank)).ToList();

    /// <summary>Whatever preset is selected on the headset right now, if any.</summary>
    public static Preset? ReadCurrent(HeadsetClient client, Bank bank, TimeSpan? wait = null)
    {
        var spec = Spec(bank);
        var values = client.ReadCategory(spec.Category, wait ?? TimeSpan.FromMilliseconds(1200));
        if (!values.TryGetValue($"{spec.Select:x}", out var selected)) return null;
        if (!TryNumber(selected.GetString(), out int id)) return null;

        string name = values.TryGetValue($"{spec.NameKey:x}", out var n)
            ? (n.GetString() ?? "").Trim() : "";
        var bands = spec.Bands
            .Select(key => values.TryGetValue($"{key:x}", out var v)
                           && TryNumber(v.GetString(), out int parsed) ? parsed : 0)
            .ToArray();

        return new Preset(id, name, bands, bank, id >= FirstCustomId);
    }

    /// <summary>A number as the headset sends it, whatever the PC's language.</summary>
    private static bool TryNumber(string? text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <summary>Custom ids with nothing in them.</summary>
    public static List<int> FreeSlots(IEnumerable<Preset> presets)
    {
        var used = presets.Where(p => p.Custom).Select(p => p.Id).ToHashSet();
        return Enumerable.Range(FirstCustomId, CustomSlots).Where(id => !used.Contains(id)).ToList();
    }

    /// <summary>
    /// How many custom presets the headset says it holds.
    ///
    /// Only a cross-check now that the slots can be read directly, but a
    /// cheap one: if this disagrees with the number of named slots, the
    /// fault is in our reading rather than in the headset.
    /// </summary>
    public static int? Count(HeadsetClient client, Bank bank, TimeSpan? wait = null)
    {
        var values = client.ReadCategory("CEC", wait ?? TimeSpan.FromMilliseconds(1200));
        return values.TryGetValue($"{Spec(bank).CountKey:x}", out var raw)
               && TryNumber(raw.GetString(), out int count) ? count : null;
    }

    // -- writing -----------------------------------------------------------

    /// <summary>Select a stored preset.</summary>
    public static void Apply(HeadsetClient client, int presetId, Bank bank) =>
        client.Set(Spec(bank).Select, presetId);

    /// <summary>
    /// Write a preset into the headset's custom slots.
    ///
    /// The order matters and it is Swarm II's order: write a slot id, fill in
    /// the ten bands, then write the name. There is no save command — writing
    /// the name is what commits it.
    ///
    /// <b>Saving always creates a new preset.</b> The slot id is only a hint:
    /// ask for slot 19 and the headset puts it in the lowest free slot
    /// instead, and asking for an occupied slot gets a second preset rather
    /// than an overwrite. Measured, not assumed. Replacing therefore means
    /// delete-then-save.
    /// </summary>
    public static void Save(HeadsetClient client, string name, IReadOnlyList<int> bands,
        Bank bank, int? hint = null)
    {
        var spec = Spec(bank);
        if (bands.Count != spec.Bands.Count)
            throw new PresetException($"expected {spec.Bands.Count} bands, got {bands.Count}");
        name = name.Trim();
        if (name.Length == 0) throw new PresetException("a preset needs a name");
        if (!NameFits(name, bank))
            throw new PresetException(
                $"'{name}' is longer than the headset can store — "
                + $"{MaxNameLength} characters is the most it takes");

        client.Set(spec.Select, hint ?? FirstCustomId);
        Thread.Sleep(150);
        for (int i = 0; i < bands.Count; i++)
        {
            client.Set(spec.Bands[i], bands[i]);
            Thread.Sleep(50);
        }
        Thread.Sleep(100);
        client.Set(spec.NameKey, name);
    }

    /// <summary>
    /// Remove a custom preset, by name.
    ///
    /// Works whatever preset is selected at the time, but if the one being
    /// deleted is the one you are listening to we move to
    /// <paramref name="fallback"/> first, so the headset is not left pointing
    /// at a slot that no longer exists. Swarm II does the same.
    /// </summary>
    public static void Delete(HeadsetClient client, string name, Bank bank, int fallback = 1)
    {
        var spec = Spec(bank);
        name = name.Trim();
        if (name.Length == 0) throw new PresetException("say which preset to delete");
        if (Factory(bank).Any(p => p.Name == name))
            throw new PresetException(
                $"{name} is one of the headset's own presets, so it cannot be deleted");

        var current = ReadCurrent(client, bank);
        if (current is not null && current.Name == name)
        {
            client.Set(spec.Select, fallback);
            Thread.Sleep(200);
        }
        client.Set(spec.DeleteKey, name);
    }
}
