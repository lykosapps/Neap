namespace StealthPro.Core.Settings;

/// <summary>How strong the wireless link is, in the four words the app uses.</summary>
public enum SignalStrength { Strong, Good, Ok, Weak }

/// <summary>
/// Key 0x320: the wireless link's strength, in dBm.
/// </summary>
/// <remarks>
/// The value arrives signed in notifications and unsigned in a full read, so
/// it is normalised to dBm either way; otherwise it swings on which arrived
/// last. It describes the 2.4 GHz link only, and says nothing about sound
/// that goes over a cable.
/// </remarks>
public static class Signal
{
    public const int Key = 0x320;

    public static int Dbm(int raw) => raw > 127 ? raw - 256 : raw;

    public static SignalStrength Strength(int raw) => Dbm(raw) switch
    {
        >= -55 => SignalStrength.Strong,
        >= -65 => SignalStrength.Good,
        >= -73 => SignalStrength.Ok,
        _ => SignalStrength.Weak,
    };
}
