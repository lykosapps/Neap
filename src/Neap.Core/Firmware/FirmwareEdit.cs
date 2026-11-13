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
    /// <summary>Whether bytes read at the address are ones this change accounts for: the factory's or the edit's.</summary>
    /// <remarks>Anything else is either another change to the firmware or a read gone wrong, so it is worth reading again before it is believed.</remarks>
    public bool Accounts(ReadOnlySpan<byte> read) => read.SequenceEqual([.. Factory]) || read.SequenceEqual([.. Active]);

    /// <summary>The change for a run of bytes, from their hex.</summary>
    public static FlashChange Of(int address, string factory, string active) =>
        new(address, Convert.FromHexString(factory), Convert.FromHexString(active));
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
/// <see cref="Plug"/> to <see cref="Write"/> run once for each session the
/// plan needs. After a write the program restarts the chip into the firmware,
/// with the cable still in, and the chip does not come back in download mode
/// by itself (SP2Tools F318, F319), so a further session, or putting a section
/// back, asks for the cable to be plugged in again. Once the last section reads
/// back, the person unplugs and the headset starts by itself.
/// </remarks>
public enum EditStep
{
    /// <summary>The headset is checked, as it is connected now or as it was last seen.</summary>
    Check,

    /// <summary>The person switches the headset off and plugs USB-C into the left ear cup, or plugs it in again.</summary>
    Plug,

    /// <summary>Neap reaches the headset's chip in download mode.</summary>
    Connect,

    /// <summary>Neap saves each section, and checks it holds the factory firmware and Neap's edits, nothing else.</summary>
    Backup,

    /// <summary>
    /// The chip writes the sections and restarts into the firmware, and Neap
    /// reads each back over the cable, whole, putting back what was there if it
    /// does not match.
    /// </summary>
    /// <remarks>
    /// One step to the person, as Neap hands the chip the sections in a moment
    /// and the chip does the writing on its own: nearly all of it is the wait
    /// for the restarted headset to answer on the cable (SP2Tools F321, F328).
    /// </remarks>
    Write,
}

/// <summary>What Neap knows of the headset before the edits are written.</summary>
/// <param name="Connected">Whether the headset is known: connected now, or seen with its firmware and battery a short while ago, so it can be switched off before applying.</param>
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
/// <param name="Sectors">The sections of flash it touches, by address, in the order they are written to put it on.</param>
/// <param name="Changes">The bytes it changes, in the order they are written to put it on.</param>
public sealed record FirmwareEdit(string Id, HeadsetModel Model, string FirmwareBuild, IReadOnlyList<int> Sectors, IReadOnlyList<FlashChange> Changes)
{
    /// <summary>Whether firmware edits are in this release of Neap on this system.</summary>
    /// <remarks>
    /// On Windows only: Neap's writer has written a real headset there, and
    /// has not been tried on Linux. Off, the Firmware page and its switch in
    /// Settings are nowhere to be found, whatever the settings file says; only
    /// a pretend run asked to open the page shows it, against the pretend
    /// headset.
    /// </remarks>
    public static bool Released { get; } = OperatingSystem.IsWindows();

    /// <summary>Whether firmware edits are offered for a headset: in a release that has them, for a headset with edits made for it.</summary>
    /// <remarks>Only the Stealth Pro II has any, so another headset, or none known yet, is offered nothing.</remarks>
    public static bool Offered(HeadsetModel? model) => Released && For(model).Count > 0;

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
    /// <remarks>
    /// The headset has two chips, and the one that answers the PC learns of a
    /// change only when it is sent one. The firmware sends noise cancellation
    /// turning on and off but never its level, so the new code sends the level
    /// itself after setting it, as the firmware sends the rest. Without that
    /// the PC is told 100 in every mode, and cannot tell transparency from
    /// noise cancellation.
    /// </remarks>
    public static FirmwareEdit NoiseCycle { get; } =
        new("noise_cycle", HeadsetModels.StealthProII, "4.107.703.0", [0x08349000, 0x08232000],
        [
            FlashChange.Of(0x08349000, new string('F', 158 * 2),
                "70B582B000240094019475206946E8F63DF860B3762001A9E8F638F838B3009B53B1019B322B03D264240125092606E0FF240025FF26" +
                "02E000240125FF26FF2C08D00194762001A9E8F6D0FD10B1204600F010F8009575206946E4F699FFFF2E02D03046DBF678FA012002B070BD002002B070BD" +
                "00B599B030237622CDE9033205904FF2A433C1F223431B68009303AA54230E212320D6F679F819B000BD"),
            FlashChange.Of(0x08232094, "FFF746F8", "16F1B4FF"),
        ]);

