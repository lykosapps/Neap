namespace StealthPro.Core.Connection;

/// <summary>What the line of readings on Home says about how the headset is connected.</summary>
public enum ConnectionShown
{
    /// <summary>Not connected: the state's own headline, as the header shows it.</summary>
    State,
    /// <summary>The headset's USB-C cable.</summary>
    Cable,
    /// <summary>Connected, and no transmitter is carrying its sound.</summary>
    NoTransmitter,
    /// <summary>The transmitter carrying its sound.</summary>
    Transmitter,
    /// <summary>The transmitter its sound is set to, which is not plugged in.</summary>
    TransmitterUnplugged,
}

/// <summary>
/// Decides how Home names the headset's connection, and whether its USB-C
/// cable is the connection.
/// </summary>
/// <remarks>
/// <para>
/// A transmitter can be selected and unplugged at once (see
/// <see cref="TransmitterList"/>), and the line must never plainly name
/// something not plugged in as the way the headset is connected.
/// </para>
/// <para>
/// Not yet having looked at what is plugged in is not "not plugged in": the
/// first paint comes before the first look, and would flash the wrong answer.
/// Nothing found at all counts the same way, since a connected headset is
/// reached through something.
/// </para>
/// </remarks>
public static class ConnectionLine
{
    /// <param name="status">The headset's state.</param>
    /// <param name="cable">The headset is plugged in with its USB-C cable; see <see cref="OverCable"/>.</param>
    /// <param name="plugged">Product ids of what is plugged in, or null before the first look.</param>
    public static ConnectionShown Of(HeadsetStatus status, bool cable, IReadOnlyCollection<string>? plugged) =>
        status.Link != Link.Connected ? ConnectionShown.State
        : cable ? ConnectionShown.Cable
        : status.NoSound ? ConnectionShown.NoTransmitter
        : plugged is null || plugged.Count == 0
          || plugged.Contains(status.Product, StringComparer.OrdinalIgnoreCase)
            ? ConnectionShown.Transmitter
            : ConnectionShown.TransmitterUnplugged;

    /// <summary>
    /// Whether the headset is plugged in with its USB-C cable, which makes the
    /// cable its connection: the app is talking to it over the cable, or its
    /// own audio device is there for Windows to use.
    /// </summary>
    /// <remarks>
    /// This follows what is plugged in, not where Windows sends sound. With
    /// the cable in, the cable is the only right place for sound, microphone
    /// and calls; anything Windows sends elsewhere is for the routing warning
    /// to say. Following Windows instead would show "Charging Dock" over a
    /// headset whose microphone and calls are on the cable.
    /// </remarks>
    /// <param name="status">The headset's state.</param>
    /// <param name="cableDevice">The product id of the headset's own audio device in Windows, or empty.</param>
    public static bool OverCable(HeadsetStatus status, string cableDevice) =>
        status.Route == Route.DirectUsb || cableDevice.Length > 0;
}
