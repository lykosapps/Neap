using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Neap.Core.Audio.Pulse;

/// <summary>
/// The part of libpulse needed to list outputs and the applications playing
/// to them, and to set an application's volume.
/// </summary>
/// <remarks>
/// <see cref="PulseClient"/> documents how these are used. The structures
/// are declared only as far as the last field read; the library owns them
/// and they are never allocated here.
/// </remarks>
[SupportedOSPlatform("linux")]
internal static class PulseNative
{
    private const string Library = "libpulse.so.0";

    private static readonly Lazy<bool> MainLibrary = new(() => Loadable(Library));
    private static readonly Lazy<bool> SimpleLibraryPresent = new(() => Loadable(SimpleLibrary));

    private static bool Loadable(string name)
    {
        if (!NativeLibrary.TryLoad(name, out var handle)) return false;
        NativeLibrary.Free(handle);
        return true;
    }

    /// <summary>Checks the sound system's library is installed, so its absence is said in words and not as a crash.</summary>
    /// <exception cref="PulseException">The library is not on this system.</exception>
    internal static void Require()
    {
        if (!MainLibrary.Value)
            throw new PulseException($"the sound system's library ({Library}) is not installed, so Neap cannot see or set sound levels");
    }

    /// <summary>Checks the library that plays and records sound is installed as well.</summary>
    /// <exception cref="PulseException">Either library is not on this system.</exception>
    internal static void RequireSimple()
    {
        Require();
        if (!SimpleLibraryPresent.Value)
            throw new PulseException($"the sound system's library ({SimpleLibrary}) is not installed, so Neap cannot play or record sound");
    }

    internal const int ContextReady = 4, ContextFailed = 5, ContextTerminated = 6;
    internal const int OperationRunning = 0;

    /// <summary>PA_CONTEXT_NOAUTOSPAWN: never start a sound server that is not already running.</summary>
    internal const int NoAutospawn = 1;

    internal const int ChannelsMax = 32;