    /// <summary>The headset's light switches off when it loses its connection, rather than blinking: one byte (SP2Tools, live since 22 October).</summary>
    public static FirmwareEdit LightOffWhenDisconnected { get; } =
        new("light_off_disconnected", HeadsetModels.StealthProII, "4.107.703.0", [0x082F9000],
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

    /// <summary>How long a session's <see cref="EditStep.Write"/> usually takes, to the last section read back.</summary>
    /// <remarks>51 to 60 seconds on the owner's headset (SP2Tools F328).</remarks>
    public static readonly TimeSpan WriteUsually = TimeSpan.FromSeconds(60);

    /// <summary>How long into <see cref="EditStep.Write"/> it is taking longer than usual.</summary>
    /// <remarks>Half again the usual time, with half a minute left before Neap stops waiting for the read back.</remarks>
    public static readonly TimeSpan WriteLate = TimeSpan.FromSeconds(90);

    /// <summary>The share of the bar to fill this long into <see cref="EditStep.Write"/>, from 0 to 100.</summary>
    /// <remarks>
    /// Nothing reports how far the chip has got, so the bar keeps time with the
    /// usual wait and stops short of full; it fills only when the step is done.
    /// </remarks>
    public static int WriteShown(TimeSpan elapsed) =>
        elapsed <= TimeSpan.Zero ? 0 : (int)Math.Min(WriteShownMost, WriteShownMost * (elapsed / WriteUsually));

    /// <summary>The most of the bar filled while <see cref="EditStep.Write"/> is under way.</summary>
    public const int WriteShownMost = 90;

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

    /// <summary>The bytes at each place the edits change, from whole sections read, laid over what was known before.</summary>
    /// <param name="edits">The edits whose places are wanted.</param>
    /// <param name="sections">Whole sections as read, by address.</param>
    /// <param name="before">The bytes known before, or null; any place a section read covers is taken from the section.</param>
    /// <returns>Every place known, or null when none is.</returns>
    public static IReadOnlyDictionary<int, byte[]>? FlashFrom(IReadOnlyList<FirmwareEdit> edits,
        IReadOnlyDictionary<int, byte[]> sections, IReadOnlyDictionary<int, byte[]>? before)
    {
        var flash = before is null ? new Dictionary<int, byte[]>() : new Dictionary<int, byte[]>(before);
        foreach (var change in edits.SelectMany(edit => edit.Changes))
        {
            int sector = change.Address & ~(SectorSize - 1);
            int at = change.Address - sector;
            if (sections.TryGetValue(sector, out var bytes) && at + change.Factory.Count <= bytes.Length)
                flash[change.Address] = bytes[at..(at + change.Factory.Count)];
        }
        return flash.Count == 0 ? null : flash;
    }

    /// <summary>Whether a whole section holds this edit's changes in it, or the factory's bytes there.</summary>
    /// <param name="sector">The section, by address.</param>
    /// <param name="section">The section's bytes, such as a backup holds them.</param>
    /// <returns>True for the edit, false for the factory's, null for neither, or for a section the edit does not touch.</returns>
    public bool? HeldIn(int sector, byte[] section)
    {
        var here = Changes.Where(change => change.Address - sector is >= 0 and < SectorSize).ToList();
        if (here.Count == 0 || section.Length != SectorSize) return null;
        bool Holds(FlashChange change, IReadOnlyList<byte> bytes) =>
            section.AsSpan(change.Address - sector, bytes.Count).SequenceEqual([.. bytes]);
        if (here.All(change => Holds(change, change.Active))) return true;
        return here.All(change => Holds(change, change.Factory)) ? false : null;
    }

    /// <summary>The edit that sections, as they would be after one is put back, leave half on, or null when they leave none.</summary>
    /// <param name="sections">Whole sections by address: the one to be put back as its backup holds it, and any other an edit shares with it as the headset holds it.</param>
    /// <param name="flash">The bytes known at each place the edits change, such as the last read, which the sections are laid over; or null.</param>
    /// <remarks>
    /// An edit is half on when its last change, the one that turns it on, is in
    /// place and an earlier one is not: a call to code that is not there, which
    /// is how the headset was stopped from starting on 15 October. The other
    /// way round, code that nothing calls, is harmless, and is what a firmware
    /// update leaves. Putting back an edit's sections one at a time is safe in
    /// one order and not the other, whichever way it goes, so the one refused
    /// is always the one to do second.
    /// </remarks>
    public static FirmwareEdit? LeftHalfOn(IReadOnlyDictionary<int, byte[]> sections, IReadOnlyDictionary<int, byte[]>? flash = null)
    {
        if (FlashFrom(All, sections, flash) is not { } after) return null;
        bool On(FlashChange change) => after[change.Address].AsSpan().SequenceEqual([.. change.Active]);
        return All.FirstOrDefault(edit => edit.Changes.All(change => after.ContainsKey(change.Address))
            && On(edit.Changes[^1]) && !edit.Changes.All(On));
    }

    /// <summary>A section as it is to be written: what the headset holds, with every edit taken out and the ticked ones put in.</summary>
    /// <param name="sector">The section, by address.</param>
    /// <param name="held">The section as read from the headset.</param>
    /// <param name="wanted">The edits ticked.</param>
    /// <param name="factory">The SHA-256 of each factory section, as <see cref="FactorySections"/> holds them for a headset.</param>
    /// <returns>
    /// The section to write, or null where the section with every edit taken out
    /// is not the factory one, so writing it would keep something Neap does not know.
    /// </returns>
    public static byte[]? Compose(int sector, ReadOnlySpan<byte> held, IReadOnlySet<FirmwareEdit> wanted, IReadOnlyDictionary<int, string> factory)
    {
        if (held.Length != SectorSize || !factory.TryGetValue(sector, out string? factoryHash)) return null;
        byte[] section = held.ToArray();
        var inSector = All.SelectMany(edit => edit.Changes.Select(change => (edit, change)))
            .Where(pair => pair.change.Address - sector is >= 0 and < SectorSize)
            .ToList();
        foreach (var (_, change) in inSector) Put(section, sector, change.Address, change.Factory);
        if (!Convert.ToHexString(SHA256.HashData(section)).Equals(factoryHash, StringComparison.Ordinal)) return null;
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

    /// <summary>The sections written in each session, in order.</summary>
    public static IReadOnlyList<IReadOnlyList<int>> Sessions(EditPlan plan) =>
        plan.Sectors.Chunk(SectorsPerSession).Select(chunk => (IReadOnlyList<int>)chunk).ToList();

    /// <summary>Whether to offer reading the headset's flash again: an edit's state is not known while the headset is connected.</summary>
    /// <remarks>
    /// A read through the dock can fail now and then, and the page reads again
    /// only as it opens or the headset connects. A headset that is not connected
    /// cannot be read, and the page says to switch it on instead.
    /// </remarks>
    public static bool OffersCheckAgain(IEnumerable<EditStatus> statuses, bool connected) =>
        connected && statuses.Contains(EditStatus.Unknown);

    /// <summary>Whether Swarm II is among the programs running, by process name.</summary>
    /// <remarks>
    /// Swarm II installs as several programs, each named "Turtle Beach ...":
    /// the app, its device service, a session monitor, an updater and a media
    /// control. Which of them talk to the headset is not known, so any of them
    /// counts.
    /// </remarks>
    public static bool SwarmIn(IEnumerable<string> programs) =>
        programs.Any(program => program.StartsWith(SwarmPrefix, StringComparison.OrdinalIgnoreCase));

    private const string SwarmPrefix = "Turtle Beach";

    /// <summary>Everything that stops the edits being written now, or none.</summary>
    /// <remarks>
    /// In download mode the battery cannot be read, so it is the level last seen.
    /// A headset never seen is the only thing said, since nothing else about it
    /// is known.
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
