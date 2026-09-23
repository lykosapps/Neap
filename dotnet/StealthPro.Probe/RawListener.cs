using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using StealthPro.Core.Hid;

namespace StealthPro.Probe;

/// <summary>
/// Every report a collection produces, by both of the ways a report can arrive.
///
/// <b>Asking and listening are different paths, and each is blind to what the
/// other sees.</b> The settings channel is read by asking — a GET_REPORT for
/// report 0x07 — and that is how the rest of this project talks to it. The
/// wheels do not answer questions at all: they push input reports down the
/// interrupt pipe, and only a read on the handle receives those. The first
/// version of this listener only asked, was pointed at the wheel collection,
/// heard nothing while the wheel was being turned, and very nearly turned that
/// into a finding.
///
/// So it does both at once and says which path saw each report. A path that
/// cannot work on a collection says so, once, rather than falling quiet —
/// quiet is exactly what it is here to tell apart from "nothing sent".
///
/// It does not ask the device to prove it has the headset first, unlike every
/// other command in the probe. The state where you most want to know what is
/// arriving is the state where nothing is answering.
///
/// Read-only: nothing is sent to the device except the GET_REPORT request
/// itself.
/// </summary>
internal static class RawListener
{
    private const uint GenericRead = 0x80000000;
    private const uint ShareReadWrite = 0x03;
    private const uint OpenExisting = 3;
    private const uint FlagOverlapped = 0x40000000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);

    public static int Run(double seconds, ushort usagePage)
    {
        var candidates = HidTransport.Candidates(usagePage: usagePage);
        if (candidates.Count == 0)
        {
            Console.WriteLine($"nothing of the headset's is plugged in on usage page 0x{usagePage:x4}");
            return 1;
        }

        var device = candidates[0];
        Console.WriteLine($"listening to 0x{device.VendorId:x4}:0x{device.ProductId:x4} "
                        + $"usage page 0x{usagePage:x4} for {seconds}s — press buttons, turn wheels");
        if (candidates.Count > 1)
            Console.WriteLine($"({candidates.Count} devices present; this is the first. Others: "
                + string.Join(", ", candidates.Skip(1).Select(c => $"0x{c.ProductId:x4}")) + ")");

        var clock = Stopwatch.StartNew();
        var gate = new object();
        var printer = new Printer(clock, gate);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));

        var pushed = Task.Run(() => Listen(device, printer, stop.Token));
        Ask(device, printer, stop.Token);
        pushed.Wait();

        printer.Flush();
        lock (gate)
        {
            Console.WriteLine();
            Console.WriteLine($"pushed: {printer.Count("pushed")} reports   "
                            + $"asked: {printer.Count("asked")} with content");
        }
        return 0;
    }

    /// <summary>What the device sends without being asked: reads on the handle.</summary>
    private static void Listen(HidDeviceInfo device, Printer printer, CancellationToken stop)
    {
        using var handle = CreateFileW(device.Path, GenericRead, ShareReadWrite,
            IntPtr.Zero, OpenExisting, FlagOverlapped, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            printer.Note("pushed", "could not open for listening (error "
                + Marshal.GetLastWin32Error() + ") — this path is blind here, not quiet");
            return;
        }

        // A read shorter than the collection's input report length fails
        // outright, so the buffer is sized from what the device declared.
        int length = Math.Max(device.InputLength, 1);
        using var stream = new FileStream(handle, FileAccess.Read, 1, isAsync: true);
        var buffer = new byte[length];
        while (!stop.IsCancellationRequested)
        {
            int read;
            try { read = stream.ReadAsync(buffer, stop).AsTask().GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { return; }
            catch (IOException ex)
            {
                printer.Note("pushed", "listening failed (" + ex.Message + ") — this path is blind here, not quiet");
                return;
            }
            if (read > 0) printer.Report("pushed", buffer.AsSpan(0, read).ToArray());
        }
    }

    /// <summary>What the device answers when asked: GET_REPORT, as the app does.</summary>
    private static void Ask(HidDeviceInfo device, Printer printer, CancellationToken stop)
    {
        HidTransport transport;
        try { transport = new HidTransport(device.Path); }
        catch (Exception ex)
        {
            printer.Note("asked", "could not open (" + ex.Message + ")");
            stop.WaitHandle.WaitOne();
            return;
        }

        using (transport)
        {
            int failures = 0;
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    var report = transport.GetInput();
                    failures = 0;
                    if (!report.Skip(1).All(b => b == 0)) printer.Report("asked", report);
                }
                catch (TransportException ex)
                {
                    // The wheel collection has no report 0x07 to ask for. Say
                    // that once and stop, rather than retrying into silence.
                    if (++failures == 20)
                    {
                        printer.Note("asked", "this collection does not answer requests ("
                            + ex.Message + ") — only the pushed path can see it");
                        stop.WaitHandle.WaitOne();
                        return;
                    }
                }
                Thread.Sleep(5);
            }
        }
    }

    /// <summary>
    /// Prints reports as they arrive, collapsing runs of identical ones — an
    /// idle device repeats the same report thousands of times.
    /// </summary>
    private sealed class Printer(Stopwatch clock, object gate)
    {
        private readonly Dictionary<string, (string Last, int Repeats, int Total)> _paths = new();

        public int Count(string path) =>
            _paths.TryGetValue(path, out var p) ? p.Total : 0;

        public void Note(string path, string text)
        {
            lock (gate) Console.WriteLine($"  [{clock.Elapsed.TotalSeconds,6:0.00}s] {path,-6} {text}");
        }

        public void Report(string path, byte[] report)
        {
            lock (gate)
            {
                string hex = Convert.ToHexString(report).ToLowerInvariant();
                _paths.TryGetValue(path, out var p);
                p.Total++;
                if (hex == p.Last)
                {
                    p.Repeats++;
                    _paths[path] = p;
                    return;
                }
                if (p.Repeats > 0) Console.WriteLine($"      {path} (x{p.Repeats + 1})");
                p.Last = hex;
                p.Repeats = 0;
                _paths[path] = p;

                string text = new(report.Select(b => b is >= 32 and < 127 ? (char)b : '.').ToArray());
                Console.WriteLine($"  [{clock.Elapsed.TotalSeconds,6:0.00}s] {path,-6} {hex}");
                Console.WriteLine($"                        {text}");
            }
        }

        public void Flush()
        {
            lock (gate)
                foreach (var (path, p) in _paths)
                    if (p.Repeats > 0) Console.WriteLine($"      {path} (x{p.Repeats + 1})");
        }
    }
}
