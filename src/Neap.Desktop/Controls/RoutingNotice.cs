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
/// On Windows it makes the choices it names, as Windows 11's Settings app
/// cannot choose the device for calls; if Windows refuses one, or on Linux,
/// it opens the system's own sound settings instead.
/// </para>
/// </remarks>
public sealed class RoutingNotice : Notice
{
    public static readonly StyledProperty<bool> SoundProperty =
        AvaloniaProperty.Register<RoutingNotice, bool>(nameof(Sound), defaultValue: true);

    public static readonly StyledProperty<bool> MicrophoneProperty =
        AvaloniaProperty.Register<RoutingNotice, bool>(nameof(Microphone), defaultValue: true);

    private IReadOnlyList<DefaultChoice> _choices = [];

    public RoutingNotice()
    {
        Severity = Severity.Warning;
        ActionInvoked += async (_, _) => await Act();
    }

    private async Task Act()
    {
        var choices = _choices;
        if (choices.Count > 0)
        {
            try
            {
                if (await Task.Run(() => SystemTools.SetDefaults(choices)))
                {
                    AppLog.Write($"routing: made {string.Join(", ", choices)} the system's default");
                    return;
                }
                AppLog.Write("routing: Windows did not take every default, so its sound settings are opened");
            }
            catch (Exception ex) { AppLog.Write($"routing: could not set the system's defaults, so its sound settings are opened: {ex.Message}"); }
        }
        try { SystemTools.OpenSoundSettings(); }
        catch (Exception ex) { AppLog.Write($"could not open the sound settings: {ex.Message}"); }
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
        _choices = OperatingSystem.IsWindows() ? story.Choices : [];
        ActionText = Strings.Get(_choices.Count > 0 ? "Routing_ChooseForMe" : "Routing_OpenSoundSettings");
        IsOpen = true;
    }
}
