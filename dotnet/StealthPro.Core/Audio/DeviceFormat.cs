using System.Runtime.InteropServices;

namespace StealthPro.Core.Audio;

public class FormatException : Exception
{
    public FormatException(string message) : base(message) { }
}

public sealed record AudioFormat(int Bits, int Rate, int Channels)
{
    public string Label
    {
        get
        {
            double khz = Rate / 1000.0;
            string quality = DeviceFormat.Quality.TryGetValue(Rate, out var name)
                ? name : "Studio quality";
            return $"{Bits}-bit, {khz:0.##} kHz ({quality})";
        }
    }
}

public sealed record FormatReport(
    string Device, AudioFormat? Current, IReadOnlyList<AudioFormat> Options);

/// <summary>
/// The shared-mode format of a Windows audio endpoint — the "Default Format"
/// dropdown on the Advanced tab of a device's properties.
///
/// Ported from stealthpro/audioformat.py. Three things here were expensive
/// to learn and each one is load-bearing.
///
/// <b>24-bit is packed here: three bytes, not four.</b> Windows stores
/// wBitsPerSample 24 and nBlockAlign 6. Padding it into a 32-bit container
/// looks reasonable and is simply wrong for the stored default format, and
/// it was wrong in a way that hid itself: read back, a padded format still
/// reports 24 bits, so the app agreed with Windows about the device's
/// setting while being unable to set it. One wrong field produced four
/// separate symptoms — see FINDINGS.md.
///
/// <b>Writing the property store is not how you change a format.</b> It
/// persists and every dialog reads it back, while the endpoint carries on at
/// the old rate and nothing is interrupted. What the Sound control panel
/// does is ask the audio service to adopt the format, through IPolicyConfig.
///
/// <b>And that only works on a live endpoint.</b> With nothing playing there
/// is nothing to reconfigure, so it records the new default and stops there;
/// the next client to open the device then pins whatever the service was
/// last actually running. Releasing the device before writing sounds
/// sensible and is precisely what breaks it.
/// </summary>
public static class DeviceFormat
{
    private const ushort WaveFormatExtensibleTag = 0xFFFE;
    private static readonly Guid SubtypePcm = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid SubtypeFloat = new("00000003-0000-0010-8000-00AA00389B71");
    private static readonly Guid PolicyConfigClsid = new("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");

    private const int ShareModeExclusive = 1;

    /// <summary>
    /// What Windows itself offers. The low rates matter: the headset
    /// microphone offers 16000Hz as well as 48000Hz, and leaving it out of
    /// this list meant the probe never asked and the app confidently
    /// reported that the microphone had exactly one format. A probe only
    /// ever finds what it thinks to ask for.
    /// </summary>
    public static readonly IReadOnlyList<int> Rates = new[]
    {
        8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000, 176400, 192000
    };

    public static readonly IReadOnlyList<int> Depths = new[] { 16, 24, 32 };

    /// <summary>
    /// Windows' own words for each rate, so the app and the Sound dialog
    /// agree. Confirmed on this machine for 16000, 48000 and 96000.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, string> Quality =
        new Dictionary<int, string>
        {
            [8000] = "Telephone quality",
            [11025] = "Telephone quality",
            [16000] = "Tape recorder quality",
            [22050] = "AM radio quality",
            [32000] = "FM radio quality",
            [44100] = "CD quality",
            [48000] = "DVD quality",
        };

    // -- reading -----------------------------------------------------------

    public static AudioFormat? Current(string match, Flow flow = Flow.Output)
    {
        using var endpoint = Endpoint.Open(match, flow, fallBackToDefault: false);
        return ReadStored(endpoint);
    }

    public static FormatReport Describe(string match, Flow flow = Flow.Output)
    {
        using var endpoint = Endpoint.Open(match, flow, fallBackToDefault: false);
        var current = ReadStored(endpoint);
        int channels = current?.Channels ?? 2;

        var options = new List<AudioFormat>();
        foreach (int rate in Rates)
            foreach (int bits in Depths)
                if (Supports(endpoint, Build(bits, rate, channels)))
                    options.Add(new AudioFormat(bits, rate, channels));

        return new FormatReport(endpoint.Name, current, options);
    }

