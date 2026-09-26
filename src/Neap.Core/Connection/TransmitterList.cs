namespace Neap.Core.Connection;

/// <summary>What one transmitter in the list is doing, in the order the list shows them.</summary>
public enum TransmitterState
{
    /// <summary>Carrying the headset's sound.</summary>
    InUse,
    /// <summary>Plugged in, and CrossPlay would move the headset's sound to it.</summary>
    CanSwitchTo,
    /// <summary>Plugged in; what the headset is doing with it is not known.</summary>
    PluggedIn,
    /// <summary>The headset's sound is set to it, and it is not plugged in.</summary>
    SelectedButUnplugged,
    /// <summary>Known to the headset, not plugged in.</summary>
    NotPluggedIn,
}

/// <param name="Firmware">Its firmware version, when the headset reported it; empty otherwise.</param>
/// <param name="Spare">The spare battery to show on it, or null when it shows none.</param>
public sealed record TransmitterRow(string Name, string Firmware, TransmitterState State, SpareReading? Spare = null);

/// <summary>
/// Every transmitter worth listing, from what the headset reported and what
/// is plugged in, each with one plain state.
/// </summary>
/// <remarks>
/// <para>
/// Selected is not the same as plugged in: unplug the transmitter carrying the
/// sound while the other carries the controls, and the headset goes on
/// reporting the unplugged one as selected. Over the cable no transmitter
/// carries the sound, so none is in use and none is offered to switch to.
/// </para>
/// <para>
/// There is deliberately no "not paired" state: the slots list the
/// transmitters the headset has seen lately, not everything it is paired with.
/// </para>
/// <para>
/// A Charging Dock's spare battery is shown only while the dock is in use.
/// The headset learns it over its link to the dock, and for a dock it is not
/// linked to, the slot holds whatever it last heard.
/// </para>
/// </remarks>
public static class TransmitterList
{
    /// <param name="plugged">Product ids of every Turtle Beach device plugged in.</param>
    /// <param name="cable">The headset is plugged in with its USB-C cable.</param>
    public static IReadOnlyList<TransmitterRow> Rows(HeadsetStatus status,
        IEnumerable<Transmitter> known, IEnumerable<string> plugged, bool cable)
    {
        var here = plugged.Where(IsTransmitter).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string inUse = status.Link == Link.Connected && !status.NoSound && !cable
            ? status.Product : "";

        var rows = new List<TransmitterRow>();
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string product, Transmitter? slot)
        {
            if (!listed.Add(product)) return;
            string name = slot?.Kind is { Length: > 0 } kind ? kind
                : Transmitters.Hardware.TryGetValue(product, out var called) ? called
                : "Transmitter";
            bool plugs = here.Contains(product);
            bool selected = inUse.Length > 0 && string.Equals(product, inUse, StringComparison.OrdinalIgnoreCase);

            var state = selected && !plugs ? TransmitterState.SelectedButUnplugged
                : selected ? TransmitterState.InUse
                : plugs && status.Link == Link.Connected && !cable ? TransmitterState.CanSwitchTo
                : plugs ? TransmitterState.PluggedIn
                : TransmitterState.NotPluggedIn;
            rows.Add(new TransmitterRow(name, slot?.Firmware ?? "", state,
                state == TransmitterState.InUse ? slot?.Spare : null));
        }

        foreach (var slot in known) Add(slot.ProductId, slot);
        foreach (var product in here) Add(product, null);

        return rows.OrderBy(r => Order(r.State)).ToList();
    }

    private static int Order(TransmitterState state) => state switch
    {
        TransmitterState.InUse => 0,
        TransmitterState.CanSwitchTo or TransmitterState.PluggedIn => 1,
        _ => 2,
    };

    private static bool IsTransmitter(string product) =>
        Transmitters.PieceOf(product) is Transmitters.Piece.Dock or Transmitters.Piece.Transmitter;
}
