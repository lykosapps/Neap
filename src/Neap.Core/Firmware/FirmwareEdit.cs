using System.Security.Cryptography;

namespace Neap.Core.Firmware;

/// <summary>Whether a firmware edit is on the headset.</summary>
public enum EditStatus
{
    /// <summary>The flash holds the edit.</summary>
    Installed,

    /// <summary>The flash is as the factory made it, where the edit would change it.</summary>
    NotInstalled,

    /// <summary>The flash could not be read, or the headset has not reported its firmware.</summary>
    Unknown,

    /// <summary>The headset's firmware is not the build the edit is made for.</summary>
    DoesNotFit,

    /// <summary>The flash holds neither the factory bytes nor the edit's at the places it changes.</summary>
    Altered,
}

/// <summary>One run of bytes an edit changes, with what the factory firmware and the edit put there.</summary>
/// <param name="Address">Where the run starts in flash.</param>
/// <param name="Factory">The bytes the factory firmware has there.</param>
/// <param name="Active">The bytes the edit puts there.</param>
public sealed record FlashChange(int Address, IReadOnlyList<byte> Factory, IReadOnlyList<byte> Active)
{
    /// <summary>The change for a run of bytes, from their hex.</summary>
    public static FlashChange Of(int address, string factory, string active) =>
        new(address, Convert.FromHexString(factory), Convert.FromHexString(active));
}

/// <summary>How much an edit changes, for the person to weigh before ticking it.</summary>
public enum EditRisk
{
    /// <summary>A setting's value in the firmware's data.</summary>
    Low,

    /// <summary>New code the firmware runs.</summary>
    Higher,
}

/// <summary>Something that stops the edits being written now.</summary>
public enum EditBlocker
{
    NotConnected,
    DoesNotFit,
    LowBattery,
    SwarmRunning,
}

/// <summary>One step of writing the edits, in the order they run.</summary>
/// <remarks>
/// From <see cref="Plug"/> to <see cref="Reconnect"/> is one session in the
/// headset's download mode, and runs once for each session the plan needs.
/// </remarks>
public enum EditStep
{
    /// <summary>The headset is checked while it is still connected as usual.</summary>
    Check,

    /// <summary>The person switches the headset off and plugs USB-C into the left ear cup.</summary>
    Plug,

    /// <summary>Neap reaches the headset in download mode and reads which firmware is on the chip.</summary>
    Connect,

    Backup,

    Write,

    /// <summary>The headset reads back what it wrote, and puts stock back itself if it does not match.</summary>
    Verify,

    /// <summary>The person unplugs the cable and switches the headset on.</summary>
    Reconnect,

    /// <summary>The flash is read to see the edits took.</summary>
    Confirm,
}

/// <summary>What Neap knows of the headset before the edits are written.</summary>
/// <param name="Connected">Whether the headset's settings can be reached.</param>
/// <param name="FirmwareBuild">The firmware version the headset reports, or null before it has.</param>
/// <param name="Battery">The headset's battery level, or null before it has reported one.</param>
/// <param name="SwarmRunning">Whether Swarm II is running, and could talk to the headset during the write.</param>
public readonly record struct EditReadiness(bool Connected, string? FirmwareBuild, int? Battery, bool SwarmRunning);

/// <summary>What applying the edits ticked would write.</summary>
/// <param name="Changing">The edits that go on or come off.</param>
/// <param name="Sectors">The sections of flash written, by address, in the order they are written.</param>
/// <param name="Sessions">How many times the headset is plugged in in download mode.</param>
/// <param name="UpToSectors">Whether the headset's edits are not known, so every section an edit touches is read and only those that differ are written.</param>
public sealed record EditPlan(IReadOnlyList<FirmwareEdit> Changing, IReadOnlyList<int> Sectors, int Sessions, bool UpToSectors)
{
    /// <summary>Gets whether nothing would be written.</summary>
    public bool Nothing => Sectors.Count == 0;
}

