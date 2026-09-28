using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Neap.Core.Audio;

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
/// Reads and sets the shared-mode format of a Windows audio endpoint: the
/// "Default Format" list on the Advanced tab of a device's properties.
/// </summary>
/// <remarks>
/// <para>
/// 24-bit is packed: three bytes per sample, not four. Windows stores
/// wBitsPerSample 24 and nBlockAlign 6 for stereo. A format padded into a
/// 32-bit container is wrong for the stored default, and the error hides
/// itself: read back, it still reports 24 bits, so the app agrees with
/// Windows about the setting while being unable to set it. FINDINGS.md lists
/// the symptoms.
/// </para>
/// <para>
/// Writing the property store does not change the format. The value
/// persists and every dialog reads it back, but the endpoint carries on at
/// the old rate. The Sound control panel instead asks the audio service to
/// adopt the format through IPolicyConfig, and so does this class, with no
/// fallback to writing the store: a setting that says one thing while the
/// device does another is worse than a refusal that says so.
/// </para>
/// <para>
/// IPolicyConfig only reconfigures a live endpoint. With nothing playing it
/// records the new default and stops, and the next client to open the device
/// pins whatever the service was last running. Do not release the device
/// before writing.
/// </para>
/// </remarks>
public static class DeviceFormat
{
    private const ushort WaveFormatExtensibleTag = 0xFFFE;
    private static readonly Guid SubtypePcm = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid SubtypeFloat = new("00000003-0000-0010-8000-00AA00389B71");
    private static readonly Guid PolicyConfigClsid = new("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");

    private const int ShareModeExclusive = 1;

    /// <summary>The sample rates Windows itself offers, each of which is tried.</summary>
    /// <remarks>
    /// Keep the low rates: the headset microphone offers 16000 Hz as well as
    /// 48000 Hz, and a rate missing from this list is never asked about, so
    /// the device appears to lack it.
    /// </remarks>
    public static readonly IReadOnlyList<int> Rates = new[]
    {
        8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000, 176400, 192000
    };

    public static readonly IReadOnlyList<int> Depths = new[] { 16, 24, 32 };

    /// <summary>Windows' own words for each rate, so the app and the Sound dialog agree.</summary>
    /// <remarks>
    /// Confirmed against the Sound dialog for 16000, 48000 and 96000. Rates
    /// not listed, 96000 included, are "Studio quality".
    /// </remarks>
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

    /// <summary>Sets the format, refusing anything the device does not accept.</summary>
    /// <exception cref="FormatException">The device does not support the format, or refused it.</exception>
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

    /// <summary>How long a device is given to come up at a new format.</summary>
    private static readonly TimeSpan FollowWait = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Sets a format and checks the device is running at it: what choosing a
    /// format should do.
    /// </summary>
    /// <remarks>
    /// A stream is held open meanwhile, a silent tone for the output and the
    /// microphone itself for the input, so there is a live endpoint for
    /// IPolicyConfig to reconfigure. Without one it records the format and
    /// changes nothing, and reports success; see the class remarks.
    /// </remarks>
    /// <exception cref="FormatException">Windows refused the format, or the device stayed at another rate.</exception>
    /// <exception cref="WindowsAudioException">The headset has no such device in Windows.</exception>
    public static void Switch(string match, int bits, int rate, Flow flow = Flow.Output)
    {
        var was = Current(match, flow);
        using (IDisposable live = flow == Flow.Output ? TestTone.Open(frequency: 1000, match) : MicrophoneListener.Open(match))
        {
            Apply(match, bits, rate, flow);
            var clock = Stopwatch.StartNew();
            while (MixFormat(match, flow)?.Rate != rate && clock.Elapsed < FollowWait) Thread.Sleep(100);
        }
        if (MixFormat(match, flow) is not { } engine || engine.Rate == rate) return;

        // Put the setting back, so it says what the device does rather than
        // what it refused. The microphone refuses: measured, it stays at 48 kHz.
        string refused = $"Windows kept the device at {engine.Rate / 1000.0:0.#} kHz";
        if (was is null) throw new FormatException(refused);
        try { Apply(match, was.Bits, was.Rate, flow); }
        catch (FormatException ex) { throw new FormatException($"{refused}, and the setting could not be put back: {ex.Message}"); }
        throw new FormatException(refused);
    }

