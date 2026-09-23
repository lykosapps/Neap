using StealthPro.Core.Audio;

namespace StealthPro.Core.Connection;

/// <summary>The four things Windows points at a device, each set on its own.</summary>
public enum AudioRole { Sound, Calls, Microphone, CallsMicrophone }

/// <summary>
/// A half pointed at the wrong one of the headset's devices: where it is,
/// and the device to choose instead, or empty when there is none to choose.
/// </summary>
public sealed record Misrouted(AudioRole Role, string OnName, string OnProduct, string Pick)
{
    public bool Output => Role is AudioRole.Sound or AudioRole.Calls;
}

/// <param name="Wrong">The halves this placement shows, in order.</param>
/// <param name="Cabled">The headset is on its cable, the only right place for everything.</param>
/// <param name="CrossPlayFixes">
/// Pressing CrossPlay would put every half right: each of the headset's
/// devices Windows uses is on the transmitter CrossPlay would move to.
/// </param>
public sealed record RoutingVerdict(IReadOnlyList<Misrouted> Wrong, bool Cabled, bool CrossPlayFixes);

/// <summary>
/// Whether Windows is sending sound, calls or the microphone to one of the
/// headset's devices that is not the one in use.
/// </summary>
/// <remarks>
/// <para>
/// Two transmitters mean two sets of audio devices, and the headset uses one
/// at a time. Windows keeps pointing wherever it was last told, and moves by
/// itself when a transmitter is plugged in or pulled out, so a half can end
/// up on the transmitter carrying nothing.
/// </para>
/// <para>
/// With the USB-C cable in, the cable is the only right place for all of it:
/// the headset plays one source at a time and sends the voice only over the
/// cable. A device that is not one of the headset's at all, such as speakers
/// or a webcam microphone, is a deliberate choice and never counted.
/// </para>
/// </remarks>
public static class RoutingCheck
{
    /// <param name="belonging">
    /// The names of the headset's devices for a product id, output or not;
    /// the first is offered as the one to choose.
    /// </param>
    /// <param name="sound">This placement shows the output halves.</param>
    /// <param name="microphone">This placement shows the microphone halves.</param>
    /// <returns>Null when there is nothing to judge: no transmitter known, or no sound at all.</returns>
    public static RoutingVerdict? Judge(HeadsetStatus status,
        Routed? output, Routed? calls, Routed? input, Routed? callsInput, string cable,
        Func<string, bool, IReadOnlyList<string>> belonging, bool sound = true, bool microphone = true)
    {
        // With no sound arriving at all, where Windows points is not the
        // problem, and saying so would send somebody to the wrong fix.
        if (status.Link is not (Link.Connected or Link.Silent)
            || status.Product.Length == 0 || status.NoSound)
            return null;

        bool cabled = cable.Length > 0;
        string right = cabled ? cable : status.Product;

        var all = new[]
        {
            Check(output, right, AudioRole.Sound, belonging),
            Check(calls, right, AudioRole.Calls, belonging),
            Check(input, right, AudioRole.Microphone, belonging),
            Check(callsInput, right, AudioRole.CallsMicrophone, belonging),
        };
        var shown = all.OfType<Misrouted>().Where(w => w.Output ? sound : microphone).ToList();

        // CrossPlay moves everything together, so it only helps when every
        // half Windows is using a headset device for is on the one it moves to.
        string to = shown.Count > 0 ? shown[0].OnProduct : "";
        bool fixes = shown.Count > 0
            && new[] { output, calls, input, callsInput }
                .All(r => r is null || r.Product.Length == 0 || Same(r.Product, to));

        return new RoutingVerdict(shown, cabled, fixes);
    }

    private static Misrouted? Check(Routed? current, string right, AudioRole role,
        Func<string, bool, IReadOnlyList<string>> belonging)
    {
        if (current is null || current.Product.Length == 0) return null;
        if (Same(current.Product, right)) return null;

        string onName = Transmitters.Hardware.TryGetValue(current.Product, out var called)
            ? called : "other transmitter";
        bool output = role is AudioRole.Sound or AudioRole.Calls;
        var picks = belonging(right, output);
        return new Misrouted(role, onName, current.Product, picks.Count > 0 ? picks[0] : "");
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