    /// <summary>PA_VOLUME_NORM: a volume of 100% as the desktop's mixer shows it.</summary>
    internal const uint NormalVolume = 0x10000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct SampleSpec
    {
        public int Format;
        public uint Rate;
        public byte Channels;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ChannelMap
    {
        public byte Channels;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ChannelsMax)]
        public int[] Map;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ChannelVolumes
    {
        public byte Channels;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ChannelsMax)]
        public uint[] Values;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ServerInfo
    {
        public IntPtr UserName;
        public IntPtr HostName;
        public IntPtr ServerVersion;
        public IntPtr ServerName;
        public SampleSpec SampleSpec;
        public IntPtr DefaultSinkName;
        public IntPtr DefaultSourceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SinkInfo
    {
        public IntPtr Name;
        public uint Index;
        public IntPtr Description;
        public SampleSpec SampleSpec;
        public ChannelMap ChannelMap;
        public uint OwnerModule;
        public ChannelVolumes Volume;
        public int Mute;
        public uint MonitorSource;
        public IntPtr MonitorSourceName;
        public ulong Latency;
        public IntPtr Driver;
        public int Flags;
        public IntPtr Proplist;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SinkInputInfo
    {
        public uint Index;
        public IntPtr Name;
        public uint OwnerModule;
        public uint Client;
        public uint Sink;
        public SampleSpec SampleSpec;
        public ChannelMap ChannelMap;
        public ChannelVolumes Volume;
        public ulong BufferUsec;
        public ulong SinkUsec;
        public IntPtr ResampleMethod;
        public IntPtr Driver;
        public int Mute;
        public IntPtr Proplist;
        public int Corked;
        public int HasVolume;
        public int VolumeWritable;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void ServerInfoCallback(IntPtr context, IntPtr info, IntPtr userdata);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void InfoListCallback(IntPtr context, IntPtr info, int eol, IntPtr userdata);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void SuccessCallback(IntPtr context, int success, IntPtr userdata);

    [DllImport(Library, EntryPoint = "pa_mainloop_new")]
    internal static extern IntPtr MainloopNew();

    [DllImport(Library, EntryPoint = "pa_mainloop_get_api")]
    internal static extern IntPtr MainloopGetApi(IntPtr mainloop);

    [DllImport(Library, EntryPoint = "pa_mainloop_iterate")]
    internal static extern int MainloopIterate(IntPtr mainloop, int block, IntPtr result);

    [DllImport(Library, EntryPoint = "pa_mainloop_prepare")]
    internal static extern int MainloopPrepare(IntPtr mainloop, int timeoutMicroseconds);

    [DllImport(Library, EntryPoint = "pa_mainloop_poll")]
    internal static extern int MainloopPoll(IntPtr mainloop);

    [DllImport(Library, EntryPoint = "pa_mainloop_dispatch")]
    internal static extern int MainloopDispatch(IntPtr mainloop);

    [DllImport(Library, EntryPoint = "pa_mainloop_free")]
    internal static extern void MainloopFree(IntPtr mainloop);

    [DllImport(Library, EntryPoint = "pa_context_new")]
    internal static extern IntPtr ContextNew(IntPtr api, byte[] name);

    [DllImport(Library, EntryPoint = "pa_context_connect")]
    internal static extern int ContextConnect(IntPtr context, IntPtr server, int flags, IntPtr spawnApi);

    [DllImport(Library, EntryPoint = "pa_context_get_state")]
    internal static extern int ContextGetState(IntPtr context);

    [DllImport(Library, EntryPoint = "pa_context_errno")]
    internal static extern int ContextErrno(IntPtr context);

    [DllImport(Library, EntryPoint = "pa_context_disconnect")]
    internal static extern void ContextDisconnect(IntPtr context);

    [DllImport(Library, EntryPoint = "pa_context_unref")]
    internal static extern void ContextUnref(IntPtr context);

    [DllImport(Library, EntryPoint = "pa_context_get_server_info")]
    internal static extern IntPtr ContextGetServerInfo(IntPtr context, ServerInfoCallback callback, IntPtr userdata);

    [DllImport(Library, EntryPoint = "pa_context_get_sink_info_list")]
    internal static extern IntPtr ContextGetSinkInfoList(IntPtr context, InfoListCallback callback, IntPtr userdata);

    [DllImport(Library, EntryPoint = "pa_context_get_source_info_list")]
    internal static extern IntPtr ContextGetSourceInfoList(IntPtr context, InfoListCallback callback, IntPtr userdata);

    [DllImport(Library, EntryPoint = "pa_context_set_sink_volume_by_name")]
    internal static extern IntPtr ContextSetSinkVolumeByName(
        IntPtr context, byte[] name, ref ChannelVolumes volume, SuccessCallback callback, IntPtr userdata);

    [DllImport(Library, EntryPoint = "pa_context_set_source_volume_by_name")]
    internal static extern IntPtr ContextSetSourceVolumeByName(
        IntPtr context, byte[] name, ref ChannelVolumes volume, SuccessCallback callback, IntPtr userdata);

    [DllImport(Library, EntryPoint = "pa_context_set_sink_mute_by_name")]
    internal static extern IntPtr ContextSetSinkMuteByName(
        IntPtr context, byte[] name, int mute, SuccessCallback callback, IntPtr userdata);

    [DllImport(Library, EntryPoint = "pa_context_set_source_mute_by_name")]
    internal static extern IntPtr ContextSetSourceMuteByName(
        IntPtr context, byte[] name, int mute, SuccessCallback callback, IntPtr userdata);

    [DllImport(Library, EntryPoint = "pa_context_get_sink_input_info_list")]
    internal static extern IntPtr ContextGetSinkInputInfoList(IntPtr context, InfoListCallback callback, IntPtr userdata);

    [DllImport(Library, EntryPoint = "pa_context_set_sink_input_volume")]
    internal static extern IntPtr ContextSetSinkInputVolume(
        IntPtr context, uint index, ref ChannelVolumes volume, SuccessCallback callback, IntPtr userdata);

    [DllImport(Library, EntryPoint = "pa_operation_get_state")]
    internal static extern int OperationGetState(IntPtr operation);

    [DllImport(Library, EntryPoint = "pa_operation_cancel")]
    internal static extern void OperationCancel(IntPtr operation);

    [DllImport(Library, EntryPoint = "pa_operation_unref")]
    internal static extern void OperationUnref(IntPtr operation);

    [DllImport(Library, EntryPoint = "pa_proplist_gets")]
    internal static extern IntPtr ProplistGets(IntPtr proplist, byte[] key);

    [DllImport(Library, EntryPoint = "pa_strerror")]
    internal static extern IntPtr StrError(int error);

    [DllImport(Library, EntryPoint = "pa_sw_volume_to_linear")]
    internal static extern double VolumeToLinear(uint volume);

    [DllImport(Library, EntryPoint = "pa_sw_volume_from_linear")]
    internal static extern uint VolumeFromLinear(double linear);

    /// <summary>A string the library owns, or empty for none.</summary>
    internal static string Text(IntPtr text) => text == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(text) ?? "";

    private const string SimpleLibrary = "libpulse-simple.so.0";

    /// <summary>PA_SAMPLE_S16LE: signed 16-bit, little-endian.</summary>
    internal const int SampleS16Le = 3;

    /// <summary>PA_STREAM_PLAYBACK.</summary>
    internal const int StreamPlayback = 1;

    /// <summary>PA_STREAM_RECORD.</summary>
    internal const int StreamRecord = 2;

    /// <summary>PA_SAMPLE_FLOAT32LE: 32-bit floating point, little-endian.</summary>
    internal const int SampleFloat32Le = 5;

    /// <summary>(uint32_t) -1: the server's own choice for a buffer setting.</summary>
    internal const uint ServerChoice = uint.MaxValue;

    [StructLayout(LayoutKind.Sequential)]
    internal struct BufferAttributes
    {
        public uint MaxLength;
        public uint TargetLength;
        public uint PreBuffer;
        public uint MinRequest;
        public uint FragmentSize;
    }

    [DllImport(SimpleLibrary, EntryPoint = "pa_simple_new")]
    internal static extern IntPtr SimpleNew(
        IntPtr server, byte[] name, int direction, byte[] device, byte[] streamName,
        ref SampleSpec spec, IntPtr channelMap, IntPtr attributes, out int error);

    [DllImport(SimpleLibrary, EntryPoint = "pa_simple_new")]
    internal static extern IntPtr SimpleNewWithBuffer(
        IntPtr server, byte[] name, int direction, byte[] device, byte[] streamName,
        ref SampleSpec spec, IntPtr channelMap, ref BufferAttributes attributes, out int error);

    [DllImport(SimpleLibrary, EntryPoint = "pa_simple_read")]
    internal static extern int SimpleRead(IntPtr stream, byte[] data, nuint bytes, out int error);

    [DllImport(SimpleLibrary, EntryPoint = "pa_simple_write")]
    internal static extern int SimpleWrite(IntPtr stream, byte[] data, nuint bytes, out int error);

    [DllImport(SimpleLibrary, EntryPoint = "pa_simple_drain")]
    internal static extern int SimpleDrain(IntPtr stream, out int error);

    [DllImport(SimpleLibrary, EntryPoint = "pa_simple_free")]
    internal static extern void SimpleFree(IntPtr stream);

    /// <summary>A string as the library takes one: UTF-8, ending in a zero byte.</summary>
    internal static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text + '\0');

    /// <summary>One property of an object, or empty when it does not have it.</summary>
    internal static string Property(IntPtr proplist, string key) =>
        proplist == IntPtr.Zero ? "" : Text(ProplistGets(proplist, Utf8(key)));
}
