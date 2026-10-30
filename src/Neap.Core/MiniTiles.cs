namespace Neap.Core;

/// <summary>A control the mini panel can show.</summary>
public enum MiniTile
{
    /// <summary>The microphone, on or off.</summary>
    Microphone,

    /// <summary>Noise cancellation, off, cancelling or transparency.</summary>
    NoiseCancellation,

    /// <summary>The system's spatial sound for the headset.</summary>
    SpatialSound,

    /// <summary>The game equaliser's preset.</summary>
    EqualiserPreset,

    /// <summary>Superhuman Hearing, on or off, with its sound.</summary>
    SuperhumanHearing,

    /// <summary>The profile in use, and the way to switch.</summary>
    Profile,
}

/// <summary>Decides which of the mini panel's controls are shown, and how the tiles are set out.</summary>
/// <remarks>
/// <para>
/// The person chooses which controls the panel shows, in Settings. The choice
/// is kept by name, so a name no longer known, from a later version, is left
/// out rather than failing, and a choice never made shows what the panel
/// showed before it could be chosen: everything except Superhuman Hearing,
/// which is new, so nobody finds it added, and the profile only once one is
/// saved, as there is nothing to switch to before.
/// </para>
/// <para>
/// A tile's own headset decides whether it can be shown at all, such as
/// noise cancellation on a headset without it; this only says whether the
/// person wants it.
/// </para>
/// </remarks>
public static class MiniTiles
{
    /// <summary>The most tiles that share a row.</summary>
    private const int MostAcross = 3;

    /// <summary>What the panel shows until a choice is made.</summary>
    public static IReadOnlySet<MiniTile> Default { get; } =
        new HashSet<MiniTile> { MiniTile.Microphone, MiniTile.NoiseCancellation, MiniTile.SpatialSound, MiniTile.EqualiserPreset };

    /// <summary>The name a tile is kept under.</summary>
    public static string NameOf(MiniTile tile) => tile switch
    {
        MiniTile.Microphone => "microphone",
        MiniTile.NoiseCancellation => "noise",
        MiniTile.SpatialSound => "spatial",
        MiniTile.EqualiserPreset => "equaliser",
        MiniTile.Profile => "profile",
        _ => "hearing",
    };

    /// <summary>The tiles a saved choice names, or <see cref="Default"/> when none was saved.</summary>
    /// <param name="saved">The names kept, or null when the person has never chosen.</param>
    /// <param name="hasProfiles">Whether a profile is saved, which adds the profile to the panel before a choice is made.</param>
    public static IReadOnlySet<MiniTile> Chosen(IEnumerable<string>? saved, bool hasProfiles = false) =>
        saved is not null
            ? Enum.GetValues<MiniTile>().Where(tile => saved.Contains(NameOf(tile))).ToHashSet()
            : hasProfiles ? Default.Append(MiniTile.Profile).ToHashSet() : Default;

    /// <summary>The names to keep for a choice, in the order the tiles are shown.</summary>
    public static IReadOnlyList<string> ToSave(IEnumerable<MiniTile> chosen) =>
        Enum.GetValues<MiniTile>().Where(chosen.Contains).Select(NameOf).ToList();

    /// <summary>How many tiles go side by side in the panel's grid of <paramref name="shown"/> tiles.</summary>
    /// <remarks>Three share a row, as the panel was set out before the choice; a fourth would make each too narrow to read, so four are two rows of two.</remarks>
    public static int Columns(int shown) => shown > MostAcross ? 2 : Math.Max(1, shown);
}
