using Neap.Core.Connection;

namespace Neap.Services;

/// <summary>What to tell a person whose sound or microphone is aimed at a transmitter the headset is not on.</summary>
/// <param name="Title">The headline: where the system is sending what.</param>
/// <param name="Message">What it means, and what to do about it.</param>
public sealed record RoutingStory(string Title, string Message)
{
    /// <summary>
    /// The warning for the routes as they stand, or null when there is nothing
    /// to warn of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two transmitters mean two sets of audio devices, and the headset uses
    /// only one at a time. The system keeps pointing at whichever it was last
    /// told to, and moves by itself when a transmitter is plugged in or pulled
    /// out, so the output or the microphone can end up aimed at the one
    /// carrying nothing. Nothing plays, or nobody hears you, with no clue
    /// anywhere: the device is present, enabled, and named almost identically
    /// to the right one.
    /// </para>
    /// <para>
    /// The text names transmitters, not devices; the device name appears once,
    /// as the thing to pick. Devices are matched to transmitters by the USB
    /// product id behind them, not by the system's naming, which is
    /// positional.
    /// </para>
    /// </remarks>
    /// <param name="status">What the headset is doing.</param>
    /// <param name="route">Where the system is sending sound and taking the microphone from.</param>
    /// <param name="sound">Check where sound is being sent.</param>
    /// <param name="microphone">Check which microphone is being listened to.</param>
    public static RoutingStory? Of(HeadsetStatus status, AudioRoute route, bool sound, bool microphone)
    {
        var verdict = RoutingCheck.Judge(status, route.Output, route.Calls, route.Input,
            route.CallsInput, route.Cable, AudioRoute.Belonging, sound, microphone);
        if (verdict is not { Wrong.Count: > 0 }) return null;

        // A whole sentence for each combination rather than one sentence with
        // the halves joined into it: "sending {sound and calls} to" only works
        // in English. Only names and lists are substituted.
        var wrong = verdict.Wrong;
        string here = status.Adapter;
        string there = wrong[0].OnName;
        bool outSound = wrong.Any(w => w.Role == AudioRole.Sound);
        bool calls = wrong.Any(w => w.Role == AudioRole.Calls);
        bool mic = wrong.Any(w => !w.Output);

        // Not "the system is using the Charging Dock": on Home that sits right
        // under "Connected through", and the two read as the same good news.
        string title = Strings.Format((outSound, calls, mic) switch
        {
            (true, false, false) => "Routing_TitleSound",
            (false, true, false) => "Routing_TitleCalls",
            (false, false, true) => "Routing_TitleMicrophone",
            (true, true, false) => "Routing_TitleSoundCalls",
            (true, false, true) => "Routing_TitleSoundMicrophone",
            (false, true, true) => "Routing_TitleCallsMicrophone",
            _ => "Routing_TitleAll",
        }, there);

        // One instruction per device to choose, saying what for, because the
        // communications default is set apart from the rest in Sound settings.
        var picks = wrong.Where(w => w.Pick.Length > 0)
            .GroupBy(w => w.Pick)
            .Select(g => Pick(g.Key, g.ToList()))
            .ToList();
        string choose = picks.Count > 0
            ? " " + Strings.Format("Routing_Choose", Strings.List(picks))
            : "";

        if (verdict.Cabled)
        {
            bool outs = wrong.Any(w => w.Output);
            return new RoutingStory(title,
                (outs && mic ? Strings.Get("Routing_CableBoth")
                    : outs ? Strings.Format("Routing_CableSound", there)
                    : Strings.Get("Routing_CableMicrophone"))
                + choose);
        }

        // Two ways out, when there are two: CrossPlay moves the headset to the
        // transmitter the system is already using, which is often quicker than
        // changing the system back.
        string on = Strings.Format((outSound, calls, mic) switch
        {
            (true, false, false) => "Routing_OnSilent",
            (false, true, false) => "Routing_OnNoCalls",
            (false, false, true) => "Routing_OnUnheard",
            _ => "Routing_On",
        }, here);

        return new RoutingStory(title, wrong.Any(w => w.Pick.Length == 0)
            ? Strings.Format("Routing_OnUnplugged", here, there)
            : verdict.CrossPlayFixes && picks.Count > 0
                ? on + " " + Strings.Format("Routing_PressOrChoose", there, here, Strings.List(picks))
                : on + choose);
    }

    /// <summary>
    /// Builds one instruction naming a device and what to choose it for, such
    /// as "Headset Earphone" for output and communications.
    /// </summary>
    private static string Pick(string device, IReadOnlyList<Misrouted> halves)
    {
        bool output = halves.Any(h => h.Role == AudioRole.Sound);
        bool input = halves.Any(h => h.Role == AudioRole.Microphone);
        bool calls = halves.Any(h => h.Role is AudioRole.Calls or AudioRole.CallsMicrophone);
        return Strings.Format((output, input, calls) switch
        {
            (true, _, true) => "Routing_PickOutputCalls",
            (true, _, false) => "Routing_PickOutput",
            (false, true, true) => "Routing_PickInputCalls",
            (false, true, false) => "Routing_PickInput",
            _ => "Routing_PickCalls",
        }, device);
    }
}
