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

/// <summary>Something that stops an edit being installed or removed now.</summary>
public enum EditBlocker
{
    NotConnected,
    DoesNotFit,
    LowBattery,
    SwarmRunning,
}

/// <summary>One step of installing or removing an edit, in the order they run.</summary>
public enum EditStep
{
    Check,
    Backup,
    Write,
    ReadBack,
    Restart,
    Confirm,
}

/// <summary>What Neap knows of the headset when an edit is about to be written.</summary>
/// <param name="Connected">Whether the headset's settings can be reached.</param>
/// <param name="FirmwareBuild">The firmware version the headset reports, or null before it has.</param>
/// <param name="Battery">The headset's battery level, or null before it has reported one.</param>
/// <param name="SwarmRunning">Whether Swarm II is running, and could talk to the headset during the write.</param>
public readonly record struct EditReadiness(bool Connected, string? FirmwareBuild, int? Battery, bool SwarmRunning);

/// <summary>
/// A firmware edit Neap knows of: the headset and firmware build it is made
/// for, and its bit in the mark the edits leave on the headset.
/// </summary>
/// <remarks>
/// <para>
/// An edit is made byte for byte against one firmware build, so it fits only
/// that build. Anything else, including a build Swarm II updated the headset
/// to, is <see cref="EditStatus.DoesNotFit"/> and is never written.
/// </para>
/// <para>
/// The edits leave a number on the headset with one bit per edit, so Neap can
/// tell which are on. Until a headset reports that mark, whether an edit is
/// on is <see cref="EditStatus.Unknown"/>, never a guess.
/// </para>
/// <para>
/// Everything written is backed up first. A step that fails once the write
/// has begun is undone from that backup, so a headset is left either as it
/// was or with the edit fully on and confirmed.
/// </para>
/// </remarks>
/// <param name="Id">The edit's name in the app log and recordings.</param>
/// <param name="Model">The headset the edit is made for.</param>
/// <param name="FirmwareBuild">The firmware version the edit is made against.</param>
/// <param name="Bit">The edit's bit in the mark.</param>
public sealed record FirmwareEdit(string Id, HeadsetModel Model, string FirmwareBuild, int Bit)
{
    /// <summary>The Mode button steps through off, transparency and noise cancellation by itself (SP2Tools F090).</summary>
    public static FirmwareEdit NoiseCycle { get; } = new("noise_cycle", HeadsetModels.StealthProII, "4.107.703.0", 0);

    /// <summary>Gets every edit Neap knows of.</summary>
    public static IReadOnlyList<FirmwareEdit> All { get; } = [NoiseCycle];

    /// <summary>The lowest battery level an edit is written at, so the headset cannot switch off part way.</summary>
    public const int MinimumBattery = 50;

    /// <summary>Gets the steps of an install or a removal, in order.</summary>
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

    /// <summary>Everything that stops this edit being written now, or none.</summary>
    /// <remarks>A headset that cannot be reached is the only thing said, since nothing else about it is known.</remarks>
    public IReadOnlyList<EditBlocker> Blockers(EditReadiness readiness)
    {
        if (!readiness.Connected) return [EditBlocker.NotConnected];
        var blockers = new List<EditBlocker>();
        if (readiness.FirmwareBuild != FirmwareBuild) blockers.Add(EditBlocker.DoesNotFit);
        if (readiness.Battery is not >= MinimumBattery) blockers.Add(EditBlocker.LowBattery);
        if (readiness.SwarmRunning) blockers.Add(EditBlocker.SwarmRunning);
        return blockers;
    }

    /// <summary>Whether a failure at this step leaves anything on the headset to undo from the backup.</summary>
    public static bool UndoAfter(EditStep failed) => failed >= EditStep.Write;
}
