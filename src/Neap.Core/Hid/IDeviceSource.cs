namespace Neap.Core.Hid;

/// <summary>
/// The control collections that are plugged in, and a way to open one.
/// </summary>
/// <remarks>
/// <see cref="SystemDevices"/> is what the operating system has; the pretend
/// headset stands in for it when the app is tested without the hardware.
/// </remarks>
public interface IDeviceSource
{
    /// <summary>Every control collection present, in the order to ask them.</summary>
    IReadOnlyList<HidDeviceInfo> Candidates();

    /// <summary>Opens one of <see cref="Candidates"/>.</summary>
    /// <exception cref="TransportException">The device could not be opened.</exception>
    IHidTransport Open(HidDeviceInfo device);
}

/// <summary>The control collections the operating system has.</summary>
public sealed class SystemDevices : IDeviceSource
{
    public static SystemDevices Instance { get; } = new();

    private SystemDevices() { }

    public IReadOnlyList<HidDeviceInfo> Candidates() =>
        HidControl.InAskingOrder(List(HidControl.UsagePage));

    public IHidTransport Open(HidDeviceInfo device) =>
        OperatingSystem.IsWindows() ? new HidTransport(device.Path) : throw Unsupported();

    /// <summary>Every Turtle Beach collection present, on one usage page or, given null, on any.</summary>
    public static IReadOnlyList<HidDeviceInfo> List(ushort? usagePage) =>
        OperatingSystem.IsWindows()
            ? HidTransport.ListDevices(HidControl.VendorId, usagePage)
            : throw Unsupported();

    private static PlatformNotSupportedException Unsupported() =>
        new($"{Environment.OSVersion.Platform} has no way to reach the headset yet");
}
