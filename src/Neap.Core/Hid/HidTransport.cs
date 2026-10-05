using System.ComponentModel;
using System.Runtime.InteropServices;

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
public readonly record struct HidDeviceInfo(
    string Path, ushort VendorId, ushort ProductId, ushort UsagePage,
    int OutputLength, int FeatureLength, int InputLength);

/// <summary>
/// An open handle to the Stealth Pro II's vendor control collection.
/// </summary>
/// <remarks>
/// <para>
/// Replies are read with GET_REPORT of type Input on report id 7. The
/// collection declares no feature reports, so HidD_GetFeature fails; Swarm II
/// uses the input path too.
/// </para>
/// <para>
/// More than one matching collection can be present. The transmitter is
/// always there, and plugging the headset in with the USB-C cable adds a
/// second. They speak the same protocol but are different devices with
/// different storage, so anything that writes must know which one it has.
/// </para>
/// </remarks>
public sealed class HidTransport : IHidTransport
{
    public const ushort DefaultVendorId = 0x10F5;
    public const ushort VendorUsagePage = 0xFF13;
    public const byte OutReportId = 0x06;
    public const byte InReportId = 0x07;

    private IntPtr _handle;

    public string Path { get; }
    public ushort VendorId { get; }
    public ushort ProductId { get; }
    public ushort UsagePage { get; }
    public int OutputLength { get; }
    public int InputLength { get; }

    public HidTransport(string? path = null, ushort? productId = null)
    {
        HidDeviceInfo info = path is null
            ? FindDevice(productId: productId)
            : Describe(path) ?? throw new DeviceNotFoundException(path);

        Path = info.Path;
        VendorId = info.VendorId;
        ProductId = info.ProductId;
        UsagePage = info.UsagePage;
        OutputLength = info.OutputLength > 0 ? info.OutputLength : 62;
        InputLength = info.InputLength > 0 ? info.InputLength : 62;

        _handle = Native.CreateFileW(
            Path, Native.GenericRead | Native.GenericWrite,
            Native.FileShareReadWrite, IntPtr.Zero, Native.OpenExisting, 0, IntPtr.Zero);
        if (_handle == IntPtr.Zero || _handle == new IntPtr(-1))
            throw new TransportException(
                $"could not open device ({new Win32Exception(Marshal.GetLastWin32Error()).Message})");
    }

    public string Describe() =>
        $"0x{VendorId:x4}:0x{ProductId:x4} usage page 0x{UsagePage:x4}";

    /// <summary>Writes one output report, which must already start with its report id.</summary>
    /// <remarks>
    /// An oversized frame is refused rather than trimmed. The protocol has no
    /// continuation, so a truncated frame is a different instruction, not a
    /// shorter one, and the headset accepts it without complaint; a preset
    /// name that overflows would be stored mangled.
    /// </remarks>
    /// <exception cref="TransportException">The frame is too long, or the write failed.</exception>
    public void SendOutput(ReadOnlySpan<byte> report)
    {
        if (report.Length > OutputLength)
            throw new TransportException(
                $"frame is {report.Length} bytes and the output report holds {OutputLength}");

        var buffer = new byte[OutputLength];
        report.CopyTo(buffer);
        if (!Native.HidD_SetOutputReport(_handle, buffer, (uint)OutputLength))
            throw new TransportException(
                $"SetOutputReport failed ({new Win32Exception(Marshal.GetLastWin32Error()).Message})");
    }