/// <summary>
/// A firmware edit Neap knows of: what it changes and the headset and
/// firmware build it is made for.
/// </summary>
/// <remarks>
/// <para>
/// An edit is made byte for byte against one firmware build, so it fits only
/// that build. Anything else, including a build Swarm II updated the headset
/// to, is <see cref="EditStatus.DoesNotFit"/>, and the firmware on the chip is
/// read again before anything is written.
/// </para>
/// <para>
/// The firmware is written a section of flash at a time, and two edits can
/// share a section. So the person ticks the edits they want and Neap works out
/// the sections: each section any changing edit touches is written whole, as
/// stock with every ticked edit in it. One edit never undoes another, nothing
/// ticked is written twice, and unticking every edit is restoring stock.
/// </para>
/// <para>
/// An edit's sections are listed in the order they are written to put it on,
/// and are written the other way round to take it off. For an edit that adds
/// code and the call that reaches it, that is the code first and the call
/// last, and the call put back first: stopped at any point, the headset is
/// left with nothing that jumps to code that is not there, which is how the
/// headset was stopped from starting on 15 October.
/// </para>
/// <para>
/// The sections and bytes come from SP2Tools' findings and were read back
/// from a headset carrying the edits. Neap holds only the bytes an edit
/// changes and a hash of each factory section, never the firmware itself: a
/// section is written as what the headset holds, checked against that hash
/// with every edit taken out, with the ticked edits put in.
/// </para>
/// <para>
/// Whether an edit is on is read live from the flash at the places it
/// changes. Its last change is the one that turns it on, such as the call
/// that reaches new code, so with that one at factory the edit is off even
/// where earlier changes remain, as a firmware update leaves new code that
/// nothing calls.
/// </para>
/// </remarks>
/// <param name="Id">The edit's name in the app log and recordings.</param>
/// <param name="Model">The headset the edit is made for.</param>
/// <param name="FirmwareBuild">The firmware version the edit is made against.</param>
/// <param name="Risk">How much it changes.</param>
/// <param name="Sectors">The sections of flash it touches, by address, in the order they are written to put it on.</param>
/// <param name="Changes">The bytes it changes, in the order they are written to put it on.</param>
public sealed record FirmwareEdit(string Id, HeadsetModel Model, string FirmwareBuild, EditRisk Risk, IReadOnlyList<int> Sectors, IReadOnlyList<FlashChange> Changes)
{
    /// <summary>Whether firmware edits are in this release of Neap at all.</summary>
    /// <remarks>
    /// Off until Neap has its own writer for a real headset. While it is off
    /// the Firmware page and its switch in Settings are nowhere to be found,
    /// whatever the settings file says; only a pretend run asked to open the
    /// page shows it, against the pretend headset.
    /// </remarks>
    public const bool Released = false;

    /// <summary>Whether the parametric equaliser is in this release of Neap.</summary>
    /// <remarks>
    /// On for a headset whose bands can be set live; see
    /// <see cref="HeadsetModel.HasParametricBands"/>. Off, only a pretend run
    /// shows the equaliser's Parametric choice. It is turned on from the
    /// Equaliser page; it writes a stored setting, not the firmware.
    /// </remarks>
    public const bool ParametricReleased = true;

    /// <summary>The size of a section of flash, the least that is written at once.</summary>
    public const int SectorSize = 0x1000;

    /// <summary>The most sections written in one session in download mode.</summary>
    public const int SectorsPerSession = 2;

    /// <summary>The lowest battery level the edits are written at, so the headset cannot switch off part way.</summary>
    public const int MinimumBattery = 50;

    /// <summary>
    /// The Mode button steps through off, transparency and noise cancellation
    /// by itself, with a tone for each: new code in empty flash at 0x08349000,
    /// then the call at 0x08232094 that reaches it (SP2Tools F090).
    /// </summary>
    public static FirmwareEdit NoiseCycle { get; } =
        new("noise_cycle", HeadsetModels.StealthProII, "4.107.703.0", EditRisk.Higher, [0x08349000, 0x08232000],
        [
            FlashChange.Of(0x08349000, new string('F', 108 * 2),
                "70B582B000240094019475206946E8F63DF840B3762001A9E8F638F818B3009B53B1019B322B03D264240125092606E0FF240025FF26" +
                "02E000240125FF26FF2C04D00194762001A9E8F6D0FD009575206946E4F69DFFFF2E02D03046DBF67CFA012002B070BD002002B070BD"),
            FlashChange.Of(0x08232094, "FFF746F8", "16F1B4FF"),
        ]);

