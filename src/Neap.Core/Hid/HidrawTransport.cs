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
/// While the node is open, Linux also polls the interface's interrupt pipe
/// and queues what arrives there for a read. A device that answers there
/// rather than to GET_REPORT would look silent, so an empty GET_REPORT is
/// followed by a look at that queue; see <see cref="GetInput"/>.
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
    private bool _answersOnRequest;
    private int _queued;
    private readonly SortedSet<byte> _queuedIds = [];

    public string Path { get; }
    public ushort VendorId { get; }
    public ushort ProductId { get; }
    public ushort UsagePage { get; }
    public int OutputLength { get; }
    public int InputLength { get; }
    public string Name { get; }

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
        Name = Hidraw.ProductName(System.IO.Path.Combine(Devices, System.IO.Path.GetFileName(Path)));

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

    /// <remarks>
    /// Says what the interrupt pipe carried while the device gave GET_REPORT
    /// nothing, as that is the one sign of where a silent device's answers go.
    /// </remarks>
    public string Describe() =>
        $"0x{VendorId:x4}:0x{ProductId:x4} usage page 0x{UsagePage:x4} on {Path}"
        + (_queued == 0 ? "" : $", whose interrupt pipe carried {_queued} reports, report ids "
            + string.Join(", ", _queuedIds.Select(id => $"0x{id:x2}")));

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
    /// <remarks>
    /// <para>
    /// An empty GET_REPORT, nothing after the report id, is followed by a look
    /// at what the interrupt pipe has queued, and a queued report with the
    /// same id is returned in its place. Reports with other ids, such as the
    /// volume keys', are dropped, as nothing else reads this node.
    /// </para>
    /// <para>
    /// Once GET_REPORT has answered, the queue is left alone, so a device that
    /// answers there, as the Stealth Pro II does, cannot have a reply read
    /// twice.
    /// </para>
    /// </remarks>
    /// <exception cref="TransportException">The read failed.</exception>
    public byte[] GetInput(byte reportId = HidControl.InReportId)
    {
        var buffer = new byte[InputLength];
        buffer[0] = reportId;
        Control(GetInputNumber, buffer, "HIDIOCGINPUT");
        if (buffer.AsSpan(1).ContainsAnyExcept((byte)0)) _answersOnRequest = true;
        else if (!_answersOnRequest && Queued(reportId) is { } queued) return queued;
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

    // -- the interrupt pipe's queue ------------------------------------------

    /// <summary>The first report with <paramref name="reportId"/> waiting on the interrupt pipe, or null when none is.</summary>
    /// <remarks>Never waits: poll with no timeout says whether a read would block.</remarks>
    /// <exception cref="TransportException">The poll or read failed.</exception>
    private byte[]? Queued(byte reportId)
    {
        bool added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
            int descriptor = (int)_handle.DangerousGetHandle();
            var buffer = new byte[InputLength];
            while (true)
            {
                var poll = new PollDescriptor { Descriptor = descriptor, Events = PollIn };
                int ready = Poll(ref poll, 1, 0);
                if (ready < 0)
                    throw new TransportException($"poll failed ({new Win32Exception(Marshal.GetLastPInvokeError()).Message})");
                if (ready == 0 || (poll.Returned & PollIn) == 0) return null;

                Array.Clear(buffer);
                nint read = Read(descriptor, buffer, buffer.Length);
                if (read < 0)
                    throw new TransportException($"read failed ({new Win32Exception(Marshal.GetLastPInvokeError()).Message})");
                if (read == 0) return null;
                _queued++;
                _queuedIds.Add(buffer[0]);
                if (buffer[0] == reportId) return buffer;
            }
        }
        finally
        {
            if (added) _handle.DangerousRelease();
        }
    }

    private const short PollIn = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct PollDescriptor
    {
        public int Descriptor;
        public short Events;
        public short Returned;
    }

    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static extern int Poll(ref PollDescriptor descriptor, nuint count, int timeout);

    [DllImport("libc", EntryPoint = "read", SetLastError = true)]
    private static extern nint Read(int descriptor, [Out] byte[] buffer, nint count);

    // -- asking the transmitter itself --------------------------------------

    /// <summary>Asks a transmitter, with <see cref="LinkInquiry"/>, whether the headset is linked to it.</summary>
    /// <remarks>
    /// The transmitter answers this itself, so an answer at all says its
    /// control channel works, whatever the headset is doing.
    /// </remarks>
    /// <returns>Its answer, or null when it gave none in time.</returns>
    /// <exception cref="AccessDeniedException">The system does not let this user open the device.</exception>
    /// <exception cref="TransportException">The device could not be opened, written or read.</exception>
    public static bool? AskLinked(HidDeviceInfo device, TimeSpan wait)
    {
        using var hid = new HidrawTransport(device);
        hid.SendOutput(LinkInquiry.Frame);
        long until = Environment.TickCount64 + (long)wait.TotalMilliseconds;
        while (Environment.TickCount64 < until)
        {
            if (LinkInquiry.Linked(hid.GetInput()) is { } linked) return linked;
            Thread.Sleep(4);
        }
        return null;
    }

    // -- restarting ----------------------------------------------------------

    /// <summary>
    /// Resets the USB device behind a hidraw node, as if it were unplugged and
    /// plugged back in, while reading it all the way through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Turtle Beach's transmitters of this family answer only if something has
    /// been reading report 7 since they were plugged in, and give empty
    /// reports for ever otherwise. Proven on the Stealth Pro II's Charging
    /// Dock in SP2Tools: reading from the moment it is plugged in again puts
    /// it right, and its replies then last through closing and reopening it.
    /// On Linux a transmitter is often plugged in long before Neap starts.
    /// </para>
    /// <para>
    /// The reset is USBDEVFS_RESET on the device's usbfs node. The hid and
    /// audio drivers stay bound through it, so the hidraw node and the handle
    /// on it stay too, and reading carries on across it on another thread.
    /// Reads that fail while the device is resetting are expected and skipped.
    /// </para>
    /// </remarks>
    /// <param name="device">The silent device, as listed.</param>
    /// <param name="listen">How long to go on reading after the reset.</param>
    /// <returns>
    /// The reports that carried a frame, in hex, report id first and trailing
    /// zeros left off. A report whose length byte is zero is empty, whatever is
    /// left after it in the buffer from an earlier reply.
    /// </returns>
    /// <exception cref="AccessDeniedException">The system does not let this user reset the device.</exception>
    /// <exception cref="TransportException">The device could not be found or reset.</exception>
    public static IReadOnlyList<string> Restart(HidDeviceInfo device, TimeSpan listen)
    {
        string node = UsbNode(device.Path);
        using var hid = new HidrawTransport(device);
        SafeFileHandle usb;
        try
        {
            usb = File.OpenHandle(node, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        }
        catch (UnauthorizedAccessException)
        {
            throw new AccessDeniedException($"no permission to reset {node}; the udev rule has to cover it");
        }
        catch (IOException ex)
        {
            throw new TransportException($"could not open {node} ({ex.Message})");
        }

        var heard = new List<string>();
        using var done = new ManualResetEventSlim();
        var reader = new Thread(() =>
        {
            while (!done.IsSet)
            {
                try
                {
                    var report = hid.GetInput();
                    int end = report.AsSpan().LastIndexOfAnyExcept((byte)0) + 1;
                    if (report.Length > 1 && report[1] > 0) heard.Add(Convert.ToHexString(report, 0, end));
                }
                catch (TransportException)
                {
                    // Resetting: the device is away for a moment.
                }
                done.Wait(4);
            }
        })
        { IsBackground = true, Name = "transmitter restart" };
        reader.Start();

        try
        {
            using (usb)
            {
                bool added = false;
                try
                {
                    usb.DangerousAddRef(ref added);
                    if (IoctlNone((int)usb.DangerousGetHandle(), UsbReset, IntPtr.Zero) < 0)
                        throw new TransportException(
                            $"USBDEVFS_RESET on {node} failed ({new Win32Exception(Marshal.GetLastPInvokeError()).Message})");
                }
                finally
                {
                    if (added) usb.DangerousRelease();
                }
            }
            Thread.Sleep(listen);
        }
        finally
        {
            done.Set();
            reader.Join();
        }
        return heard;
    }

    /// <summary>USBDEVFS_RESET, _IO('U', 20).</summary>
    private const nuint UsbReset = ('U' << 8) | 20;

    /// <summary>The usbfs node of the USB device a hidraw node belongs to, such as /dev/bus/usb/005/003.</summary>
    /// <remarks>
    /// The hidraw node's device is the HID device, whose parent is the USB
    /// interface, whose parent is the USB device with its bus and device
    /// numbers.
    /// </remarks>
    /// <exception cref="TransportException">The USB device could not be found.</exception>
    private static string UsbNode(string hidrawPath)
    {
        string hid = RealPath(System.IO.Path.Combine(Devices, System.IO.Path.GetFileName(hidrawPath), "device"));
        string usb = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(hid) ?? "") ?? "";
        try
        {
            int bus = int.Parse(Hidraw.ReadAll(System.IO.Path.Combine(usb, "busnum")).Trim(), CultureInfo.InvariantCulture);
            int number = int.Parse(Hidraw.ReadAll(System.IO.Path.Combine(usb, "devnum")).Trim(), CultureInfo.InvariantCulture);
            return $"/dev/bus/usb/{bus:000}/{number:000}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            throw new TransportException($"could not find the USB device behind {hidrawPath} at {usb} ({ex.Message})");
        }
    }

    /// <summary>A path with every link in it followed, as realpath(3) gives it.</summary>
    /// <exception cref="TransportException">The path could not be followed.</exception>
    private static string RealPath(string path)
    {
        IntPtr resolved = RealPathNative(System.Text.Encoding.UTF8.GetBytes(path + " "), IntPtr.Zero);
        if (resolved == IntPtr.Zero)
            throw new TransportException($"could not follow {path} ({new Win32Exception(Marshal.GetLastPInvokeError()).Message})");
        try { return Marshal.PtrToStringUTF8(resolved) ?? ""; }
        finally { Free(resolved); }
    }

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int IoctlNone(int descriptor, nuint request, IntPtr argument);

    [DllImport("libc", EntryPoint = "realpath", SetLastError = true)]
    private static extern IntPtr RealPathNative(byte[] path, IntPtr resolved);

    [DllImport("libc", EntryPoint = "free")]
    private static extern void Free(IntPtr pointer);

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

    /// <summary>The USB device's product string, or empty when it has none.</summary>
    /// <param name="folder">The hidraw node's folder in sysfs, such as /sys/class/hidraw/hidraw0.</param>
    /// <remarks>
    /// The folder's device link is the HID device. Two levels up, past the
    /// interface, is the USB device, which holds the string. The folder is a
    /// link too, and a link's target is taken from the path as spelled, so the
    /// folder is resolved before the device link that climbs out of it.
    /// </remarks>
    public static string ProductName(string folder)
    {
        try
        {
            string real = Directory.ResolveLinkTarget(folder, returnFinalTarget: true)?.FullName ?? folder;
            if (Directory.ResolveLinkTarget(System.IO.Path.Combine(real, "device"), returnFinalTarget: true)?.FullName is not { } hid
                || new DirectoryInfo(hid).Parent?.Parent is not { } usb) return "";
            string product = System.IO.Path.Combine(usb.FullName, "product");
            return File.Exists(product) ? ReadAll(product).Trim() : "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

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
