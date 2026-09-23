using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Audio;
using StealthPro.Core.Connection;

namespace StealthPro.App.Controls;

/// <summary>
/// Windows is sending sound somewhere the headset is not.
///
/// <b>Two transmitters mean two sets of audio devices</b>, and the headset
/// only uses one of them at a time. Windows keeps pointing at whichever it
/// was last told to — and moves by itself when a transmitter is plugged in or
/// pulled out — so the output, or the microphone separately, ends up aimed at
/// the one that is carrying nothing. Nothing plays, or nobody hears you, and
/// there is no clue anywhere: the device is present, enabled, and named
/// almost identically to the right one.
///
/// This cost three separate goes at diagnosing silence in one session, and
/// the third time it was already clear what class of problem it was and it
/// still had to be worked out by hand.
///
/// <b>It sits beside what it is about</b>, not at the top of the page: in the
/// Connections card on Home, under Volume on Audio, and under the microphone's
/// own section. <see cref="Sound"/> and <see cref="Microphone"/> say which
/// half a placement checks.
///
/// <b>Written as which transmitter, not which endpoint.</b> The first version
/// quoted two endpoint names per device, in full, twice over, and read as a
/// wall. People think in the hardware in front of them; the endpoint name
/// only appears once, as the thing to pick.
///
/// Endpoints are matched to transmitters by the USB product id in the
/// kernel filter path behind them, not by Windows' "2- " naming, which is
/// positional. See <see cref="Routing"/>.
/// </summary>
public sealed class RoutingNotice : InfoBar
{
    public RoutingNotice()
    {
        IsClosable = false;
        Severity = InfoBarSeverity.Warning;
        Margin = new Thickness(0);

        // <b>Closed is not gone.</b> A closed InfoBar still counts as a child
        // of the page's stack, so the stack's spacing is added around it:
        // Home carried a blank band under its title wherever a notice was
        // closed. Collapsing it with the bar takes the gap away too.
        Visibility = Visibility.Collapsed;
        RegisterPropertyChangedCallback(IsOpenProperty, (_, _) =>
            Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed);

        // The one thing to do about it, one click away. Windows' own page,
        // not a switch of ours: changing the default device from here was
        // considered and dropped as too unreliable to do on anyone's behalf.
        var open = new HyperlinkButton { Content = "Open Sound settings" };
        open.Click += async (_, _) =>
        {
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:sound")); }
            catch { }
        };
        ActionButton = open;

        Loaded += (_, _) =>
        {
            // Where Windows points is watched by AudioRoute. Every change of
            // a headset value used to repaint this instead, several times a
            // second, each time asking Windows afresh.
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

    /// <summary>A half pointed at the wrong device, in the words the notice uses.</summary>
    private sealed record Wrong(string What, bool Output, string For, string OnName, string Pick);

    private static Wrong Words(Misrouted half) => half.Role switch
    {
        AudioRole.Sound => new("sound", true, "output", half.OnName, half.Pick),
        AudioRole.Calls => new("calls", true, "communications", half.OnName, half.Pick),
        AudioRole.Microphone => new("the microphone", false, "input", half.OnName, half.Pick),
        _ => new("the microphone", false, "communications", half.OnName, half.Pick),
    };

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

        var shown = verdict.Wrong.Select(Words).ToList();
        string here = status.Adapter;
        string there = shown[0].OnName;
        var what = shown.Select(w => w.What).Distinct().ToList();

        // Not "Windows is using the Charging Dock": on Home that sits right
        // under "Connected through", and the two read as the same good news.
        Title = what is ["sound"] ? $"Windows is playing to the {there}"
            : what is ["the microphone"] ? $"Windows is using the {there}'s microphone"
            : $"Windows is sending {Join(what)} to the {there}";

        // One instruction per device to choose, saying what for, because the
        // communications default is set apart from the rest in Sound settings.
        var picks = shown.Where(w => w.Pick.Length > 0)
            .GroupBy(w => w.Pick)
            .Select(g => $"\"{g.Key}\" for {Join(g.Select(w => w.For).Distinct().ToList())}")
            .ToList();
        string choose = picks.Count > 0 ? $" In Sound settings, choose {Join(picks)}." : "";

        if (verdict.Cabled)
        {
            bool outs = shown.Any(w => w.Output), ins = shown.Any(w => !w.Output);
            Message = (outs && ins
                    ? "Your headset is plugged in with its cable. It plays one source at a time, "
                      + "and sends your voice only over the cable."
                    : outs
                        ? "Your headset is plugged in with its cable and plays one source at a "
                          + $"time, so sound sent to the {there} and to the cable won't play together."
                        : "Your headset is plugged in with its cable and sends your voice only "
                          + "over it, so nobody will hear you.")
                + choose;
            IsOpen = true;
            return;
        }

        // Two ways out, when there are two: CrossPlay moves the headset to the
        // transmitter Windows is already using, which is often quicker than
        // changing Windows back.
        string press = $"Press CrossPlay on the headset to switch it to the {there}";
        string so = what is ["sound"] ? ", so you will not hear anything"
            : what is ["calls"] ? ", so you will not hear calls"
            : what is ["the microphone"] ? ", so nobody will hear you"
            : "";

        Message = shown.Any(w => w.Pick.Length == 0)
            ? $"Your headset is on the {here}, which is not plugged in. {press}."
            : verdict.CrossPlayFixes && picks.Count > 0
                ? $"Your headset is on the {here}{so}. {press}, or to stay on the {here}, "
                  + $"choose {Join(picks)} in Sound settings."
                : $"Your headset is on the {here}{so}.{choose}";
        IsOpen = true;
    }

    /// <summary>"a", "a and b", "a, b and c".</summary>
    private static string Join(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1],
    };
}