    /// <summary>
    /// Sets the output's format again, by way of another the device accepts,
    /// and checks the engine came back at it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stepping away and back is what turned the Charging Dock's status ring
    /// purple when done by hand; see <see cref="RingRefresh"/>.
    /// </para>
    /// <para>
    /// Each step holds a silent stream of its own open, because a format is
    /// only taken up by a live endpoint. The first step can end the streams
    /// that were open, so the second cannot count on the first's.
    /// </para>
    /// </remarks>
    /// <exception cref="FormatException">There is no other format to step through, or the device did not come back.</exception>
    /// <exception cref="WindowsAudioException">The headset has no output Windows can play to.</exception>
    public static void Refresh(string match)
    {
        var report = Describe(match);
        if (report.Current is not { } current)
            throw new FormatException($"{report.Device} has no format to set again");
        var other = report.Options.FirstOrDefault(f => f.Bits == current.Bits && f.Rate != current.Rate)
            ?? report.Options.FirstOrDefault(f => f.Bits != current.Bits || f.Rate != current.Rate)
            ?? throw new FormatException($"{report.Device} offers no other format to step through");

        // A tone opens silent and stays so until given a loudness.
        foreach (var step in new[] { other, current })
            using (TestTone.Open(frequency: 1000, match))
                Apply(match, step.Bits, step.Rate);

        if (MixFormat(match) is { } engine && engine.Rate != current.Rate)
            throw new FormatException($"{report.Device} came back at {engine.Rate} Hz, not {current.Rate} Hz");
    }

    // -- the parts that do the work ---------------------------------------

    /// <summary>Builds a WAVEFORMATEXTENSIBLE the way Windows builds them.</summary>
    /// <remarks>
    /// <para>
    /// Mono is SPEAKER_FRONT_CENTER (0x4), not the first speaker (0x1).
    /// Windows matches the stored format against the ones its driver
    /// advertises by value, so a plausible but different field gives a format
    /// that works yet shows as an extra, unlabelled entry in the Sound dialog.
    /// </para>
    /// <para>
    /// 32-bit float is unverified: no headset device offers 32-bit. Windows
    /// stores one Realtek output as 32-bit PCM with 24 valid bits, not float,
    /// so check what Windows writes before relying on it.
    /// </para>
    /// </remarks>
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
            finally { _ = Com.PropVariantClear(ref value); }
        }
        finally { Marshal.ReleaseComObject(store); }
    }

    /// <summary>Whether the device accepts a format, asked in exclusive mode.</summary>
    /// <remarks>
    /// Exclusive mode is what the Windows "Default Format" list is built from.
    /// Shared mode only agrees with whatever the engine is already mixing at,
    /// so its answer changes whenever anything else touches the device.
    /// </remarks>
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
        if (PolicySetFormat(endpoint.Id, format) is { } why)
            throw new FormatException($"Windows would not switch {endpoint.Name} to that format: {why}");
    }

    /// <returns>Null when Windows took the format, or why it didn't.</returns>
    private static string? PolicySetFormat(string deviceId, WaveFormatExtensible format)
    {
        if (string.IsNullOrEmpty(deviceId)) return "the device has no id";
        IPolicyConfig policy;
        try { policy = Com.Create<IPolicyConfig>(PolicyConfigClsid); }
        catch (Exception ex) { return $"its audio settings could not be reached ({ex.Message})"; }

        IntPtr block = Marshal.AllocHGlobal(Marshal.SizeOf<WaveFormatExtensible>());
        try
        {
            Marshal.StructureToPtr(format, block, false);
            int hr = policy.SetDeviceFormat(deviceId, block, block);
            return Com.Failed(hr) ? $"it was refused (0x{hr:x8})" : null;
        }
        catch (Exception ex) { return ex.Message; }
        finally
        {
            Marshal.FreeHGlobal(block);
            Marshal.ReleaseComObject(policy);
        }
    }

    /// <summary>
    /// Sets the default playback or recording device for all three roles, as
    /// the Sound dialog does.
    /// </summary>
    /// <returns>False if IPolicyConfig is unavailable or any role is refused.</returns>
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
