using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Neap.Core.Hid;

/// <summary>
/// An open handle to the Stealth Pro II's vendor control collection, through
/// Windows' HID class driver.
/// </summary>
/// <remarks>
/// Windows gives each top-level collection a device of its own, so the vendor
/// collection is found by its usage page, and HidD_GetFeature fails on it
/// because it declares no feature reports. <see cref="HidControl"/>
/// describes the reports themselves.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class HidTransport : IHidTransport
{
    private IntPtr _handle;

    public string Path { get; }
    public ushort VendorId { get; }
    public ushort ProductId { get; }
    public ushort UsagePage { get; }
    public int OutputLength { get; }
    public int InputLength { get; }

    /// <exception cref="DeviceNotFoundException">Nothing at that path can be described.</exception>
    /// <exception cref="TransportException">The device could not be opened.</exception>
    public HidTransport(string path)
    {
        HidDeviceInfo info = Describe(path) ?? throw new DeviceNotFoundException(path);

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
    public byte[] GetInput(byte reportId = HidControl.InReportId)
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
        ushort vendorId = HidControl.VendorId, ushort? usagePage = HidControl.UsagePage)
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