    /// <summary>The headset's light switches off when it loses its connection, rather than blinking: one byte (SP2Tools, live since 22 October).</summary>
    public static FirmwareEdit LightOffWhenDisconnected { get; } =
        new("light_off_disconnected", HeadsetModels.StealthProII, "4.107.703.0", EditRisk.Low, [0x082F9000],
        [FlashChange.Of(0x082F9B94, "02", "00")]);

    /// <summary>Gets every edit Neap knows of.</summary>
    public static IReadOnlyList<FirmwareEdit> All { get; } = [NoiseCycle, LightOffWhenDisconnected];

    /// <summary>The SHA-256 of each section an edit touches, as the factory firmware holds it.</summary>
    /// <remarks>
    /// From the factory 4.107.703.0 image, checked against a headset read before
    /// any edit; the section of new code is empty flash in the factory firmware.
    /// </remarks>
    public static IReadOnlyDictionary<int, string> FactorySections { get; } = new Dictionary<int, string>
    {
        [0x08232000] = "AC4290E1BC232049B8548650C786BE969338990CAF4C2203D4B80631CA3C9118",
        [0x082F9000] = "9CA08519E75F4CCA8F10B05013CAF12BE54DB9107950698F7CFCA47CF8A5D334",
        [0x08349000] = "F47A8EC3E9AFF2318D896942282AD4FE37D6391C82914F54A5DA8A37DE1300C6",
    };

    /// <summary>Gets the steps of applying, in order; see <see cref="EditStep"/>.</summary>
    public static IReadOnlyList<EditStep> Steps { get; } = Enum.GetValues<EditStep>();

    /// <summary>The edits made for a headset, or none for an unknown one.</summary>
    public static IReadOnlyList<FirmwareEdit> For(HeadsetModel? model) =>
        model is null ? [] : All.Where(edit => edit.Model == model).ToList();

    /// <summary>Whether this edit is on, from the flash read at the places it changes.</summary>
    /// <param name="firmwareBuild">The firmware version the headset reports, or null before it has.</param>
    /// <param name="flash">The bytes read at each change's address, or null where the flash could not be read.</param>
    public EditStatus StatusIn(string? firmwareBuild, IReadOnlyDictionary<int, byte[]>? flash)
    {
        if (firmwareBuild is null) return EditStatus.Unknown;
        if (firmwareBuild != FirmwareBuild) return EditStatus.DoesNotFit;
        if (flash is null || Changes.Any(change => !flash.ContainsKey(change.Address))) return EditStatus.Unknown;
        bool Holds(FlashChange change, IReadOnlyList<byte> bytes) => flash[change.Address].SequenceEqual(bytes);
        if (Changes.All(change => Holds(change, change.Active))) return EditStatus.Installed;
        var last = Changes[^1];
        return Holds(last, last.Factory) && Changes.All(change => Holds(change, change.Factory) || Holds(change, change.Active))
            ? EditStatus.NotInstalled
            : EditStatus.Altered;
    }

    /// <summary>The edits on, from the flash, or null where any edit's state is not clear.</summary>
    public static IReadOnlySet<FirmwareEdit>? InstalledIn(IReadOnlyList<FirmwareEdit> edits, string? firmwareBuild, IReadOnlyDictionary<int, byte[]>? flash)
    {
        var statuses = edits.ToDictionary(edit => edit, edit => edit.StatusIn(firmwareBuild, flash));
        return statuses.Values.All(status => status is EditStatus.Installed or EditStatus.NotInstalled)
            ? statuses.Where(pair => pair.Value == EditStatus.Installed).Select(pair => pair.Key).ToHashSet()
            : null;
    }

