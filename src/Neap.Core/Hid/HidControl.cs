namespace Neap.Core.Hid;

public class DeviceNotFoundException : Exception
{
    public DeviceNotFoundException(string message) : base(message) { }
}

public class TransportException : Exception
{
    public TransportException(string message) : base(message) { }
}

/// <summary>Everything we can learn about a control collection before opening it.</summary>
/// <param name="Path">What the operating system opens it by.</param>
/// <param name="VendorId">The USB vendor id.</param>
/// <param name="ProductId">The USB product id, which says which piece of the family it is.</param>
/// <param name="UsagePage">The usage page of its top-level collection.</param>
/// <param name="OutputLength">The output report's length, report id included.</param>
/// <param name="FeatureLength">The feature report's length, report id included.</param>
/// <param name="InputLength">The input report's length, report id included.</param>
public readonly record struct HidDeviceInfo(
    string Path, ushort VendorId, ushort ProductId, ushort UsagePage,
    int OutputLength, int FeatureLength, int InputLength);

/// <summary>
/// The Stealth Pro II's vendor control collection: how it is recognised, the
/// reports it is spoken to through, and which device to ask first.
/// </summary>
/// <remarks>
/// <para>
/// Requests go out as output report 6. Replies are read with GET_REPORT of
/// type Input on report 7. The collection declares no feature reports; Swarm
/// II uses the input path too.
/// </para>
/// <para>
/// More than one matching collection can be present. The transmitter is
/// always there, and plugging the headset in with the USB-C cable adds a
/// second. They speak the same protocol but are different devices with
/// different storage, so anything that writes must know which one it has.
/// </para>
/// </remarks>
public static class HidControl
{
    public const ushort VendorId = 0x10F5;
    public const ushort UsagePage = 0xFF13;
    public const byte OutReportId = 0x06;
    public const byte InReportId = 0x07;

    /// <summary>
    /// The devices in the order to ask them: the headset itself, then a USB
    /// Transmitter, then a Charging Dock, then anything unrecognised.
    /// </summary>
    /// <remarks>
    /// This is a tie-break, not a decision. Which devices are present says
    /// nothing about which one the headset is on: two transmitters can be
    /// plugged in at once, the headset pairs with one at a time, and the other
    /// opens cleanly and answers nothing. A caller must ask each one, as
    /// <see cref="HeadsetClient.Behind"/> does.
    /// </remarks>
    public static IReadOnlyList<HidDeviceInfo> InAskingOrder(IEnumerable<HidDeviceInfo> devices) =>
        devices.OrderBy(Rank).ToList();

    /// <summary>The device with a product id, or with none given the first to ask.</summary>
    /// <exception cref="DeviceNotFoundException">There is no such device.</exception>
    public static HidDeviceInfo Pick(IEnumerable<HidDeviceInfo> devices, ushort? productId = null)
    {
        var ordered = InAskingOrder(devices);
        foreach (var device in ordered)
            if (productId is null || device.ProductId == productId) return device;

        var want = productId is null ? "" : $", product 0x{productId:x4}";
        throw new DeviceNotFoundException(
            $"no HID collection with vendor 0x{VendorId:x4}{want} and usage page "
            + $"0x{UsagePage:x4} — is the transmitter plugged in?");
    }

    private static int Rank(HidDeviceInfo device) => Transmitters.PieceOf(device.ProductId) switch
    {
        Transmitters.Piece.Headset => 0,
        Transmitters.Piece.Transmitter => 1,
        Transmitters.Piece.Dock => 2,
        _ => 3,
    };
}
