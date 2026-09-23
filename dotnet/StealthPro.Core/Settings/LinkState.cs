namespace StealthPro.Core.Settings;

/// <summary>How the headset says it is attached.</summary>
public enum Attachment
{
    /// <summary>Not reported, or a value we have not seen.</summary>
    Unknown,
    /// <summary>Over 2.4GHz, through a transmitter.</summary>
    Wireless,
    /// <summary>Cabled to the PC by USB.</summary>
    Usb,
}

/// <summary>
/// Keys 0x290 and 0x250: the headset's own account of how it is attached, and
/// whether it is taking power.
/// </summary>
/// <remarks>
/// <para>
/// Both were identified by polling every value the headset reports while the
/// hardware was operated; these two were the only ones that moved. 0x290 went
/// 3 -> 2 when Bluetooth was disconnected, 2 -> 4 when the headset was plugged
/// into USB, 4 -> 5 when Bluetooth came back, and 5 -> 3 when the cable came
/// out again. Every one of those fits one layout and no other:
/// </para>
/// <code>
///   bit 0      Bluetooth is connected
///   bits 1-2   how it is attached: 1 = 2.4GHz, 2 = USB
/// </code>
/// <para>
/// 0x250 went 0 -> 1 as the cable went in and 1 -> 0 as it came out, and the
/// battery percentage climbed only while it read 1. Both keys were tested in
/// both directions.
/// </para>
/// <para>
/// 0x250 is read as charging rather than as "a cable is attached", for three
/// reasons. The attachment is already reported in 0x290, so a second flag
/// saying the same thing would carry no information. USB-C on this headset is
/// not a charging port with audio bolted on: plugged into the PC it enumerates
/// as its own playback and recording device, so "cable in" and "charging" are
/// separate facts about it. And the battery percentage moved only while this
/// read 1. It is not yet settled: with the headset at 100% and the cable still
/// in, this going to 0 while 0x290 still says USB would confirm the name.
/// </para>
/// </remarks>
public static class LinkState
{
    /// <summary>The attachment and Bluetooth byte.</summary>
    public const int Key = 0x290;

    /// <summary>1 while the battery is filling. See the class remarks.</summary>
    public const int ChargingKey = 0x250;

    /// <summary>
    /// A value that changes when the headset moves between transmitters. Use
    /// only the fact that it changed, not what it equals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It does not mean "2 while the headset is on this transmitter, 1 while it
    /// is not". With the Charging Dock the only transmitter plugged in, the
    /// only one paired, and the one the headset had selected (music playing,
    /// both wheels working), it read 1; treating that as "not on this
    /// transmitter" leaves the app on "Not connected" indefinitely.
    /// </para>
    /// <para>
    /// The best available reading is that it is the slot number the headset is
    /// using: the dock sat in slot 1 and it said 1, and the 2-to-1 change below
    /// fits a dock in slot 2 equally well. That is one data point, so it is a
    /// theory and nothing depends on it. Which transmitter the headset selected
    /// is stated outright by the slots, and comparing that slot's product id
    /// with the device actually open is an answer rather than an inference.
    /// </para>
    /// <para>
    /// It is not "there is an audio link"; it cannot tell a healthy connection
    /// from one carrying no sound. Pressing CrossPlay with both transmitters
    /// plugged in, asked of the dock, it went 2 to 1 as the headset left the
    /// dock and 1 to 2 as it came back, in both directions, while audio worked
    /// through the other transmitter the whole time.
    /// </para>
    /// <para>
    /// It makes a good trigger. It moves with the transmitter slots' active
    /// flag, but it is one value rather than four round trips, and it moved
    /// about three seconds earlier than the slots on both transitions. Watch
    /// this, and read the slots only when it changes.
    /// </para>
    /// </remarks>
    public const int OnThisTransmitterKey = 0x150;

    public static bool OnThisTransmitter(int value) => value == 2;

    public static bool Bluetooth(int value) => (value & 1) != 0;

    public static Attachment How(int value) => (value >> 1) switch
    {
        1 => Attachment.Wireless,
        2 => Attachment.Usb,
        _ => Attachment.Unknown,
    };
}
