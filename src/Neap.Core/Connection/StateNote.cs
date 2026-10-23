namespace Neap.Core.Connection;

/// <summary>Which explanation a state needs on Home, if any.</summary>
public enum Note
{
    /// <summary>Nothing to explain.</summary>
    None,
    /// <summary>The headset's settings are out of reach and sound still plays.</summary>
    Unreachable,
    /// <summary>The settings are out of reach and the sound went with them.</summary>
    UnreachableNoSound,
    /// <summary>Connected, and no transmitter is sending the headset sound.</summary>
    NoSound,
    /// <summary>Switched off on its USB-C cable, which the cable shows for certain.</summary>
    OffOnCable,
    /// <summary>Switched off or out of range, which cannot be told apart.</summary>
    Off,
    /// <summary>Connected to a headset whose settings Neap only reads, so its controls are shown but cannot be used.</summary>
    ReadOnly,
    /// <summary>The transmitter has stopped answering, and only plugging it in again puts it right.</summary>
    Stopped,
}

/// <summary>Decides which explanation Home gives for the headset's state.</summary>
/// <remarks>
/// Nothing plugged in has no note here: every page says that once, at the
/// top. Connecting has none either; it lasts a few seconds.
/// </remarks>
public static class StateNote
{
    /// <param name="status">The headset's state.</param>
    /// <param name="plugged">
    /// The product ids plugged in, as four hex digits, or null before the
    /// first look. With nothing but the headset's own cable among them, a
    /// quiet headset is off: there is no transmitter for it to be out of
    /// range of, and no CrossPlay to press. The cable shows it for certain a
    /// few seconds later.
    /// </param>
    /// <param name="readOnly">Whether the headset is one whose settings Neap only reads; see <see cref="HeadsetModel.Writable"/>.</param>
    /// <param name="stopped">
    /// Whether a transmitter still gives no answer of its own after Neap
    /// restarted it. Switching the headset on cannot help then, so it comes
    /// before every other reason for a quiet headset.
    /// </param>
    public static Note For(HeadsetStatus status, IReadOnlyList<string>? plugged = null, bool readOnly = false, bool stopped = false) => status.Link switch
    {
        Link.Quiet when stopped => Note.Stopped,
        Link.Silent when status.NoSound => Note.UnreachableNoSound,
        Link.Silent => Note.Unreachable,
        Link.Connected when status.NoSound => Note.NoSound,
        Link.Connected when readOnly => Note.ReadOnly,
        Link.Quiet when status.SwitchedOff || OnlyTheCable(plugged) => Note.OffOnCable,
        Link.Quiet => Note.Off,
        _ => Note.None,
    };

    private static bool OnlyTheCable(IReadOnlyList<string>? plugged) =>
        plugged is { Count: > 0 } && plugged.All(product => Transmitters.PieceOf(product) == Transmitters.Piece.Headset);
}
