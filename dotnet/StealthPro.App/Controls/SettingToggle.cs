using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using StealthPro.Core.Settings;

namespace StealthPro.App.Controls;

/// <summary>
/// A switch for one on-or-off headset setting, on its own, for the header
/// of a settings expander whose rows hold what goes with it.
/// </summary>
/// <remarks>
/// Hidden until the headset reports the setting, since a switch at off
/// would read as a value; the expander's header still says what it is. The
/// switch takes the name given to this control, and its automation id is
/// the setting's registry name, so a script can find it by the setting it
/// writes.
/// </remarks>
public sealed class SettingToggle : UserControl
{
    private readonly ToggleSwitch _switch = new() { OnContent = null, OffContent = null, MinWidth = 0 };
    private readonly SettingLink _link;

    public SettingToggle()
    {
        Content = _switch;
        IsTabStop = false;
        _link = new SettingLink(this, () => Setting, Build, Paint);
        _switch.Toggled += (_, _) => _link.Write(_switch.IsOn ? 1 : 0);
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(string), typeof(SettingToggle), new PropertyMetadata(""));

    /// <summary>Gets or sets the setting's registry name, such as "anc".</summary>
    public string Setting
    {
        get => (string)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    private void Build(SettingKey key)
    {
        AutomationProperties.SetName(_switch, AutomationProperties.GetName(this));
        AutomationProperties.SetAutomationId(_switch, key.Name);
    }

    private void Paint()
    {
        _switch.Visibility = _link.Value is null ? Visibility.Collapsed : Visibility.Visible;
        _switch.IsEnabled = _link.Key is { Writable: true };
        _switch.IsOn = _link.Value == 1;
    }
}
