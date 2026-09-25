using System.Globalization;
using System.Text.Json;
using Neap.Core.Protocol;

namespace Neap.Core.Presets;

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
/// Lists, applies, saves and deletes the equaliser presets stored on the headset.
/// </summary>
/// <remarks>
/// <para>
/// Listing takes ten reads and makes no sound. Each custom slot is its own
/// category (CG1..CG5 for the game bank, CM1..CM5 for the microphone, read
/// with SCG1..SCG5 and SCM1..SCM5), and each answers with the slot's name and
/// all ten bands in one reply. Swarm II lists them the same way and keeps no
/// list of its own.
/// </para>
/// <para>
/// Saving is a sequence of writes with no save command: a slot id, then the
/// ten bands, then the name. Writing the name commits it. Deleting is one
/// write of the preset's name to the delete register.
/// </para>
/// </remarks>
public static class PresetStore
{
    /// <summary>Custom slot n is preset id 15 + n.</summary>
    public const int FirstCustomId = 16;
    public const int CustomSlots = 5;

    /// <summary>The longest preset name the headset will take, in plain ASCII characters.</summary>
    /// <remarks>
    /// A name is written as a single set_kvp frame and the protocol has no
    /// continuation, so it must fit one 62-byte output report. Everything
    /// around the name (report id, the two lengths, the RACE header, the verb,
    /// and the JSON braces and quotes) costs 43 bytes. Turtle Beach's own
    /// longest preset name, "Bass &amp; Treble Boost", is exactly 19
    /// characters, which suggests Swarm II works to the same ceiling.
    /// </remarks>
    public const int MaxNameLength = 19;

    /// <summary>The step the headset takes band values in, in tenths: half a decibel.</summary>
    /// <remarks>
    /// Values between steps are not stored as sent (measured): +2.7 dB left
    /// the band where it was and +2.3 dB became +2.0, while +2.5, +2.0,
    /// +0.5 and 0 were stored as written. The write still counts as an edit,
    /// so nothing says it missed.
    /// </remarks>
    public const int BandStep = 5;

    /// <summary>A band value moved to the nearest step the headset takes.</summary>
    public static int Snap(int tenths) =>
        (int)Math.Round(tenths / (double)BandStep, MidpointRounding.AwayFromZero) * BandStep;

    /// <summary>Whether this name fits one output report once escaped.</summary>
    /// <remarks>
    /// Counting characters is not enough: a quotation mark costs two bytes and
    /// anything outside plain ASCII costs six.
    /// </remarks>
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
        // Eight of these appear literally in Swarm II's resources. Qt pools
        // identical string literals, so "250 Hz" and "1 kHz", already in the
        // game list, appear only once for both banks; their placement here is
        // inferred, not read.
        new[] { "100 Hz", "160 Hz", "250 Hz", "400 Hz", "630 Hz",
                "1 kHz", "1.6 kHz", "2.5 kHz", "4 kHz", "6.3 kHz" });

    public static BankSpec Spec(Bank bank) => bank == Bank.Game ? Game : Microphone;

    /// <summary>The headset's built-in presets for a bank.</summary>
    /// <remarks>
    /// Factory presets are fixed in firmware and are not in the custom slots,
    /// so there is nothing to read them from; Swarm II has them built in too.
    /// These values were measured off the headset by selecting each preset and
    /// reading it back.
    /// </remarks>
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

    /// <summary>Reads the used custom slots straight off the headset, without changing what plays.</summary>
    /// <remarks>
    /// An empty slot answers with an empty name. A slot that does not answer
    /// at all is also left out, but it is not known to be empty, so do not
    /// treat it as free to write.
    /// </remarks>
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

    /// <summary>How many custom presets the headset says it holds.</summary>
    /// <remarks>
    /// A cheap cross-check on <see cref="ReadCustoms"/>: if this disagrees
    /// with the number of named slots, the fault is in our reading rather than
    /// in the headset.
    /// </remarks>
    public static int? Count(HeadsetClient client, Bank bank, TimeSpan? wait = null)
    {
        var values = client.ReadCategory("CEC", wait ?? TimeSpan.FromMilliseconds(1200));
        return values.TryGetValue($"{Spec(bank).CountKey:x}", out var raw)
               && TryNumber(raw.GetString(), out int count) ? count : null;
    }

    // -- writing -----------------------------------------------------------

    /// <summary>Selects a stored preset.</summary>
    public static void Apply(HeadsetClient client, int presetId, Bank bank) =>
        client.Set(Spec(bank).Select, presetId);

    /// <summary>Saves a new preset into the headset's custom slots.</summary>
    /// <remarks>
    /// <para>
    /// The order is Swarm II's and it matters: write a slot id, fill in the
    /// ten bands, then write the name. There is no save command; writing the
    /// name commits it.
    /// </para>
    /// <para>
    /// Saving always creates a new preset. The slot id is only a hint: asking
    /// for slot 19 puts the preset in the lowest free slot instead (measured),
    /// and asking for an occupied slot adds a second preset rather than
    /// overwriting. To replace a preset, delete it and then save.
    /// </para>
    /// </remarks>
    /// <param name="hint">The slot id to write first; the headset may ignore it.</param>
    /// <exception cref="PresetException">
    /// The band count is wrong, or the name is empty or does not fit.
    /// </exception>
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

    /// <summary>Deletes a custom preset by name.</summary>
    /// <remarks>
    /// Deleting works whatever preset is selected, but if the preset being
    /// deleted is the selected one, <paramref name="fallback"/> is selected
    /// first so the headset is not left pointing at a slot that no longer
    /// exists. Swarm II does the same.
    /// </remarks>
    /// <exception cref="PresetException">The name is empty or names a factory preset.</exception>
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
