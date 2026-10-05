using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Neap.Core.Audio.Pulse.PulseNative;

namespace Neap.Core.Audio.Pulse;

/// <summary>An output, as the sound server describes it.</summary>
/// <param name="Index">The server's number for it, which lasts only as long as the device is plugged in.</param>
/// <param name="Name">The server's name for it, which stays the same across restarts.</param>
/// <param name="Description">What the person sees it called.</param>
/// <param name="Vendor">The USB vendor id behind it as four hex digits, or empty when it is not USB.</param>
/// <param name="Product">The USB product id behind it as four hex digits, or empty when it is not USB.</param>
internal sealed record PulseSink(uint Index, string Name, string Description, string Vendor, string Product)
{
    /// <summary>Whether it is one of the headset's outputs: the headset itself, a transmitter or a dock.</summary>
    /// <remarks>
    /// By the USB ids behind it rather than its name, which the sound server
    /// builds from the device and the desktop may rename.
    /// </remarks>
    internal bool IsHeadset =>
        Vendor == "10F5" && Transmitters.PieceOf(Product) != Transmitters.Piece.Unknown;

    /// <summary>A USB id as four upper-case hex digits; PipeWire writes "0x10f5" where PulseAudio writes "10f5".</summary>
    internal static string UsbId(string id)
    {
        string digits = id.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? id[2..] : id;
        return digits.Length == 4 ? digits.ToUpperInvariant() : "";
    }
}

/// <summary>One application's stream to one output.</summary>
/// <param name="Index">The server's number for it, which lasts only as long as the stream.</param>
/// <param name="Sink">The index of the output it plays to.</param>
/// <param name="Program">The program behind it: its file's name, or failing that the name it gives itself.</param>
/// <param name="ProcessId">Its process id as it reports it, or 0 when it does not.</param>
/// <param name="Volume">Its volume as a linear gain, the average of its channels.</param>
/// <param name="Channels">How many channels it has, each of which is set to the same volume.</param>
/// <param name="Playing">Whether it is playing rather than paused.</param>
/// <param name="Name">The name it gives itself, which is what a person would recognise; empty when it gives none.</param>
internal sealed record PulseStream(
    uint Index, uint Sink, string Program, uint ProcessId, float Volume, byte Channels, bool Playing, string Name);

