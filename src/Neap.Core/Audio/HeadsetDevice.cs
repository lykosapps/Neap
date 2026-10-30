namespace Neap.Core.Audio;

/// <summary>Chooses the sound device the headset's volume and mute are read from and set on.</summary>
/// <remarks>
/// <para>
/// The headset can offer several devices at once, one per transmitter plugged
/// in and one more for itself over the USB-C cable, so the one in use comes
/// first: taking the first headset device could move the Charging Dock's
/// level while the sound plays through the cable, which changes nothing
/// audible.
/// </para>
/// <para>
/// Any headset device comes before the default, as moving the slider while
/// the default is a pair of speakers would otherwise change the speakers.
/// With no headset device it falls back to the default and says that it is
/// not the headset's.
/// </para>
/// <para>Windows and Linux each list their own kind of device, so the choice takes how to tell a device's part.</para>
/// </remarks>
public static class HeadsetDevice
{
    /// <summary>The device to use, and whether it is the headset's.</summary>
    /// <param name="devices">The devices on one flow, in the order the system lists them.</param>
    /// <param name="isHeadset">Whether a device is one of the headset's.</param>
    /// <param name="isDefault">Whether a device is the system's default.</param>
    /// <returns>The device, or null when there is no headset device and no default.</returns>
    public static (T? Device, bool Headset) Choose<T>(
        IReadOnlyList<T> devices, Func<T, bool> isHeadset, Func<T, bool> isDefault)
        where T : class
    {
        var headset = devices.FirstOrDefault(d => isHeadset(d) && isDefault(d))
                      ?? devices.FirstOrDefault(isHeadset);
        return headset is not null ? (headset, true) : (devices.FirstOrDefault(isDefault), false);
    }
}
