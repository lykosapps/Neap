using Avalonia;
using Avalonia.Interactivity;
using Neap.Core.Connection;

namespace Neap.Desktop.Controls;

/// <summary>
/// A warning shown when the system is sending sound to, or listening
/// through, a transmitter the headset is not using.
/// </summary>
/// <remarks>
/// <para>
/// The notice sits beside what it is about rather than at the top of the
/// page: under the headset's name on Home, under Volume on Audio, and under
/// the microphone's own section. <see cref="Sound"/> and
/// <see cref="Microphone"/> say which half a placement checks. What it says
/// is <see cref="RoutingStory"/>'s.
/// </para>
/// <para>
/// It opens the system's own sound settings. The app does not change the
/// default device itself: that is too unreliable to do on anyone's behalf.
/// </para>
/// </remarks>
public sealed class RoutingNotice : Notice
{
    public static readonly StyledProperty<bool> SoundProperty =
        AvaloniaProperty.Register<RoutingNotice, bool>(nameof(Sound), defaultValue: true);

    public static readonly StyledProperty<bool> MicrophoneProperty =
        AvaloniaProperty.Register<RoutingNotice, bool>(nameof(Microphone), defaultValue: true);

    public RoutingNotice()
    {
        Severity = Severity.Warning;
        ActionText = Strings.Get("Routing_OpenSoundSettings");
        ActionInvoked += (_, _) =>
        {
            try { SystemTools.OpenSoundSettings(); }
            catch (Exception ex) { AppLog.Write($"could not open the sound settings: {ex.Message}"); }
        };
    }

    protected override Type StyleKeyOverride => typeof(Notice);

    /// <summary>Check where the system is sending sound.</summary>
    public bool Sound
    {
        get => GetValue(SoundProperty);
        set => SetValue(SoundProperty, value);
    }

    /// <summary>Check which microphone the system is listening to.</summary>
    public bool Microphone
    {
        get => GetValue(MicrophoneProperty);
        set => SetValue(MicrophoneProperty, value);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Repaint on connection status and on route changes only, not on
        // every headset value change: those arrive several times a second,
        // and each repaint would ask the system afresh.
        AppServices.Headset.StatusChanged += OnStatus;
        AppServices.AudioRoute.Changed += Paint;
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.StatusChanged -= OnStatus;
        AppServices.AudioRoute.Changed -= Paint;
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