    /// <summary>What the audio engine is actually mixing at, right now.</summary>
    public static AudioFormat? MixFormat(string match, Flow flow = Flow.Output)
    {
        using var endpoint = Endpoint.Open(match, flow, fallBackToDefault: false);
        var client = ActivateClient(endpoint);
        if (client is null) return null;
        try
        {
            if (Com.Failed(client.GetMixFormat(out IntPtr raw)) || raw == IntPtr.Zero) return null;
            try { return Read(Marshal.PtrToStructure<WaveFormatExtensible>(raw)); }
            finally { Com.CoTaskMemFree(raw); }
        }
        finally { Marshal.ReleaseComObject(client); }
    }

    // -- writing -----------------------------------------------------------

    /// <summary>Set the format, refusing anything the device has not agreed to.</summary>
    public static void Apply(string match, int bits, int rate, Flow flow = Flow.Output)
    {
        using var endpoint = Endpoint.Open(match, flow, fallBackToDefault: false);
        var current = ReadStored(endpoint);
        var format = Build(bits, rate, current?.Channels ?? 2);
        if (!Supports(endpoint, format))
            throw new FormatException(
                $"{new AudioFormat(bits, rate, 2).Label} is not supported by {endpoint.Name}");
        Write(endpoint, format);
    }

    // -- the parts that do the work ---------------------------------------

    /// <summary>
    /// A WAVEFORMATEXTENSIBLE shaped the way Windows shapes them.
    ///
    /// Mono is SPEAKER_FRONT_CENTER (0x4), not "the first speaker" (0x1).
    /// Windows matches the stored format against the ones its driver
    /// advertises by value, so a plausible-but-different field gives you a
    /// format that works and is nonetheless not theirs — it appeared as an
    /// extra, unlabelled entry in the Sound dialog every time we wrote one.
    ///
    /// 32-bit is a guess and no device here offers it, so it has never been
    /// exercised. Windows stores the Realtek output on this machine as
    /// 32-bit PCM with 24 valid bits, not float; check what Windows writes
    /// before trusting this line.
    /// </summary>
    internal static WaveFormatExtensible Build(int bits, int rate, int channels)
    {
        ushort container = (ushort)bits;             // packed, including 24-bit
        ushort blockAlign = (ushort)(channels * container / 8);
        return new WaveFormatExtensible
        {
            FormatTag = WaveFormatExtensibleTag,
            Channels = (ushort)channels,
            SamplesPerSec = (uint)rate,
            AvgBytesPerSec = (uint)(rate * blockAlign),
            BlockAlign = blockAlign,
            BitsPerSample = container,
            CbSize = 22,
            ValidBitsPerSample = (ushort)bits,
            ChannelMask = channels == 1 ? 0x4u : channels == 2 ? 0x3u : (1u << channels) - 1,
            SubFormat = bits == 32 ? SubtypeFloat : SubtypePcm,
        };
    }

    private static AudioFormat Read(WaveFormatExtensible format)
    {
        int bits = format.FormatTag == WaveFormatExtensibleTag && format.ValidBitsPerSample > 0
            ? format.ValidBitsPerSample : format.BitsPerSample;
        return new AudioFormat(bits, (int)format.SamplesPerSec, format.Channels);
    }

    private static AudioFormat? ReadStored(Endpoint endpoint)
    {
        if (Com.Failed(endpoint.Device.OpenPropertyStore(Com.StgmRead, out var store))) return null;
        try
        {
            var key = Com.DeviceFormatKey;
            if (Com.Failed(store.GetValue(ref key, out var value))) return null;
            try
            {
                if (value.Type != Com.VtBlob || value.BlobData == IntPtr.Zero) return null;
                // Copied into managed memory before the clear below frees it.
                return Read(Marshal.PtrToStructure<WaveFormatExtensible>(value.BlobData));
            }
            finally { Com.PropVariantClear(ref value); }
        }
        finally { Marshal.ReleaseComObject(store); }
    }

