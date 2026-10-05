using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Neap.Desktop.Controls;

/// <summary>One place in the navigation rail: an icon and a name.</summary>
/// <remarks>
/// The name is the item's content, so a screen reader reads it and the rail
/// shows only the icon when it is folded to the side.
/// </remarks>
public sealed class NavItem : ListBoxItem
{
    /// <summary>The icon drawn before the name.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<NavItem, Geometry?>(nameof(Icon));

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Which page the item opens.</summary>
    public string Page { get; set; } = "";

    protected override Type StyleKeyOverride => typeof(NavItem);
}
