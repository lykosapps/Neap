namespace Neap.Core.Audio;

/// <summary>A spatial sound format Windows can run on headphones.</summary>
public enum SpatialFormat
{
    Off,
    WindowsSonic,
    DolbyAtmos,
}

/// <summary>What Windows answered when asked to change the spatial format.</summary>
/// <remarks>The same cases as Windows' SetDefaultSpatialAudioFormatStatus, which Core cannot see.</remarks>
public enum SpatialResult
{
    Succeeded,
    AccessDenied,
    LicenseExpired,
    LicenseNotValidForAudioEndpoint,
    NotSupportedOnAudioEndpoint,
    UnknownError,
}

/// <summary>What to tell the person after a change of spatial format.</summary>
public enum SpatialNote
{
    None,

    /// <summary>The format's licence is missing or expired; its own app activates it.</summary>
    NeedsLicence,

    /// <summary>Windows would not change it, for a reason nothing here can fix.</summary>
    Refused,
}

/// <summary>Which spatial formats to offer for the headset, and what a change's result means.</summary>
/// <remarks>
/// <para>
/// Only Dolby Atmos for headphones is offered, not the speaker or home
/// theatre renderings Windows also lists under the same name for
/// hardware nobody is wearing. DTS is left out entirely: Windows would
/// offer it the same way, but nobody has confirmed it does anything on
/// this headset.
/// </para>
/// <para>
/// Windows says whether a format is installed, not whether it is licensed:
/// Dolby Atmos is offered as soon as Dolby Access is installed, and a missing
/// licence shows only when it is chosen. So a licence failure is a note that
/// points to the format's own app rather than a reason to hide it.
/// </para>
/// </remarks>
public static class SpatialSound
{
    /// <summary>Windows' identifier for each format, from Windows.Media.Audio.SpatialAudioFormatSubtype.</summary>
    public static readonly IReadOnlyDictionary<SpatialFormat, Guid> Subtypes = new Dictionary<SpatialFormat, Guid>
    {
        [SpatialFormat.WindowsSonic] = new("B53D940C-B846-4831-9F76-D102B9B725A0"),
        [SpatialFormat.DolbyAtmos] = new("1459AC38-3875-49BF-BB59-0FE80F4D395D"),
    };

    private static readonly SpatialFormat[] Order = [SpatialFormat.WindowsSonic, SpatialFormat.DolbyAtmos];

    /// <summary>Off, then each headphone format Windows says the headset supports, in a fixed order.</summary>
    public static IReadOnlyList<SpatialFormat> Offered(Func<Guid, bool> supported) =>
        [SpatialFormat.Off, .. Order.Where(f => supported(Subtypes[f]))];

    /// <summary>The format Windows has on, or null for one this does not offer.</summary>
    /// <remarks>Windows gives an empty identifier when spatial sound is off.</remarks>
    public static SpatialFormat? Of(Guid active)
    {
        if (active == Guid.Empty) return SpatialFormat.Off;
        foreach (var (format, id) in Subtypes)
            if (id == active) return format;
        return null;
    }

    /// <summary>The identifier to ask Windows for, empty for off.</summary>
    public static Guid SubtypeOf(SpatialFormat format) =>
        format == SpatialFormat.Off ? Guid.Empty : Subtypes[format];

    public static SpatialNote NoteFor(SpatialResult result) => result switch
    {
        SpatialResult.Succeeded => SpatialNote.None,
        SpatialResult.LicenseExpired or SpatialResult.LicenseNotValidForAudioEndpoint => SpatialNote.NeedsLicence,
        _ => SpatialNote.Refused,
    };
}
