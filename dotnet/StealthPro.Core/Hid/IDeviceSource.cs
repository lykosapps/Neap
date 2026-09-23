namespace StealthPro.Core.Hid;

/// <summary>
/// The control collections that are plugged in, and a way to open one.
/// </summary>
/// <remarks>
/// <see cref="SystemDevices"/> is what Windows has; the pretend headset
/// stands in for it when the app is tested without the hardware.
/// </remarks>
public interface IDeviceSource
{
    /// <summary>Every control collection present, in the order to ask them.</summary>
    IReadOnlyList<HidDeviceInfo> Candidates();

    /// <summary>Opens one of <see cref="Candidates"/>.</summary>
    /// <exception cref="TransportException">The device could not be opened.</exception>
    IHidTransport Open(HidDeviceInfo device);
}

/// <summary>The control collections Windows has.</summary>
public sealed class SystemDevices : IDeviceSource
{
    public static SystemDevices Instance { get; } = new();

    private SystemDevices() { }

    public IReadOnlyList<HidDeviceInfo> Candidates() => HidTransport.Candidates();

    public IHidTransport Open(HidDeviceInfo device) => new HidTransport(device.Path);
}
