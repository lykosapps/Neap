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
/// 0x290 and 0x250 — the headset's own account of how it is attached, and
/// whether it is taking power.
///
/// Both were unlabelled until they were watched. Every value the headset
/// reports was polled while the hardware was operated, and these two were the
/// only things that moved: <b>0x290</b> went 3 -> 2 when Bluetooth was
/// disconnected, 2 -> 4 when the headset was plugged into USB, 4 -> 5 when
/// Bluetooth came back, and 5 -> 3 when the cable came out again. Every one of
/// those fits one shape and no other:
///
/// <code>
///   bit 0      Bluetooth is connected
///   bits 1-2   how it is attached: 1 = 2.4GHz, 2 = USB
/// </code>
///
/// <b>0x250</b> went 0 -> 1 as the cable went in and 1 -> 0 as it came out,
/// and the battery percentage climbed only while it read 1.
///
/// Tested in both directions, which the rest of this project has learned to
/// insist on.
///
/// <b>0x250 is read as charging rather than as "a cable is attached".</b>
/// Three things point that way and one still does not settle it. The
/// attachment is already reported, in 0x290 — a second flag that only said
/// the same thing would be carrying no information. USB-C on this headset is
/// not a charging port with audio bolted on: plugged into the PC it enumerates
/// as its own playback and recording device, so "cable in" and "charging" are
/// genuinely separate facts about it. And the battery percentage moved only
/// while this read 1. What would settle it is a headset sitting at 100% with
/// the cable still in: if this goes to 0 while 0x290 still says USB, the name
/// is right.
/// </summary>
public static class LinkState
{
    /// <summary>The attachment and Bluetooth byte.</summary>
    public const int Key = 0x290;

    /// <summary>1 while the battery is filling. See the class remarks.</summary>
    public const int ChargingKey = 0x250;

    /// <summary>
    /// <b>Do not read a meaning off this value.</b> It was documented as "2
    /// while the headset is on this transmitter, 1 while it is not", and the
    /// app was built on that. Measured later with the Charging Dock the only
    /// transmitter plugged in, the only one paired, and the one the headset
    /// had selected — music playing, both wheels working — it read 1, and the
    /// app sat on "Not connected" indefinitely.
    ///
    /// The best available reading is that it is the slot number the headset is
    /// using: the dock sat in slot 1 and it said 1, and the original 2-to-1
    /// observation fits a dock in slot 2 equally well. One data point, so it
    /// is written here as a theory and nothing depends on it.
    ///
    /// Use it only for the thing it certainly supports: <b>when it moves,
    /// something happened.</b> Which transmitter the headset selected is
    /// stated outright by the slots, and comparing that slot's product id with
    /// the device actually open is an answer rather than an inference.
    ///
    /// The original observation, kept because it is still real:
    ///
    /// <b>Not "there is an audio link".</b> That was the hope, because the app
    /// still cannot tell a healthy connection from one carrying no sound. It
    /// is narrower: asked of the dock, it answers whether the dock is the one
    /// the headset is using. Measured by pressing CrossPlay with both
    /// transmitters plugged in — it went 2 to 1 as the headset left the dock
    /// and 1 to 2 as it came back, in both directions, while audio was working
    /// perfectly through the other transmitter the whole time.
    ///
    /// It is worth having anyway. It says the same thing as the transmitter
    /// slots' active flag, but it is one value rather than four round trips,
    /// and it moved <b>about three seconds earlier</b> than the slots did on
    /// both transitions. So it makes a good trigger: watch this, and read the
    /// slots only when it changes.
    /// </summary>
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
