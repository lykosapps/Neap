using System.Globalization;

namespace Neap.Core.Firmware;

/// <summary>Whether a backup can be put back on the headset as it is now.</summary>
public enum BackupFit
{
    /// <summary>It is a whole section of this headset's firmware, holding the factory firmware and Neap's edits only.</summary>
    Fits,

    /// <summary>The file is not one Neap saved: its name or its size is not a backup's.</summary>
    NotABackup,

    /// <summary>The headset has not reported its firmware, so there is nothing to match the backup against.</summary>
    NoHeadset,

    /// <summary>It was taken from other firmware than the headset has now, such as before a Swarm II update.</summary>
    OtherFirmware,

    /// <summary>It holds something other than the factory firmware and Neap's edits.</summary>
    Unknown,
}

/// <summary>A section of firmware saved before it was written.</summary>
/// <remarks>
/// Every backup stays on disk, and the page lists only the newest of each
/// section (<see cref="NewestOfEach"/>): restoring it undoes the last change
/// to that section, and older ones are a click away in the folder.
/// </remarks>
/// <param name="File">Where it is.</param>
/// <param name="Taken">When it was made.</param>
/// <param name="Sector">The section, by address.</param>
/// <param name="FirmwareBuild">The firmware it was taken from.</param>
public sealed record FirmwareBackup(string File, DateTime Taken, int Sector, string FirmwareBuild)
{
    private const string Stamp = "yyyy-MM-dd HH.mm.ss";

    /// <summary>The file name a backup is saved under, which says what it holds.</summary>
    public static string FileName(DateTime taken, int sector, string build) =>
        string.Create(CultureInfo.InvariantCulture, $"{taken.ToString(Stamp, CultureInfo.InvariantCulture)} {sector:X8} {build}.bin");

    /// <summary>The backup a file holds, or null for a file not named by <see cref="FileName"/>.</summary>
    public static FirmwareBackup? Of(string file)
    {
        var parts = Path.GetFileNameWithoutExtension(file).Split(' ');
        return parts.Length == 4
            && DateTime.TryParseExact($"{parts[0]} {parts[1]}", Stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var taken)
            && int.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int sector)
            ? new FirmwareBackup(file, taken, sector, parts[3])
            : null;
    }

    /// <summary>Whether a backup can be put back on a headset with this firmware.</summary>
    /// <param name="named">The backup its file name says it is, or null for a file not named as Neap names them.</param>
    /// <param name="bytes">What the file holds.</param>
    /// <param name="headsetBuild">The firmware the headset reports, or null before it has.</param>
    /// <param name="factory">The SHA-256 of each factory section, as <see cref="FirmwareEdit.FactorySections"/> holds them.</param>
    /// <remarks>
    /// A section of other firmware, put back, would mix two versions of the
    /// firmware. A section with anything in it but the factory firmware and
    /// Neap's edits would write what Neap does not know, which is how a file
    /// from anywhere else is kept off the headset.
    /// </remarks>
    public static BackupFit Judge(FirmwareBackup? named, ReadOnlySpan<byte> bytes, string? headsetBuild, IReadOnlyDictionary<int, string> factory)
    {
        if (named is null || bytes.Length != FirmwareEdit.SectorSize) return BackupFit.NotABackup;
        if (headsetBuild is null) return BackupFit.NoHeadset;
        if (named.FirmwareBuild != headsetBuild) return BackupFit.OtherFirmware;
        return FirmwareEdit.Compose(named.Sector, bytes, new HashSet<FirmwareEdit>(), factory) is null ? BackupFit.Unknown : BackupFit.Fits;
    }

    /// <summary>The newest backup of each section and firmware, newest first.</summary>
    /// <remarks>
    /// A backup taken from other firmware is its own entry, as it does not fit
    /// the firmware a later one was taken from.
    /// </remarks>
    public static IReadOnlyList<FirmwareBackup> NewestOfEach(IEnumerable<FirmwareBackup> backups) =>
        backups.GroupBy(backup => (backup.Sector, backup.FirmwareBuild))
            .Select(group => group.MaxBy(backup => backup.Taken)!)
            .OrderByDescending(backup => backup.Taken)
            .ThenBy(backup => backup.Sector)
            .ToList();

    /// <summary>The newest backup of a section taken from this firmware, or null when there is none.</summary>
    public static FirmwareBackup? NewestOf(IEnumerable<FirmwareBackup> backups, int sector, string build) =>
        backups.Where(backup => backup.Sector == sector && backup.FirmwareBuild == build).MaxBy(backup => backup.Taken);
}