    /// <summary>
    /// Asked in EXCLUSIVE mode, which is what the Windows "Default Format"
    /// list is built from. Shared mode only ever agrees with whatever the
    /// engine is already mixing at, so its answer moves whenever anything
    /// else touches the device.
    /// </summary>
    private static bool Supports(Endpoint endpoint, WaveFormatExtensible format)
    {
        var client = ActivateClient(endpoint);
        if (client is null) return false;
        IntPtr block = Marshal.AllocHGlobal(Marshal.SizeOf<WaveFormatExtensible>());
        try
        {
            Marshal.StructureToPtr(format, block, false);
            int hr = client.IsFormatSupported(ShareModeExclusive, block, out IntPtr closest);
            if (closest != IntPtr.Zero) Com.CoTaskMemFree(closest);
            return hr == Com.SOk;
        }
        finally
        {
            Marshal.FreeHGlobal(block);
            Marshal.ReleaseComObject(client);
        }
    }

    private static IAudioClient? ActivateClient(Endpoint endpoint)
    {
        Guid iid = typeof(IAudioClient).GUID;
        return Com.Failed(endpoint.Device.Activate(ref iid, Com.ClsCtxAll, IntPtr.Zero, out object o))
            ? null : (IAudioClient)o;
    }

    private static void Write(Endpoint endpoint, WaveFormatExtensible format)
    {
        if (PolicySetFormat(endpoint.Id, format)) return;
        // Fallback for a machine where IPolicyConfig is not available. Still
        // better than refusing — but on its own it only changes what the
        // setting says, not what the device does.
        WritePropertyStore(endpoint, format);
    }

    private static bool PolicySetFormat(string deviceId, WaveFormatExtensible format)
    {
        if (string.IsNullOrEmpty(deviceId)) return false;
        IPolicyConfig? policy;
        try { policy = Com.Create<IPolicyConfig>(PolicyConfigClsid); }
        catch (Exception) { return false; }

        IntPtr block = Marshal.AllocHGlobal(Marshal.SizeOf<WaveFormatExtensible>());
        try
        {
            Marshal.StructureToPtr(format, block, false);
            return !Com.Failed(policy.SetDeviceFormat(deviceId, block, block));
        }
        catch (Exception) { return false; }
        finally
        {
            Marshal.FreeHGlobal(block);
            Marshal.ReleaseComObject(policy);
        }
    }

    private static void WritePropertyStore(Endpoint endpoint, WaveFormatExtensible format)
    {
        if (Com.Failed(endpoint.Device.OpenPropertyStore(Com.StgmReadWrite, out var store)))
            throw new FormatException("Windows would not let the app change this device's format");
        IntPtr block = Marshal.AllocHGlobal(Marshal.SizeOf<WaveFormatExtensible>());
        try
        {
            Marshal.StructureToPtr(format, block, false);
            var key = Com.DeviceFormatKey;
            var value = new PropVariant
            {
                Type = Com.VtBlob,
                BlobSize = (uint)Marshal.SizeOf<WaveFormatExtensible>(),
                BlobData = block,
            };
            int hr = store.SetValue(ref key, ref value);
            if (Com.Failed(hr)) throw new FormatException($"the device refused the format (0x{hr:x})");
            if (Com.Failed(store.Commit())) throw new FormatException("the change could not be saved");
        }
        finally
        {
            Marshal.FreeHGlobal(block);
            Marshal.ReleaseComObject(store);
        }
    }

    /// <summary>Set the default playback or recording device, as the Sound dialog does.</summary>
    public static bool SetDefaultEndpoint(string deviceId)
    {
        IPolicyConfig? policy;
        try { policy = Com.Create<IPolicyConfig>(PolicyConfigClsid); }
        catch (Exception) { return false; }
        try
        {
            foreach (var role in new[] { Role.Console, Role.Multimedia, Role.Communications })
                if (Com.Failed(policy.SetDefaultEndpoint(deviceId, role))) return false;
            return true;
        }
        finally { Marshal.ReleaseComObject(policy); }
    }
}
