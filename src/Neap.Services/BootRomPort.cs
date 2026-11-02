using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using Microsoft.Win32;
using Neap.Core.Firmware;

namespace Neap.Services;

/// <summary>Finds the headset chip's download-mode port and opens it, handshake and all.</summary>
/// <remarks>
/// <para>
/// The port is there for about half a second after the chip powers up unless
/// it is greeted at once (SP2Tools F290), so it is looked for every 20
/// milliseconds, from before the cable goes in, and greeted the moment it
/// appears. A port that does not answer is let go and looked for again.
/// </para>
/// <para>
/// The port is found by the chip's USB ids. On Windows they are in the
/// registry beside the port's name, which is quick to read; on Linux the
/// <c>ttyACM</c> device's USB parent names them.
/// </para>
/// </remarks>
public static class BootRomPort
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(20);
    private static readonly string Ids = string.Create(CultureInfo.InvariantCulture, $"VID_{BootRom.VendorId:X4}&PID_{BootRom.ProductId:X4}");

    /// <summary>Waits for the chip in download mode and greets it.</summary>
    /// <exception cref="OperationCanceledException">The wait was stopped.</exception>
    public static async Task<BootRom> Open(CancellationToken cancel)
    {
        while (true)
        {
            cancel.ThrowIfCancellationRequested();
            if (Find() is { } name && Greet(name) is { } found) return found;
            await Task.Delay(Poll, cancel);
        }
    }

    /// <summary>Whether the download-mode port is there now.</summary>
    public static bool Present => Find() is not null;

    /// <summary>The name of the download-mode port that is there now, or null.</summary>
    public static string? Find()
    {
        var present = SerialPort.GetPortNames();
        return Candidates().FirstOrDefault(present.Contains);
    }

    private static IEnumerable<string> Candidates()
    {
        if (OperatingSystem.IsWindows())
        {
            using var device = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\USB\{Ids}");
            if (device is null) yield break;
            foreach (string instance in device.GetSubKeyNames())
            {
                using var parameters = device.OpenSubKey($@"{instance}\Device Parameters");
                if (parameters?.GetValue("PortName") is string port) yield return port;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            string vendor = BootRom.VendorId.ToString("x4", CultureInfo.InvariantCulture);
            string product = BootRom.ProductId.ToString("x4", CultureInfo.InvariantCulture);
            foreach (string tty in Directory.EnumerateDirectories("/sys/class/tty", "ttyACM*"))
            {
                string usb = Path.Combine(tty, "device", "..");
                if (Read(Path.Combine(usb, "idVendor")) == vendor && Read(Path.Combine(usb, "idProduct")) == product)
                    yield return "/dev/" + Path.GetFileName(tty);
            }
        }
    }

    private static string? Read(string file) => File.Exists(file) ? File.ReadAllText(file).Trim() : null;

    private static BootRom? Greet(string name)
    {
        SerialLink? link = null;
        try
        {
            link = new SerialLink(name);
            var chip = new BootRom(link);
            if (chip.Handshake()) return chip;
            AppLog.Write($"firmware: {name} did not answer as the chip's download mode does");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            AppLog.Write($"firmware: could not open {name}: {ex.Message}");
        }
        link?.Dispose();
        return null;
    }

    /// <summary>The serial port, read a byte at a time as the bytes come.</summary>
    private sealed class SerialLink : IBootRomLink
    {
        private readonly SerialPort _port;

        public SerialLink(string name)
        {
            _port = new SerialPort(name, BootRom.BaudRate) { ReadTimeout = 20, WriteTimeout = 2000 };
            _port.Open();
        }

        public void Write(ReadOnlySpan<byte> bytes) => _port.BaseStream.Write(bytes);

        public byte[] Read(int count, TimeSpan quiet)
        {
            var bytes = new byte[count];
            int got = 0;
            var since = Stopwatch.StartNew();
            while (got < count && since.Elapsed < quiet)
            {
                try
                {
                    int read = _port.Read(bytes, got, count - got);
                    if (read > 0) since.Restart();
                    got += read;
                }
                catch (TimeoutException)
                {
                }
            }
            return bytes[..got];
        }

        public void Dispose() => _port.Dispose();
    }
}