    /// <summary>A section as it is to be written: what the headset holds, with every edit taken out and the ticked ones put in.</summary>
    /// <param name="sector">The section, by address.</param>
    /// <param name="held">The section as read from the headset.</param>
    /// <param name="wanted">The edits ticked.</param>
    /// <returns>
    /// The section to write, or null where the section with every edit taken out
    /// is not the factory one, so writing it would keep something Neap does not know.
    /// </returns>
    public static byte[]? Compose(int sector, ReadOnlySpan<byte> held, IReadOnlySet<FirmwareEdit> wanted)
    {
        if (held.Length != SectorSize || !FactorySections.TryGetValue(sector, out string? factory)) return null;
        byte[] section = held.ToArray();
        var inSector = All.SelectMany(edit => edit.Changes.Select(change => (edit, change)))
            .Where(pair => pair.change.Address - sector is >= 0 and < SectorSize)
            .ToList();
        foreach (var (_, change) in inSector) Put(section, sector, change.Address, change.Factory);
        if (!Convert.ToHexString(SHA256.HashData(section)).Equals(factory, StringComparison.Ordinal)) return null;
        foreach (var (edit, change) in inSector.Where(pair => wanted.Contains(pair.edit))) Put(section, sector, change.Address, change.Active);
        return section;
    }

    private static void Put(byte[] section, int sector, int address, IReadOnlyList<byte> bytes)
    {
        for (int i = 0; i < bytes.Count; i++) section[address - sector + i] = bytes[i];
    }

    /// <summary>What applying would write, to have exactly the wanted edits on.</summary>
    /// <param name="edits">The edits made for the headset.</param>
    /// <param name="wanted">The edits ticked.</param>
    /// <param name="installed">The edits on now, or null where that is not known.</param>
    public static EditPlan Plan(IReadOnlyList<FirmwareEdit> edits, IReadOnlySet<FirmwareEdit> wanted, IReadOnlySet<FirmwareEdit>? installed)
    {
        var changing = installed is null
            ? edits.ToList()
            : edits.Where(edit => wanted.Contains(edit) != installed.Contains(edit)).ToList();
        // Edits coming off first, each in reverse; then edits going on, each in order.
        var sectors = changing.Where(edit => !wanted.Contains(edit)).SelectMany(edit => edit.Sectors.Reverse())
            .Concat(changing.Where(wanted.Contains).SelectMany(edit => edit.Sectors))
            .Distinct()
            .ToList();
        int sessions = (sectors.Count + SectorsPerSession - 1) / SectorsPerSession;
        return new EditPlan(changing, sectors, sessions, UpToSectors: installed is null);
    }

    /// <summary>The ticked edits that a section holds once it is written.</summary>
    /// <remarks>Everything else in the section is stock.</remarks>
    public static IReadOnlyList<FirmwareEdit> In(int sector, IReadOnlySet<FirmwareEdit> wanted) =>
        All.Where(edit => wanted.Contains(edit) && edit.Sectors.Contains(sector)).ToList();

    /// <summary>The sections written in each session, in order.</summary>
    public static IReadOnlyList<IReadOnlyList<int>> Sessions(EditPlan plan) =>
        plan.Sectors.Chunk(SectorsPerSession).Select(chunk => (IReadOnlyList<int>)chunk).ToList();

    /// <summary>Everything that stops the edits being written now, or none.</summary>
    /// <remarks>
    /// Asked while the headset is still connected as usual: in download mode its
    /// battery cannot be read. A headset that cannot be reached is the only thing
    /// said, since nothing else about it is known.
    /// </remarks>
    public static IReadOnlyList<EditBlocker> Blockers(IReadOnlyList<FirmwareEdit> changing, EditReadiness readiness)
    {
        if (!readiness.Connected) return [EditBlocker.NotConnected];
        var blockers = new List<EditBlocker>();
        if (changing.Any(edit => edit.FirmwareBuild != readiness.FirmwareBuild)) blockers.Add(EditBlocker.DoesNotFit);
        if (readiness.Battery is not >= MinimumBattery) blockers.Add(EditBlocker.LowBattery);
        if (readiness.SwarmRunning) blockers.Add(EditBlocker.SwarmRunning);
        return blockers;
    }
}
