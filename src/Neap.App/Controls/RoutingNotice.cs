using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Neap.Core.Connection;

namespace Neap.App.Controls;

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
/// page: under the headset's name on Home, under Volume on Audio, and under
/// the microphone's own section. <see cref="Sound"/> and
/// <see cref="Microphone"/> say which half a placement checks.
/// </para>
/// <para>
/// The text names transmitters, not endpoints; the endpoint name appears
/// once, as the thing to pick. Endpoints are matched to transmitters by the
/// USB product id in the kernel filter path behind them, not by Windows'
/// "2- " naming, which is positional. See <see cref="RoutingStory"/>.
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
            catch (Exception ex) { AppLog.Write($"could not open Sound settings: {ex.Message}"); }
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
        var story = RoutingStory.Of(AppServices.Headset.Status, AppServices.AudioRoute, Sound, Microphone);
        if (story is null)
        {
            IsOpen = false;
            return;
        }

        Title = story.Title;
        Message = story.Message;
        IsOpen = true;
    }
}
