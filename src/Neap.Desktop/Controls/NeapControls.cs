using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>Makes Neap's own controls from code: the segmented control, the switch and the slider.</summary>
/// <remarks>
/// A segmented control is a pill holding radio buttons drawn as its parts,
/// so a screen reader says which of how many is chosen and the arrow keys
/// move the choice. The looks are Controls.axaml's.
/// </remarks>
internal static class NeapControls
{
    /// <summary>A pill around <paramref name="segments"/>, at the right of its row.</summary>
    public static Border Pill(StackPanel segments)
    {
        segments.Orientation = Orientation.Horizontal;
        return new Border { Classes = { "segmented" }, HorizontalAlignment = HorizontalAlignment.Right, Child = segments };
    }

    /// <summary>One segment of the pill, in the radio group <paramref name="group"/>.</summary>
    public static RadioButton Segment(object content, string group) => new()
    {
        Theme = Of("SegmentButton"),
        Content = content,
        GroupName = group,
    };

    /// <summary>Fills <paramref name="segments"/> with a segment for each option of an enum setting, calling <paramref name="chosen"/> with the option's value when one is ticked.</summary>
    /// <remarks>The caller guards against the ticks it makes itself while painting; see <see cref="Tick"/>.</remarks>
    public static void FillSegments(StackPanel segments, SettingKey key, Action<int> chosen)
    {
        foreach (var option in key.Options ?? new Dictionary<int, string>())
        {
            var button = Segment(OptionNames.For(key, option.Key), key.Name);
            button.Tag = option.Key;
            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked == true) chosen(option.Key);
            };
            segments.Children.Add(button);
        }
    }

    /// <summary>Ticks the segment holding <paramref name="value"/>, and none when the value is not known.</summary>
    public static void Tick(StackPanel segments, int? value)
    {
        foreach (var button in segments.Children.OfType<RadioButton>())
            button.IsChecked = button.Tag is int tag && tag == value;
    }

    /// <summary>One option of a list of choices: a full-width inset row with the radio dot inside it, in the radio group <paramref name="group"/>.</summary>
    public static RadioButton ChoiceRow(object content, string group) => new()
    {
        Theme = Of("ChoiceRow"),
        Content = content,
        GroupName = group,
    };

    /// <summary>Puts a panel of radio buttons into the accessibility tree as a group, so its name and automation id are reachable.</summary>
    /// <remarks>
    /// A plain panel is left out of the tree, and its name and id with it. As a
    /// group it is announced with its name before the options, and a script can
    /// find it by the setting it writes.
    /// </remarks>
    public static void AsGroup(Control options)
    {
        AutomationProperties.SetAccessibilityView(options, AccessibilityView.Content);
        AutomationProperties.SetControlTypeOverride(options, AutomationControlType.Group);
    }

    /// <summary>A switch with its state in words beside it, at the right of its row.</summary>
    public static ToggleSwitch Switch() => new()
    {
        Theme = Of("NeapSwitch"),
        OnContent = Strings.Get("Switch_On"),
        OffContent = Strings.Get("Switch_Off"),
        HorizontalAlignment = HorizontalAlignment.Right,
    };

    /// <summary>A slider centred in its row.</summary>
    public static Slider Slider() => new() { VerticalAlignment = VerticalAlignment.Center };

    private static ControlTheme Of(string key) => (ControlTheme)Application.Current!.FindResource(key)!;
}
