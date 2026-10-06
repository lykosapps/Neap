using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Neap.Core.Hid;

/// <summary>
/// An open handle to the Stealth Pro II's vendor control collection, through
/// Linux's hidraw driver.
/// </summary>
/// <remarks>
/// <para>
/// Linux gives one hidraw device per USB interface, which can hold several
/// top-level collections, so each is described from the interface's report
/// descriptor. Reports are exchanged with HIDIOCSOUTPUT and HIDIOCGINPUT,
/// which send SET_REPORT and GET_REPORT over the control pipe exactly as
/// Windows' HidD_SetOutputReport and HidD_GetInputReport do; a plain write
/// or read would use the interrupt pipe instead.
/// </para>
/// <para>
/// Describing needs nothing but sysfs, which anyone can read. Opening needs
/// read and write on the device node, which Linux gives only to root unless
/// a udev rule says otherwise, so a refusal says so rather than looking like
/// an absent headset.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed class HidrawTransport : IHidTransport
{
    private const string Devices = "/sys/class/hidraw";

    private readonly SafeFileHandle _handle;

    public string Path { get; }
    public ushort VendorId { get; }
    public ushort ProductId { get; }
    public ushort UsagePage { get; }
    public int OutputLength { get; }
    public int InputLength { get; }

    /// <param name="device">One of <see cref="ListDevices"/>.</param>
    /// <exception cref="TransportException">The device node could not be opened.</exception>
    public HidrawTransport(HidDeviceInfo device)
    {
        Path = device.Path;
        VendorId = device.VendorId;
        ProductId = device.ProductId;
        UsagePage = device.UsagePage;
        OutputLength = device.OutputLength;
        InputLength = device.InputLength;

        try
        {
            _handle = File.OpenHandle(Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        }
        catch (UnauthorizedAccessException)
        {
            throw new AccessDeniedException(
                $"no permission to open {Path}; a udev rule has to give this user access to the headset");
        }
        catch (IOException ex)
        {
            throw new TransportException($"could not open {Path} ({ex.Message})");
        }
    }

    public string Describe() =>
        $"0x{VendorId:x4}:0x{ProductId:x4} usage page 0x{UsagePage:x4} on {Path}";

    /// <summary>Writes one output report, which must already start with its report id.</summary>
    /// <remarks>An oversized frame is refused rather than trimmed, for the reason on <see cref="HidTransport.SendOutput"/>.</remarks>
    /// <exception cref="TransportException">The frame is too long, or the write failed.</exception>
    public void SendOutput(ReadOnlySpan<byte> report)
    {
        if (report.Length > OutputLength)
            throw new TransportException(
                $"frame is {report.Length} bytes and the output report holds {OutputLength}");

        var buffer = new byte[OutputLength];
        report.CopyTo(buffer);
        Control(SendOutputNumber, buffer, "HIDIOCSOUTPUT");
    }

    /// <summary>Reads one report from the headset, report id included.</summary>
    /// <exception cref="TransportException">The read failed.</exception>
    public byte[] GetInput(byte reportId = HidControl.InReportId)
    {
        var buffer = new byte[InputLength];
        buffer[0] = reportId;
        Control(GetInputNumber, buffer, "HIDIOCGINPUT");
        return buffer;
    }

    public void Dispose() => _handle.Dispose();

    // -- reports -------------------------------------------------------------

    /// <summary>HIDIOCSOUTPUT and HIDIOCGINPUT's numbers, before the length goes in.</summary>
    private const int SendOutputNumber = 0x0B, GetInputNumber = 0x0A;

    /// <summary>Sends one report request, the report travelling in <paramref name="buffer"/> both ways.</summary>
    /// <remarks>
    /// The request is _IOC(_IOC_READ | _IOC_WRITE, 'H', number, length), as
    /// linux/hidraw.h builds it. The error is read straight after the call:
    /// anything else that reaches the system first, formatting a number
    /// included, can overwrite it.
    /// </remarks>
    /// <exception cref="TransportException">The request failed.</exception>
    private void Control(int number, byte[] buffer, string name)
    {
        nuint request = (3u << 30) | ((uint)buffer.Length << 16) | ('H' << 8) | (uint)number;
        bool added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
            if (Ioctl((int)_handle.DangerousGetHandle(), request, buffer) >= 0) return;
            int error = Marshal.GetLastPInvokeError();
            throw new TransportException($"{name} failed ({new Win32Exception(error).Message})");
        }
        finally
        {
            if (added) _handle.DangerousRelease();
        }
    }

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int Ioctl(int descriptor, nuint request, [In, Out] byte[] buffer);

    // -- enumeration ---------------------------------------------------------

    /// <summary>Every collection present from one vendor, on one usage page or, given null, on any.</summary>
    /// <remarks>
    /// A device whose descriptor cannot be read is reported through
    /// <paramref name="trouble"/> rather than left out in silence.
    /// </remarks>
    public static IReadOnlyList<HidDeviceInfo> ListDevices(
        ushort vendorId = HidControl.VendorId, ushort? usagePage = HidControl.UsagePage,
        Action<string>? trouble = null)
    {
        var found = new List<HidDeviceInfo>();
        if (!Directory.Exists(Devices)) return found;

        foreach (string folder in Directory.EnumerateDirectories(Devices))
        {
            string node = "/dev/" + System.IO.Path.GetFileName(folder);
            try
            {
                if (Hidraw.Ids(Hidraw.ReadAll(System.IO.Path.Combine(folder, "device", "uevent")))
                    is not { } ids || ids.Vendor != vendorId) continue;
                byte[] descriptor = Hidraw.ReadBytes(System.IO.Path.Combine(folder, "device", "report_descriptor"));
                foreach (var collection in ReportDescriptor.Collections(descriptor))
                    if (usagePage is null || collection.UsagePage == usagePage)
                        found.Add(new HidDeviceInfo(node, ids.Vendor, ids.Product, collection.UsagePage,
                            collection.OutputLength, collection.FeatureLength, collection.InputLength));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
            {
                trouble?.Invoke($"{node} could not be described: {ex.Message}");
            }
        }
        return found;
    }
}

/// <summary>What Linux's hidraw driver says about a device, read without opening it.</summary>
public static class Hidraw
{
    /// <summary>A whole file of the kernel's, read to its end.</summary>
    /// <remarks>
    /// Not File.ReadAllBytes: sysfs reports a file as 4096 bytes whatever its
    /// real length, a descriptor of 68 included, and a read that trusts the
    /// reported length stops short of it or fails.
    /// </remarks>
    public static byte[] ReadBytes(string path)
    {
        using var file = File.OpenRead(path);
        using var all = new MemoryStream();
        file.CopyTo(all);
        return all.ToArray();
    }

    /// <summary>A whole text file of the kernel's; see <see cref="ReadBytes"/>.</summary>
    public static string ReadAll(string path) => System.Text.Encoding.UTF8.GetString(ReadBytes(path));

    /// <summary>The vendor and product ids in a hidraw device's uevent, or null when it has none.</summary>
    /// <remarks>
    /// The line reads <c>HID_ID=0003:000010F5:0000229B</c>: the bus, then the
    /// vendor and product ids, each as eight hex digits.
    /// </remarks>
    public static (ushort Vendor, ushort Product)? Ids(string uevent)
    {
        foreach (string line in uevent.Split('\n'))
        {
            if (!line.StartsWith("HID_ID=", StringComparison.Ordinal)) continue;
            string[] parts = line["HID_ID=".Length..].Trim().Split(':');
            if (parts.Length == 3
                && uint.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint vendor)
                && uint.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint product)
                && vendor <= ushort.MaxValue && product <= ushort.MaxValue)
                return ((ushort)vendor, (ushort)product);
            throw new FormatException($"the uevent's HID_ID line does not read as ids: {line}");
        }
        return null;
    }
}
