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

/// <summary>What reading one custom slot found.</summary>
public enum SlotFound
{
    /// <summary>A preset, its name and bands read.</summary>
    Preset,

    /// <summary>The slot answered with no name: it holds nothing.</summary>
    Empty,

    /// <summary>No reply came: the slot is empty, or its reply was lost.</summary>
    NoAnswer,

    /// <summary>A reply came that is not a name and a number for each band.</summary>
    Unparsed,
}

/// <summary>One custom slot as read.</summary>
/// <param name="Slot">The slot's category, such as CG1.</param>
/// <param name="Found">What the read found.</param>
/// <param name="Preset">The preset, when one was read.</param>
public sealed record SlotReading(string Slot, SlotFound Found, Preset? Preset = null);

/// <summary>A bank's custom slots as read, and the count the headset gave.</summary>
/// <param name="Slots">Each slot, in order.</param>
/// <param name="Count">How many custom presets the headset says it holds, or null when it did not say.</param>
public sealed record CustomsRead(IReadOnlyList<SlotReading> Slots, int? Count)
{
    /// <summary>The presets read.</summary>
    public IReadOnlyList<Preset> Presets { get; } = [.. Slots.Select(s => s.Preset).OfType<Preset>()];

    /// <summary>Whether every slot is accounted for: the headset gave its count and the presets read match it.</summary>
    /// <remarks>
    /// Only then is a slot that did not answer known to be empty rather than
    /// one whose reply was lost.
    /// </remarks>
    public bool Complete => Count == Presets.Count;

    /// <summary>What was and was not read, for the app log.</summary>
    public string Describe()
    {
        var unknown = Slots
            .Where(s => s.Found is SlotFound.NoAnswer or SlotFound.Unparsed)
            .Select(s => s.Found == SlotFound.NoAnswer ? $"{s.Slot} did not answer" : $"{s.Slot} was not understood")
            .DefaultIfEmpty("every slot answered");
        string count = Count is int n ? $"the headset counts {n}" : "the headset gave no count";
        return $"{Presets.Count} read, {count}; {string.Join(", ", unknown)}";
    }
}

/// <summary>Which keys and categories a bank lives at, and the centre of each of its ten bands in hertz.</summary>
public sealed record BankSpec(
    Bank Bank, int Select, int NameKey, IReadOnlyList<int> Bands,
    string Category, IReadOnlyList<string> Slots, int DeleteKey, int CountKey,
    IReadOnlyList<int> Hertz);

