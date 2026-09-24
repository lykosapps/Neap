using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Settings;

namespace StealthPro.App.Controls;

/// <summary>
/// The headset's microphone as one switch, on while it is live, with its
/// state in words.
/// </summary>
/// <remarks>
/// <para>
/// A switch labelled "Muted" answers "is my microphone on?" backwards: a live
/// microphone shows as a switch in the off position. So the switch is the
/// microphone, and <see cref="Microphone"/> turns it into the headset's mute.
/// Whatever holds it crosses out its microphone icon while muted
/// (<see cref="GlyphFor"/>), so the state reads without the switch's colour.
/// </para>
/// <para>
/// It follows the boom arm: flipping it up mutes the headset, and the switch
/// goes off as it happens. A dash stands in for it until the headset has
/// reported whether it is muted.
/// </para>
/// </remarks>
public sealed class MicSwitch : UserControl
{
    private readonly ToggleSwitch _switch = new()
    {
        OnContent = Strings.Get("Mic_Live"),
        OffContent = Strings.Get("Mic_Muted"),
        MinWidth = 0,
        Visibility = Visibility.Collapsed,
    };
    private readonly TextBlock _absent = new() { Text = "—", VerticalAlignment = VerticalAlignment.Center };
    private readonly SettingLink _link;

    public MicSwitch()
    {
        _absent.Style = (Style)Application.Current.Resources["SecondaryBodyTextStyle"];
        AutomationProperties.SetName(_switch, Strings.Get("Mic_Name"));
        Content = new Grid { Children = { _switch, _absent } };
        IsTabStop = false;

        _link = new SettingLink(this, () => Microphone.Setting, _ => { }, Paint);
        _switch.Toggled += (_, _) => _link.Write(Microphone.ValueFor(_switch.IsOn));
    }

    /// <summary>Raised when the microphone is found live or muted, by the headset or by the switch.</summary>
    public event Action<MicState>? StateChanged;

    /// <summary>The microphone icon for a state, crossed out while muted.</summary>
    public static string GlyphFor(MicState state) => state == MicState.Muted ? "\uF781" : "\uE720";

    private void Paint()
    {
        var state = Microphone.Of(_link.Value);
        bool known = state != MicState.Unknown;
        _switch.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
        _absent.Visibility = known ? Visibility.Collapsed : Visibility.Visible;
        _switch.IsOn = state == MicState.Live;
        StateChanged?.Invoke(state);
    }
}