    /// <summary>Reads one report from the headset, report id included.</summary>
    public byte[] GetInput(byte reportId = InReportId)
    {
        var buffer = new byte[InputLength];
        buffer[0] = reportId;
        if (!Native.HidD_GetInputReport(_handle, buffer, (uint)InputLength))
            throw new TransportException(
                $"GetInputReport failed ({new Win32Exception(Marshal.GetLastWin32Error()).Message})");
        return buffer;
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero && _handle != new IntPtr(-1))
        {
            Native.CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    // -- enumeration -------------------------------------------------------

    /// <summary>Every collection present from one vendor, on one usage page or, given null, on any.</summary>
    public static IReadOnlyList<HidDeviceInfo> ListDevices(
        ushort vendorId = DefaultVendorId, ushort? usagePage = VendorUsagePage)
    {
        var found = new List<HidDeviceInfo>();
        foreach (var path in EnumeratePaths())
        {
            var info = Describe(path);
            if (info is { } d && d.VendorId == vendorId && (usagePage is null || d.UsagePage == usagePage))
                found.Add(d);
        }
        return found;
    }

    /// <summary>
    /// The order in which devices are asked: the headset itself, then a USB
    /// Transmitter, then a Charging Dock, then anything unrecognised.
    /// </summary>
    /// <remarks>
    /// This is a tie-break, not a decision. Which devices are present says
    /// nothing about which one the headset is on: two transmitters can be
    /// plugged in at once, the headset pairs with one at a time, and the other
    /// opens cleanly and answers nothing. A caller must ask each one, as
    /// <see cref="HeadsetClient.Behind"/> does.
    /// </remarks>
    private static int Rank(HidDeviceInfo device) => Transmitters.PieceOf(device.ProductId) switch
    {
        Transmitters.Piece.Headset => 0,
        Transmitters.Piece.Transmitter => 1,
        Transmitters.Piece.Dock => 2,
        _ => 3,
    };

    /// <summary>Every matching control collection present, in the order to ask them.</summary>
    public static IReadOnlyList<HidDeviceInfo> Candidates(
        ushort vendorId = DefaultVendorId, ushort usagePage = VendorUsagePage)
    {
        return ListDevices(vendorId, usagePage).OrderBy(Rank).ToList();
    }

    public static HidDeviceInfo FindDevice(
        ushort vendorId = DefaultVendorId, ushort usagePage = VendorUsagePage,
        ushort? productId = null)
    {
        var found = ListDevices(vendorId, usagePage);
        if (productId is not null)
        {
            foreach (var info in found)
                if (info.ProductId == productId) return info;
        }
        else if (found.Count > 0)
        {
            return found.OrderBy(Rank).First();
        }

        var want = productId is null ? "" : $", product 0x{productId:x4}";
        throw new DeviceNotFoundException(
            $"no HID collection with vendor 0x{vendorId:x4}{want} and usage page "
            + $"0x{usagePage:x4} — is the transmitter plugged in?");
    }

    private static IEnumerable<string> EnumeratePaths()
    {
        Native.HidD_GetHidGuid(out var guid);
        IntPtr set = Native.SetupDiGetClassDevsW(
            ref guid, null, IntPtr.Zero, Native.DigcfPresent | Native.DigcfDeviceInterface);
        if (set == new IntPtr(-1)) yield break;
        try
        {
            var data = new Native.SpDeviceInterfaceData
            {
                CbSize = (uint)Marshal.SizeOf<Native.SpDeviceInterfaceData>()
            };
            for (uint index = 0;
                 Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref data);
                 index++)
            {
                Native.SetupDiGetDeviceInterfaceDetailW(
                    set, ref data, IntPtr.Zero, 0, out uint needed, IntPtr.Zero);
                if (needed == 0) continue;

                IntPtr detail = Marshal.AllocHGlobal((int)needed);
                try
                {
                    // cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA_W: 8 on x64,
                    // 6 on x86. It is the size of the header, not the buffer;
                    // a wrong value makes the call fail with no useful error.
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!Native.SetupDiGetDeviceInterfaceDetailW(
                            set, ref data, detail, needed, out _, IntPtr.Zero))
                        continue;
                    var path = Marshal.PtrToStringUni(detail + 4);
                    if (!string.IsNullOrEmpty(path)) yield return path;
                }
                finally
                {
                    Marshal.FreeHGlobal(detail);
                }
            }
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(set);
        }
    }

    private static HidDeviceInfo? Describe(string path)
    {
        // No access rights: enough to query attributes and capabilities
        // without disturbing whoever has the device open.
        IntPtr handle = Native.CreateFileW(
            path, 0, Native.FileShareReadWrite, IntPtr.Zero, Native.OpenExisting, 0, IntPtr.Zero);
        if (handle == new IntPtr(-1)) return null;
        try
        {
            var attrs = new Native.HiddAttributes
            {
                Size = (uint)Marshal.SizeOf<Native.HiddAttributes>()
            };
            if (!Native.HidD_GetAttributes(handle, ref attrs)) return null;
            if (!Native.HidD_GetPreparsedData(handle, out IntPtr preparsed)) return null;
            try
            {
                var caps = new Native.HidpCaps();
                if (Native.HidP_GetCaps(preparsed, ref caps) != Native.HidpStatusSuccess)
                    return null;
                return new HidDeviceInfo(path, attrs.VendorId, attrs.ProductId,
                    caps.UsagePage, caps.OutputReportByteLength,
                    caps.FeatureReportByteLength, caps.InputReportByteLength);
            }
            finally
            {
                Native.HidD_FreePreparsedData(preparsed);
            }
        }
        finally
        {
            Native.CloseHandle(handle);
        }
    }
}