/// <summary>
/// Lists, applies, saves and deletes the equaliser presets stored on the headset.
/// </summary>
/// <remarks>
/// <para>
/// Listing takes ten reads and the counts, and makes no sound. Each custom
/// slot is its own category (CG1..CG5 for the game bank, CM1..CM5 for the
/// microphone, read with SCG1..SCG5 and SCM1..SCM5), and each answers with the
/// slot's name and all ten bands in one reply. Swarm II lists them the same
/// way and keeps no list of its own.
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

    /// <summary>What is wrong with a name for a new custom preset, if anything.</summary>
    /// <remarks>
    /// Everything that would refuse the save, so a dialog can say so while
    /// the name is typed rather than as an error afterwards.
    /// </remarks>
    /// <param name="name">The name typed.</param>
    /// <param name="presets">The bank's presets as read; the headset's own are counted whether or not they are among them.</param>
    /// <param name="bank">Which bank, for its own presets and its name's report.</param>
    /// <param name="fullyRead">Whether every slot is accounted for; see <see cref="CustomsRead.Complete"/>.</param>
    public static PresetNameProblem Check(string name, IReadOnlyList<Preset> presets, Bank bank, bool fullyRead)
    {
        name = name.Trim();
        if (name.Length == 0) return PresetNameProblem.Empty;
        if (presets.Concat(Factory(bank)).Any(p => !p.Custom && PresetName.Same(p.Name, name))) return PresetNameProblem.Factory;
        if (presets.Any(p => p.Custom && PresetName.Same(p.Name, name))) return PresetNameProblem.Taken;
        if (!NameFits(name, bank)) return PresetNameProblem.TooLong;
        if (!fullyRead) return PresetNameProblem.Unread;
        return FreeSlots(presets, fullyRead).Count == 0 ? PresetNameProblem.Full : PresetNameProblem.None;
    }

    public static readonly BankSpec Game = new(
        Bank.Game, 0x1210, 0x12C0,
        Enumerable.Range(0, 10).Select(i => 0x1220 + 0x10 * i).ToArray(),
        "AQG", Enumerable.Range(1, 5).Select(n => $"CG{n}").ToArray(),
        0x1610, 0x1600,
        new[] { 32, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 });

    public static readonly BankSpec Microphone = new(
        Bank.Mic, 0x1310, 0x13C0,
        Enumerable.Range(0, 10).Select(i => 0x1320 + 0x10 * i).ToArray(),
        "AQM", Enumerable.Range(1, 5).Select(n => $"CM{n}").ToArray(),
        0x1630, 0x1620,
        // Eight of these appear literally in Swarm II's resources. Qt pools
        // identical string literals, so "250 Hz" and "1 kHz", already in the
        // game list, appear only once for both banks; their placement here is
        // inferred, not read.
        new[] { 100, 160, 250, 400, 630, 1000, 1600, 2500, 4000, 6300 });

    public static BankSpec Spec(Bank bank) => bank == Bank.Game ? Game : Microphone;

    /// <summary>The headset's built-in presets for a bank.</summary>
    /// <remarks>
    /// Factory presets are fixed in firmware and are not in the custom slots,
    /// so there is nothing to read them from; Swarm II has them built in too.
    /// These values were measured off the headset by selecting each preset and
    /// reading it back. The names are the headset's own, in English, as the
    /// registry's option names are; what a person reads comes from the app's
    /// resource file (see the preset service).
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

    /// <summary>
    /// A name for a copy of a preset: the first free one of the copy
    /// patterns, shortening the preset's own name if that is what it takes to
    /// fit.
    /// </summary>
    /// <param name="name">The preset being copied.</param>
    /// <param name="first">The first copy's pattern, with {0} for the name, such as "{0} copy".</param>
    /// <param name="numbered">Later copies' pattern, with {1} for the number, such as "{0} copy {1}".</param>
    /// <param name="taken">Every name already in the bank, factory presets included.</param>
    /// <param name="bank">Which bank, for its name-length limit and character set.</param>
    public static string CopyName(string name, string first, string numbered, IEnumerable<string> taken, Bank bank)
    {
        var used = taken.Select(t => t.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int copy = 1; copy < 100; copy++)
        {
            for (int keep = name.Length; keep > 0; keep--)
            {
                string stem = name[..keep].TrimEnd();
                string candidate = copy == 1
                    ? string.Format(CultureInfo.CurrentCulture, first, stem)
                    : string.Format(CultureInfo.CurrentCulture, numbered, stem, copy);
                if (candidate.Length > MaxNameLength || !NameFits(candidate, bank)) continue;
                if (!used.Contains(candidate)) return candidate;
                break;
            }
        }
        return "";
    }

    // -- reading -----------------------------------------------------------

    /// <summary>Reads the custom slots straight off the headset, without changing what plays.</summary>
    /// <remarks>See <see cref="ReadCustoms(Bank, Func{int, Task{SlotReading}}, Func{Task{int?}})"/>.</remarks>
    public static CustomsRead ReadCustoms(HeadsetClient client, Bank bank, TimeSpan? wait = null) =>
        ReadCustoms(bank,
            index => Task.FromResult(ReadSlot(client, bank, index, wait)),
            () => Task.FromResult(Count(client, bank, wait))).GetAwaiter().GetResult();

    /// <summary>Reads every custom slot and the count the headset gives, then reads again whatever is not known.</summary>
    /// <remarks>
    /// An empty slot does not answer, and neither does a slot whose reply is
    /// lost, so silence alone does not say a slot is free. The count settles
    /// it: when the presets read match it, the slots that did not answer are
    /// empty. When they do not, each slot not known is read once more, and the
    /// count too if it was not given.
    /// </remarks>
    /// <param name="bank">Which bank.</param>
    /// <param name="readSlot">Reads one slot, by its index from zero.</param>
    /// <param name="count">Reads how many custom presets the headset says it holds.</param>
    public static async Task<CustomsRead> ReadCustoms(
        Bank bank, Func<int, Task<SlotReading>> readSlot, Func<Task<int?>> count)
    {
        var slots = new SlotReading[Spec(bank).Slots.Count];
        for (int i = 0; i < slots.Length; i++) slots[i] = await readSlot(i).ConfigureAwait(false);
        var read = new CustomsRead([.. slots], await count().ConfigureAwait(false));
        if (read.Complete) return read;

        for (int i = 0; i < slots.Length; i++)
            if (slots[i].Found is SlotFound.NoAnswer or SlotFound.Unparsed)
                slots[i] = await readSlot(i).ConfigureAwait(false);
        return new CustomsRead(slots, read.Count ?? await count().ConfigureAwait(false));
    }

    /// <summary>Reads one custom slot straight off the headset.</summary>
    /// <remarks>
    /// One that answers with an empty name is empty. A reply that is not a
    /// name and one number for each band is not understood, and is not made
    /// into a preset with bands it does not have. An empty slot costs the
    /// whole wait.
    /// </remarks>
    public static SlotReading ReadSlot(HeadsetClient client, Bank bank, int index, TimeSpan? wait = null)
    {
        var spec = Spec(bank);
        string category = spec.Slots[index];
        var values = client.ReadCategory(category, wait ?? TimeSpan.FromSeconds(1));
        if (values.Count == 0) return new SlotReading(category, SlotFound.NoAnswer);

        string key = $"{Verbs.PresetSlotKey(category):x}";
        if (!values.TryGetValue(key, out var slot) || slot.ValueKind != JsonValueKind.Object
            || !slot.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String)
            return new SlotReading(category, SlotFound.Unparsed);

        string name = n.GetString()!.Trim();
        if (name.Length == 0) return new SlotReading(category, SlotFound.Empty);

        if (!slot.TryGetProperty("bands", out var b) || b.ValueKind != JsonValueKind.Array
            || b.GetArrayLength() != spec.Bands.Count)
            return new SlotReading(category, SlotFound.Unparsed);
        var bands = new List<int>();
        foreach (var value in b.EnumerateArray())
        {
            if (!TryNumber(value, out int parsed)) return new SlotReading(category, SlotFound.Unparsed);
            bands.Add(parsed);
        }

        return new SlotReading(category, SlotFound.Preset, new Preset(FirstCustomId + index, name, bands, bank, true));
    }

    /// <summary>Whatever preset is selected on the headset right now, or null when none is or it cannot be read.</summary>
    public static Preset? ReadCurrent(HeadsetClient client, Bank bank, TimeSpan? wait = null)
    {
        var spec = Spec(bank);
        var values = client.ReadCategory(spec.Category, wait ?? TimeSpan.FromMilliseconds(1200));
        if (!values.TryGetValue($"{spec.Select:x}", out var selected) || !TryNumber(selected, out int id)) return null;

        string name = NameIn(values, spec);
        var bands = new int[spec.Bands.Count];
        for (int i = 0; i < bands.Length; i++)
            if (!values.TryGetValue($"{spec.Bands[i]:x}", out var v) || !TryNumber(v, out bands[i])) return null;

        return new Preset(id, name, bands, bank, id >= FirstCustomId);
    }

    /// <summary>The name of the preset in use, from a read of its bank's category, or empty when it is not there.</summary>
    private static string NameIn(IReadOnlyDictionary<string, JsonElement> values, BankSpec spec) =>
        values.TryGetValue($"{spec.NameKey:x}", out var name) && name.ValueKind == JsonValueKind.String
            ? name.GetString()!.Trim() : "";

    /// <summary>A number as the headset sends it, as text or as a number, whatever the PC's language.</summary>
    private static bool TryNumber(JsonElement value, out int number)
    {
        number = 0;
        return value.ValueKind switch
        {
            JsonValueKind.String => int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number),
            JsonValueKind.Number => value.TryGetInt32(out number),
            _ => false,
        };
    }

    /// <summary>Custom ids with nothing in them, or none when not every slot could be read.</summary>
    /// <remarks>
    /// A slot missing from a bank not fully read may hold a preset whose reply
    /// was lost, so offering it would let a save go ahead on a full bank.
    /// </remarks>
    /// <param name="presets">The presets read.</param>
    /// <param name="fullyRead">Whether every slot is accounted for; see <see cref="CustomsRead.Complete"/>.</param>
    public static List<int> FreeSlots(IEnumerable<Preset> presets, bool fullyRead)
    {
        if (!fullyRead) return [];
        var used = presets.Where(p => p.Custom).Select(p => p.Id).ToHashSet();
        return Enumerable.Range(FirstCustomId, CustomSlots).Where(id => !used.Contains(id)).ToList();
    }

    /// <summary>How many custom presets the headset says it holds, or null when it does not say.</summary>
    public static int? Count(HeadsetClient client, Bank bank, TimeSpan? wait = null)
    {
        var values = client.ReadCategory("CEC", wait ?? TimeSpan.FromMilliseconds(1200));
        return values.TryGetValue($"{Spec(bank).CountKey:x}", out var raw) && TryNumber(raw, out int count) ? count : null;
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
    /// <param name="client">The headset to save it on.</param>
    /// <param name="name">The preset's name.</param>
    /// <param name="bands">Its band gains, in tenths of a decibel.</param>
    /// <param name="bank">Which bank it belongs to.</param>
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

        // Only the name says whether to move off it: a band lost from the
        // reply must not leave the headset on a slot about to be emptied.
        if (NameIn(client.ReadCategory(spec.Category, TimeSpan.FromMilliseconds(1200)), spec) == name)
        {
            client.Set(spec.Select, fallback);
            Thread.Sleep(200);
        }
        client.Set(spec.DeleteKey, name);
    }
}
