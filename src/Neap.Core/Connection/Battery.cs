namespace Neap.Core.Connection;

/// <summary>The headset's battery, as the app shows it.</summary>
public readonly record struct BatteryReading(int Percent, bool Charging);

/// <summary>Decides whether the headset's battery reading is worth showing.</summary>
/// <remarks>
/// With the settings out of reach, a transmitter answers with the last value
/// it holds for a headset it can no longer reach, and a stale number is worse
/// than none. Switched off on its cable, the headset still answers, for
/// charging, so the reading is real there and the one worth seeing.
/// </remarks>
public static class Battery
{
    /// <summary>Key 0x240: the battery's charge, as a percentage.</summary>
    public const int Key = 0x240;

    /// <param name="status">The headset's state.</param>
    /// <param name="percent">The reported charge, or null if none has been reported.</param>
    /// <param name="power">The reported power source (<see cref="Settings.LinkState.ChargingKey"/>), where 1 is charging.</param>
    /// <returns>The reading, or null when there is none worth showing.</returns>
    public static BatteryReading? Of(HeadsetStatus status, int? percent, int? power) =>
        (status.Link == Link.Connected || status.SwitchedOff) && percent is { } charge
            ? new BatteryReading(charge, power == 1)
            : null;
}
