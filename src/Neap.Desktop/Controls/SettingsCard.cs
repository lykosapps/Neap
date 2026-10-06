using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Neap.Desktop.Controls;

/// <summary>
/// One row of settings: an icon, a name, a line saying what it does, and the
/// control it is set with on the right.
/// </summary>
/// <remarks>
/// The same row on every page, so a setting looks like a setting wherever it
/// is. Its look is in Neap.axaml.
/// </remarks>
public class SettingsCard : ContentControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SettingsCard, string?>(nameof(Header));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsCard, string?>(nameof(Description));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SettingsCard, Geometry?>(nameof(Icon));

    /// <summary>What the setting is called.</summary>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>One line under the name saying what the setting does.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>The icon before the name, or none.</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(SettingsCard);

    /// <summary>The width below which the control moves under the name, so neither is squeezed.</summary>
    private const double Wide = 560;

    private Grid? _layout;
    private Control? _control;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layout = e.NameScope.Find<Grid>("Layout");
        _control = e.NameScope.Find<Control>("Control");
        Place(Bounds.Width);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Place(finalSize.Width);
        return base.ArrangeOverride(finalSize);
    }

    /// <summary>Puts the control beside the name when there is room, and under it when there is not.</summary>
    private void Place(double width)
    {
        if (_layout is null || _control is null || width <= 0) return;
        bool beside = width >= Wide;
        Grid.SetRow(_control, beside ? 0 : 1);
        Grid.SetColumn(_control, beside ? 2 : 1);
        Grid.SetColumnSpan(_control, beside ? 1 : 2);
        _control.HorizontalAlignment = beside ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }
}
