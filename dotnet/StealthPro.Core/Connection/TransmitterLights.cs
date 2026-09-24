namespace StealthPro.Core.Connection;

/// <summary>Which transmitter's lights the app can offer to set.</summary>
public enum LightSet
{
    /// <summary>None: nothing is answering, or which lights a control would reach has not been seen.</summary>
    None,
    /// <summary>The Charging Dock's two rings.</summary>
    Dock,
    /// <summary>The USB Transmitter's one light.</summary>
    Transmitter,
}

/// <summary>Decides which transmitter's lights are offered.</summary>
/// <remarks>
/// <para>
/// The brightness settings reach the transmitter carrying the headset, so
/// they are named and laid out for that transmitter. Under the Charging
/// Dock's names, the USB Transmitter's light would be called the ring around
/// a battery slot it does not have.
/// </para>
/// <para>
/// Each case was checked by eye, not by reading values back. On the Charging
/// Dock both rings change. On the USB Transmitter its one light follows the
/// first brightness, and the second changes nothing, so it is not offered.
/// </para>
/// <para>
/// When the headset's sound and settings are on different transmitters,
/// nothing is offered. Which transmitter's lights a setting reaches then has
/// not been observed, and a control that might do nothing is not put on
/// screen.
/// </para>
/// </remarks>
public static class TransmitterLights
{
    public static LightSet For(HeadsetStatus status) =>
        status.Link != Link.Connected || status.ControlVia.Length > 0 ? LightSet.None
        : Transmitters.PieceOf(status.Product) switch
        {
            Transmitters.Piece.Dock => LightSet.Dock,
            Transmitters.Piece.Transmitter => LightSet.Transmitter,
            _ => LightSet.None,
        };
}
