namespace Neap.Core.Connection;

/// <summary>The headset's battery, as the app shows it.</summary>
/// <param name="Percent">The reported charge.</param>
/// <param name="Charging">Whether the headset says it is on power.</param>
/// <param name="Settling">Whether the reading is still falling too fast to be the battery draining; see <see cref="BatteryTrend"/>.</param>
public readonly record struct BatteryReading(int Percent, bool Charging, bool Settling = false);

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
    /// <param name="falling">Whether the reading has been falling too fast to be the battery draining.</param>
    /// <returns>The reading, or null when there is none worth showing.</returns>
    /// <remarks>A reading that is rising on power is not settling: only a fall is.</remarks>
    public static BatteryReading? Of(HeadsetStatus status, int? percent, int? power, bool falling = false) =>
        (status.Link == Link.Connected || status.SwitchedOff) && percent is { } charge
            ? new BatteryReading(charge, power == 1, falling && power != 1)
            : null;
}
