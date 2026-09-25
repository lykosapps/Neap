using System.Runtime.InteropServices;

namespace Neap.Core.Audio;

internal enum DataFlow { Render = 0, Capture = 1, All = 2 }
internal enum Role { Console = 0, Multimedia = 1, Communications = 2 }

internal static class Com
{
    internal const uint ClsCtxAll = 23;
    internal const uint DeviceStateActive = 0x1;
    internal const uint StgmRead = 0;
    internal const uint StgmReadWrite = 2;
    internal const ushort VtBlob = 65;

    internal const int SOk = 0;
    internal const int SFalse = 1;

    /// <summary>Whether an HRESULT is an error; only negative values are.</summary>
    /// <remarks>
    /// Core Audio returns S_FALSE (1) when a set would change nothing, such as
    /// muting something already muted or writing the current level. That is
    /// success; treating any non-zero result as failure shows the user an
    /// error for a harmless no-op.
    /// </remarks>
    internal static bool Failed(int hresult) => hresult < 0;

    internal static readonly Guid MMDeviceEnumeratorClsid =
        new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    /// <summary>PKEY_AudioEngine_DeviceFormat, the value the Advanced tab writes.</summary>
    internal static readonly PropertyKey DeviceFormatKey =
        new(new Guid("F19F064D-082C-4E27-BC73-6882A1BB8E4C"), 0);

    [DllImport("ole32.dll")]
    internal static extern int CoCreateInstance(
        ref Guid clsid, IntPtr outer, uint context, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out object instance);

    [DllImport("ole32.dll")]
    internal static extern void CoTaskMemFree(IntPtr memory);

    /// <summary>Frees the memory a PROPVARIANT from a property store owns.</summary>
    /// <remarks>
    /// A PROPVARIANT holding a string or a blob owns memory, and the caller
    /// owns it once GetValue returns. Every GetValue needs a matching clear:
    /// the readers run once per endpoint about once a second while a volume
    /// row is on screen, so a missed clear leaks steadily.
    /// </remarks>
    [DllImport("ole32.dll")]
    internal static extern int PropVariantClear(ref PropVariant value);

    internal static T Create<T>(Guid clsid)
    {
        Guid iid = typeof(T).GUID;
        int hr = CoCreateInstance(ref clsid, IntPtr.Zero, ClsCtxAll, ref iid, out object o);
        if (Failed(hr)) Marshal.ThrowExceptionForHR(hr);
        return (T)o;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid FormatId;
    public uint PropertyId;
    public PropertyKey(Guid formatId, uint propertyId)
    {
        FormatId = formatId;
        PropertyId = propertyId;
    }
}

/// <summary>A PROPVARIANT, declared only as far as the string and blob members used here.</summary>
/// <remarks>
/// The union starts at offset 8, and where the value sits depends on the
/// type. A string (VT_LPWSTR) is a pointer at offset 8; a BLOB is a size at 8
/// and a pointer at 16. Reading a string through the blob's pointer gives an
/// empty device name, which later surfaces as "no endpoint matching".
/// </remarks>
[StructLayout(LayoutKind.Explicit)]
internal struct PropVariant
{
    [FieldOffset(0)] public ushort Type;
    [FieldOffset(8)] public IntPtr PointerValue;   // VT_LPWSTR
    [FieldOffset(8)] public uint BlobSize;         // VT_BLOB
    [FieldOffset(16)] public IntPtr BlobData;      // VT_BLOB
}

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(DataFlow flow, uint stateMask, out IMMDeviceCollection devices);
    [PreserveSig] int GetDefaultAudioEndpoint(DataFlow flow, Role role, out IMMDevice device);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
    [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int Item(uint index, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, uint context, IntPtr parameters,
        [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore store);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out uint state);
}

[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetAt(uint index, out PropertyKey key);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
    [PreserveSig] int Commit();
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
    [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
    [PreserveSig] int GetChannelCount(out uint count);
    [PreserveSig] int SetMasterVolumeLevel(float level, IntPtr context);
    [PreserveSig] int SetMasterVolumeLevelScalar(float level, IntPtr context);
    [PreserveSig] int GetMasterVolumeLevel(out float level);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
    [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, IntPtr context);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, IntPtr context);
    [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, IntPtr context);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

[ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient
{
    [PreserveSig]
    int Initialize(int shareMode, uint flags, long bufferDuration,
        long periodicity, IntPtr format, IntPtr sessionGuid);
    [PreserveSig] int GetBufferSize(out uint frames);
    [PreserveSig] int GetStreamLatency(out long latency);
    [PreserveSig] int GetCurrentPadding(out uint frames);
    [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
    [PreserveSig] int GetMixFormat(out IntPtr format);
    [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
    [PreserveSig] int Start();
    [PreserveSig] int Stop();
    [PreserveSig] int Reset();
    [PreserveSig] int SetEventHandle(IntPtr handle);
    [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
}

[ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioMeterInformation
{
    [PreserveSig] int GetPeakValue(out float peak);
}

/// <summary>
/// The undocumented policy interface the Sound control panel uses to set
/// default devices and formats.
/// </summary>
/// <remarks>
/// <para>
/// Stable since Windows 7 and used by audio-switching tools generally. It is
/// the only way to make a format change take effect: writing the property
/// store persists the value and every dialog reads it back, while the
/// endpoint carries on at the old rate.
/// </para>
/// <para>
/// Method order is the vtable order. Do not rearrange.
/// </para>
/// </remarks>
[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr format);
    [PreserveSig]
    int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id,
        [MarshalAs(UnmanagedType.Bool)] bool isDefault, out IntPtr format);
    [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig]
    int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id,
        IntPtr endpointFormat, IntPtr mixFormat);
    [PreserveSig]
    int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id,
        [MarshalAs(UnmanagedType.Bool)] bool isDefault, out long period, out long minimum);
    [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, ref long period);
    [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
    [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
    [PreserveSig]
    int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id,
        ref PropertyKey key, out PropVariant value);
    [PreserveSig]
    int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id,
        ref PropertyKey key, ref PropVariant value);
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, Role role);
    [PreserveSig]
    int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id,
        [MarshalAs(UnmanagedType.Bool)] bool visible);
}

/// <summary>WAVEFORMATEXTENSIBLE, which is what Windows stores for endpoints.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct WaveFormatExtensible
{
    public ushort FormatTag;
    public ushort Channels;
    public uint SamplesPerSec;
    public uint AvgBytesPerSec;
    public ushort BlockAlign;
    public ushort BitsPerSample;
    public ushort CbSize;
    public ushort ValidBitsPerSample;
    public uint ChannelMask;
    public Guid SubFormat;
}
