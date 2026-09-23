using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Audio;
using StealthPro.Core.Connection;

namespace StealthPro.App.Controls;

/// <summary>
/// A warning shown when Windows is sending sound to, or listening through, a
/// transmitter the headset is not using.
/// </summary>
/// <remarks>
/// <para>
/// Two transmitters mean two sets of audio devices, and the headset uses only
/// one at a time. Windows keeps pointing at whichever it was last told to,
/// and moves by itself when a transmitter is plugged in or pulled out, so the
/// output or the microphone can end up aimed at the one carrying nothing.
/// Nothing plays, or nobody hears you, with no clue anywhere: the device is
/// present, enabled, and named almost identically to the right one.
/// </para>
/// <para>
/// The notice sits beside what it is about rather than at the top of the
/// page: in the Connections card on Home, under Volume on Audio, and under
/// the microphone's own section. <see cref="Sound"/> and
/// <see cref="Microphone"/> say which half a placement checks.
/// </para>
/// <para>
/// The text names transmitters, not endpoints; the endpoint name appears
/// once, as the thing to pick. Endpoints are matched to transmitters by the
/// USB product id in the kernel filter path behind them, not by Windows'
/// "2- " naming, which is positional. See <see cref="Routing"/>.
/// </para>
/// </remarks>
public sealed class RoutingNotice : InfoBar
{
    public RoutingNotice()
    {
        IsClosable = false;
        Severity = InfoBarSeverity.Warning;
        Margin = new Thickness(0);

        // A closed InfoBar still counts as a child of the page's stack, so the
        // stack's spacing is added around it and leaves a blank band.
        // Collapsing it with the bar removes the gap.
        Visibility = Visibility.Collapsed;
        RegisterPropertyChangedCallback(IsOpenProperty, (_, _) =>
            Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed);

        // Opens Windows' own Sound settings. The app does not change the
        // default device itself: that is too unreliable to do on anyone's
        // behalf.
        var open = new HyperlinkButton { Content = Strings.Get("Routing_OpenSoundSettings") };
        open.Click += async (_, _) =>
        {
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:sound")); }
            catch { }
        };
        ActionButton = open;

        Loaded += (_, _) =>
        {
            // Repaint on connection status and on AudioRoute changes only, not
            // on every headset value change: those arrive several times a
            // second, and each repaint would ask Windows afresh.
            AppServices.Headset.StatusChanged += OnStatus;
            AppServices.AudioRoute.Changed += Paint;
            Paint();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged -= OnStatus;
            AppServices.AudioRoute.Changed -= Paint;
        };
    }

    public static readonly DependencyProperty SoundProperty = DependencyProperty.Register(
        nameof(Sound), typeof(bool), typeof(RoutingNotice), new PropertyMetadata(true));

    /// <summary>Check where Windows is sending sound.</summary>
    public bool Sound
    {
        get => (bool)GetValue(SoundProperty);
        set => SetValue(SoundProperty, value);
    }

    public static readonly DependencyProperty MicrophoneProperty = DependencyProperty.Register(
        nameof(Microphone), typeof(bool), typeof(RoutingNotice), new PropertyMetadata(true));

    /// <summary>Check which microphone Windows is listening to.</summary>
    public bool Microphone
    {
        get => (bool)GetValue(MicrophoneProperty);
        set => SetValue(MicrophoneProperty, value);
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    private void Paint()
    {
        var status = AppServices.Headset.Status;
        var route = AppServices.AudioRoute;
        var verdict = RoutingCheck.Judge(status, route.Output, route.Calls, route.Input,
            route.CallsInput, route.Cable, (product, output) => Routing.Belonging(product, output),
            Sound, Microphone);
        if (verdict is not { Wrong.Count: > 0 })
        {
            IsOpen = false;
            return;
        }

        // A whole sentence for each combination rather than one sentence with
        // the halves joined into it: "sending {sound and calls} to" only works
        // in English. Only names and lists are substituted.
        var wrong = verdict.Wrong;
        string here = status.Adapter;
        string there = wrong[0].OnName;
        bool sound = wrong.Any(w => w.Role == AudioRole.Sound);
        bool calls = wrong.Any(w => w.Role == AudioRole.Calls);
        bool mic = wrong.Any(w => !w.Output);

        // Not "Windows is using the Charging Dock": on Home that sits right
        // under "Connected through", and the two read as the same good news.
        Title = Strings.Format((sound, calls, mic) switch
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
            Message = (outs && mic ? Strings.Get("Routing_CableBoth")
                    : outs ? Strings.Format("Routing_CableSound", there)
                    : Strings.Get("Routing_CableMicrophone"))
                + choose;
            IsOpen = true;
            return;
        }

        // Two ways out, when there are two: CrossPlay moves the headset to the
        // transmitter Windows is already using, which is often quicker than
        // changing Windows back.
        string on = Strings.Format((sound, calls, mic) switch
        {
            (true, false, false) => "Routing_OnSilent",
            (false, true, false) => "Routing_OnNoCalls",
            (false, false, true) => "Routing_OnUnheard",
            _ => "Routing_On",
        }, here);

        Message = wrong.Any(w => w.Pick.Length == 0)
            ? Strings.Format("Routing_OnUnplugged", here, there)
            : verdict.CrossPlayFixes && picks.Count > 0
                ? on + " " + Strings.Format("Routing_PressOrChoose", there, here, Strings.List(picks))
                : on + choose;
        IsOpen = true;
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
