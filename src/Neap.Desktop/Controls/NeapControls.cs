using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

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

    /// <summary>One option of a list of choices: a full-width inset row with the radio dot inside it, in the radio group <paramref name="group"/>.</summary>
    public static RadioButton ChoiceRow(object content, string group) => new()
    {
        Theme = Of("ChoiceRow"),
        Content = content,
        GroupName = group,
    };

    /// <summary>A switch with its state in words beside it, at the right of its row.</summary>
    public static ToggleSwitch Switch() => new()
    {
        Theme = Of("NeapSwitch"),
        OnContent = Strings.Get("Switch_On"),
        OffContent = Strings.Get("Switch_Off"),
        HorizontalAlignment = HorizontalAlignment.Right,
    };

    /// <summary>A slider with Neap's thicker track.</summary>
    public static Slider Slider() => new() { Theme = Of("NeapSlider"), VerticalAlignment = VerticalAlignment.Center };

    private static ControlTheme Of(string key) => (ControlTheme)Application.Current!.FindResource(key)!;
}
