namespace Neap.Core.Firmware;

/// <summary>Whether a firmware edit is on the headset.</summary>
public enum EditStatus
{
    /// <summary>The headset's mark says the edit is on.</summary>
    Installed,

    /// <summary>The headset's mark says the edit is not on.</summary>
    NotInstalled,

    /// <summary>The headset leaves no mark Neap can read, or has not reported its firmware.</summary>
    Unknown,

    /// <summary>The headset's firmware is not the build the edit is made for.</summary>
    DoesNotFit,
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

    /// <summary>The headset's mark is read to see the edits took.</summary>
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
/// A firmware edit Neap knows of: what it changes, the headset and firmware
/// build it is made for, and its bit in the mark the edits leave.
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
/// The sections each edit touches come from SP2Tools' findings. The bytes
/// written come from SP2Tools' list of edits when the real writer is built.
/// </para>
/// </remarks>
/// <param name="Id">The edit's name in the app log and recordings.</param>
/// <param name="Model">The headset the edit is made for.</param>
/// <param name="FirmwareBuild">The firmware version the edit is made against.</param>
/// <param name="Bit">The edit's bit in the mark.</param>
/// <param name="Risk">How much it changes.</param>
/// <param name="Sectors">The sections of flash it touches, by address, in the order they are written to put it on.</param>
public sealed record FirmwareEdit(string Id, HeadsetModel Model, string FirmwareBuild, int Bit, EditRisk Risk, IReadOnlyList<int> Sectors)
{
    /// <summary>Whether firmware edits are in this release of Neap at all.</summary>
    /// <remarks>
    /// Off until Neap has its own writer for a real headset. While it is off
    /// the Firmware page and its switch in Settings are nowhere to be found,
    /// whatever the settings file says; only a pretend run asked to open the
    /// page shows it, against the pretend headset.
    /// </remarks>
    public const bool Released = false;

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
        new("noise_cycle", HeadsetModels.StealthProII, "4.107.703.0", 0, EditRisk.Higher, [0x08349000, 0x08232000]);

    /// <summary>The headset's light switches off when it loses its connection, rather than blinking: one byte (SP2Tools, live since 22 October).</summary>
    public static FirmwareEdit LightOffWhenDisconnected { get; } =
        new("light_off_disconnected", HeadsetModels.StealthProII, "4.107.703.0", 1, EditRisk.Low, [0x082F9000]);

    /// <summary>Gets every edit Neap knows of.</summary>
    public static IReadOnlyList<FirmwareEdit> All { get; } = [NoiseCycle, LightOffWhenDisconnected];

    /// <summary>Gets the steps of applying, in order; see <see cref="EditStep"/>.</summary>
    public static IReadOnlyList<EditStep> Steps { get; } = Enum.GetValues<EditStep>();

    /// <summary>The edits made for a headset, or none for an unknown one.</summary>
    public static IReadOnlyList<FirmwareEdit> For(HeadsetModel? model) =>
        model is null ? [] : All.Where(edit => edit.Model == model).ToList();

    /// <summary>Whether this edit is on a headset with this firmware and mark.</summary>
    /// <param name="firmwareBuild">The firmware version the headset reports, or null before it has.</param>
    /// <param name="mark">The mark the headset reports, or null where it leaves none.</param>
    public EditStatus StatusOn(string? firmwareBuild, int? mark) => (firmwareBuild, mark) switch
    {
        (null, _) => EditStatus.Unknown,
        _ when firmwareBuild != FirmwareBuild => EditStatus.DoesNotFit,
        (_, null) => EditStatus.Unknown,
        (_, int bits) => (bits >> Bit & 1) == 1 ? EditStatus.Installed : EditStatus.NotInstalled,
    };

    /// <summary>The edits a mark says are on, or null where there is no mark.</summary>
    public static IReadOnlySet<FirmwareEdit>? Installed(IReadOnlyList<FirmwareEdit> edits, int? mark) =>
        mark is int bits ? edits.Where(edit => (bits >> edit.Bit & 1) == 1).ToHashSet() : null;

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
        wanted.Where(edit => edit.Sectors.Contains(sector)).OrderBy(edit => edit.Bit).ToList();

    /// <summary>The sections written in each session, in order.</summary>
    public static IReadOnlyList<IReadOnlyList<int>> Sessions(EditPlan plan) =>
        plan.Sectors.Chunk(SectorsPerSession).Select(chunk => (IReadOnlyList<int>)chunk).ToList();

    /// <summary>The mark the headset should report once the wanted edits are on.</summary>
    public static int MarkOf(IEnumerable<FirmwareEdit> wanted) => wanted.Aggregate(0, (mark, edit) => mark | 1 << edit.Bit);

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