/// <summary>
/// A connection to the sound server: PulseAudio, or PipeWire through its
/// PulseAudio server, which is what SteamOS and most desktops run.
/// </summary>
/// <remarks>
/// <para>
/// Synchronous. Each call runs the library's main loop on the caller's thread
/// until its answer is in, for at most <see cref="Patience"/>, so a server
/// that stops answering costs a pass of the mix rather than a stuck thread.
/// A connection that drops is made again on the next call.
/// </para>
/// <para>
/// A volume is read and set as a linear gain, the same scale as Windows'
/// session volume, so the mix's levels mean the same on both. The server's
/// own percentage, which desktop mixers show, is cubic: a gain of 0.5 shows
/// as 79%.
/// </para>
/// <para>
/// The program behind a stream is named from its properties rather than by
/// looking its process up. An application in a Flatpak sandbox reports a
/// process id from inside the sandbox, which names some other process or
/// none outside it.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed class PulseClient : IDisposable
{
    /// <summary>The longest a single request is waited for.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private IntPtr _mainloop;
    private IntPtr _context;

    // Held for as long as the library might call them.
    private readonly ServerInfoCallback _onServer;
    private readonly InfoListCallback _onSink;
    private readonly InfoListCallback _onStream;
    private readonly SuccessCallback _onSuccess;

    private string _defaultSink = "";
    private readonly List<PulseSink> _sinks = new();
    private readonly List<PulseStream> _streams = new();
    private bool _succeeded;

    internal PulseClient()
    {
        _onServer = OnServer;
        _onSink = OnSink;
        _onStream = OnStream;
        _onSuccess = OnSuccess;
    }

    /// <summary>The name of the output sound goes to unless an application says otherwise.</summary>
    /// <exception cref="PulseException">The server could not be reached or did not answer.</exception>
    internal string DefaultSink()
    {
        lock (_gate)
        {
            Run(c => ContextGetServerInfo(c, _onServer, IntPtr.Zero), "the server's defaults");
            return _defaultSink;
        }
    }

    /// <summary>Every output.</summary>
    /// <exception cref="PulseException">The server could not be reached or did not answer.</exception>
    internal IReadOnlyList<PulseSink> Sinks()
    {
        lock (_gate)
        {
            _sinks.Clear();
            Run(c => ContextGetSinkInfoList(c, _onSink, IntPtr.Zero), "the outputs");
            return _sinks.ToList();
        }
    }

    /// <summary>Every application stream whose volume can be set.</summary>
    /// <exception cref="PulseException">The server could not be reached or did not answer.</exception>
    internal IReadOnlyList<PulseStream> Streams()
    {
        lock (_gate)
        {
            _streams.Clear();
            Run(c => ContextGetSinkInputInfoList(c, _onStream, IntPtr.Zero), "the applications playing");
            return _streams.ToList();
        }
    }

    /// <summary>Sets every channel of one stream to a linear gain.</summary>
    /// <exception cref="PulseException">The server could not be reached, or refused.</exception>
    internal void SetVolume(PulseStream stream, float volume)
    {
        var volumes = new ChannelVolumes { Channels = stream.Channels, Values = new uint[ChannelsMax] };
        uint value = VolumeFromLinear(Math.Max(0f, volume));
        for (int i = 0; i < stream.Channels; i++) volumes.Values[i] = value;

        lock (_gate)
        {
            _succeeded = false;
            Connect();
            Await(ContextSetSinkInputVolume(_context, stream.Index, ref volumes, _onSuccess, IntPtr.Zero),
                $"{stream.Program}'s volume");
            if (!_succeeded) throw new PulseException($"the server would not set {stream.Program}'s volume");
        }
    }

    public void Dispose()
    {
        lock (_gate) Disconnect();
    }

    // -- the main loop --------------------------------------------------------

    /// <summary>Starts one request and runs the main loop until it is answered.</summary>
    private void Run(Func<IntPtr, IntPtr> start, string what)
    {
        Connect();
        Await(start(_context), what);
    }

    /// <summary>Runs the main loop until a request is answered.</summary>
    private void Await(IntPtr operation, string what)
    {
        if (operation == IntPtr.Zero) throw Failure($"could not ask for {what}");
        try
        {
            var clock = Stopwatch.StartNew();
            while (OperationGetState(operation) == OperationRunning)
            {
                if (clock.Elapsed > Patience)
                {
                    OperationCancel(operation);
                    throw new PulseException($"the sound server did not answer about {what}");
                }
                Iterate();
                if (ContextGetState(_context) != ContextReady) throw Failure($"lost the sound server asking about {what}");
            }
        }
        finally
        {
            OperationUnref(operation);
        }
    }

    private void Connect()
    {
        if (_context != IntPtr.Zero && ContextGetState(_context) == ContextReady) return;
        Disconnect();

        _mainloop = MainloopNew();
        _context = ContextNew(MainloopGetApi(_mainloop), Utf8("Neap"));
        if (_context == IntPtr.Zero || ContextConnect(_context, IntPtr.Zero, NoAutospawn, IntPtr.Zero) < 0)
            throw Failure("could not reach the sound server");

        var clock = Stopwatch.StartNew();
        while (true)
        {
            int state = ContextGetState(_context);
            if (state == ContextReady) return;
            if (state is ContextFailed or ContextTerminated) throw Failure("the sound server turned the connection down");
            if (clock.Elapsed > Patience) throw new PulseException("the sound server did not answer");
            Iterate();
        }
    }

    /// <summary>Waits a little for the server, and handles whatever it sent.</summary>
    private void Iterate()
    {
        if (MainloopPrepare(_mainloop, 100_000) < 0 || MainloopPoll(_mainloop) < 0 || MainloopDispatch(_mainloop) < 0)
            throw new PulseException("the sound server's main loop stopped");
    }

    private void Disconnect()
    {
        if (_context != IntPtr.Zero)
        {
            ContextDisconnect(_context);
            ContextUnref(_context);
            _context = IntPtr.Zero;
        }
        if (_mainloop != IntPtr.Zero)
        {
            MainloopFree(_mainloop);
            _mainloop = IntPtr.Zero;
        }
    }

    private PulseException Failure(string what)
    {
        string reason = _context == IntPtr.Zero ? "" : Text(StrError(ContextErrno(_context)));
        return new PulseException(reason.Length == 0 ? what : $"{what}: {reason}");
    }

    // -- answers ----------------------------------------------------------------

    private void OnServer(IntPtr context, IntPtr info, IntPtr userdata)
    {
        _defaultSink = info == IntPtr.Zero ? "" : Text(Marshal.PtrToStructure<ServerInfo>(info).DefaultSinkName);
    }

    private void OnSink(IntPtr context, IntPtr info, int eol, IntPtr userdata)
    {
        if (eol != 0 || info == IntPtr.Zero) return;
        var sink = Marshal.PtrToStructure<SinkInfo>(info);
        _sinks.Add(new PulseSink(sink.Index, Text(sink.Name), Text(sink.Description),
            PulseSink.UsbId(Property(sink.Proplist, "device.vendor.id")),
            PulseSink.UsbId(Property(sink.Proplist, "device.product.id"))));
    }

    private void OnStream(IntPtr context, IntPtr info, int eol, IntPtr userdata)
    {
        if (eol != 0 || info == IntPtr.Zero) return;
        var stream = Marshal.PtrToStructure<SinkInputInfo>(info);
        if (stream.HasVolume == 0 || stream.VolumeWritable == 0 || stream.Volume.Channels == 0) return;

        string program = Property(stream.Proplist, "application.process.binary");
        if (program.Length == 0) program = Property(stream.Proplist, "application.name");
        _ = uint.TryParse(Property(stream.Proplist, "application.process.id"), NumberStyles.None, CultureInfo.InvariantCulture, out uint pid);

        double sum = 0;
        for (int i = 0; i < stream.Volume.Channels; i++) sum += VolumeToLinear(stream.Volume.Values[i]);
        _streams.Add(new PulseStream(stream.Index, stream.Sink, program, pid,
            (float)(sum / stream.Volume.Channels), stream.Volume.Channels, stream.Corked == 0,
            Property(stream.Proplist, "application.name")));
    }

    private void OnSuccess(IntPtr context, int success, IntPtr userdata) => _succeeded = success != 0;

}

public class PulseException : Exception
{
    public PulseException(string message) : base(message) { }
}
