using Avalonia;
using Avalonia.Automation;
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

    public static readonly StyledProperty<Control?> BelowProperty =
        AvaloniaProperty.Register<SettingsCard, Control?>(nameof(Below));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SettingsCard, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<double> SideBySideWidthProperty =
        AvaloniaProperty.Register<SettingsCard, double>(nameof(SideBySideWidth), 560);

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

    /// <summary>Something under the name and its line, such as a level meter or a caution, or none.</summary>
    public Control? Below
    {
        get => GetValue(BelowProperty);
        set => SetValue(BelowProperty, value);
    }

    /// <summary>The icon before the name, or none.</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// The width below which the control moves under the name, so neither is
    /// squeezed. A panel whose rows are short, like the mini panel, sets it
    /// to zero in a style to keep a switch beside its name.
    /// </summary>
    public double SideBySideWidth
    {
        get => GetValue(SideBySideWidthProperty);
        set => SetValue(SideBySideWidthProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(SettingsCard);

    private Grid? _layout;
    private Control? _control;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layout = e.NameScope.Find<Grid>("Layout");
        _control = e.NameScope.Find<Control>("Control");
        Place(Bounds.Width);
    }

    /// <summary>
    /// Names the control for a screen reader after the row, and gives it the
    /// row's description: a switch or a button alone says what it does only
    /// through the text beside it, which a screen reader does not reach.
    /// </summary>
    /// <remarks>A name or description the control has of its own is kept.</remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != HeaderProperty && change.Property != DescriptionProperty && change.Property != ContentProperty) return;
        if (Content is not Control control) return;
        if (!string.IsNullOrEmpty(Header) && string.IsNullOrEmpty(AutomationProperties.GetName(control)))
            AutomationProperties.SetName(control, Header);
        if (!string.IsNullOrEmpty(Description) && string.IsNullOrEmpty(AutomationProperties.GetHelpText(control)))
            AutomationProperties.SetHelpText(control, Description);
    }

    // Placed while measuring, so the control is measured where it is drawn; placed
    // during arrange, it would keep the size measured for the other place.
    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width)) Place(availableSize.Width);
        return base.MeasureOverride(availableSize);
    }

    /// <summary>Puts the control beside the name when there is room, and under it when there is not.</summary>
    private void Place(double width)
    {
        if (_layout is null || _control is null || width <= 0) return;
        bool beside = width >= SideBySideWidth;
        Grid.SetRow(_control, beside ? 0 : 1);
        Grid.SetColumn(_control, beside ? 2 : 1);
        Grid.SetColumnSpan(_control, beside ? 1 : 2);
        _control.HorizontalAlignment = beside ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        // A page may give the control a width so controls line up in a column beside their names.
        // Under the name there is no column, and the control starts where the name does.
        if (beside) _control.ClearValue(MinWidthProperty);
        else _control.MinWidth = 0;

        // The gap between name and control belongs to the control, so a row with no icon has no gap before its name.
        // Likewise the gap above it when it sits under the name, so a row with nothing under it has no spare room.
        _control.Margin = beside ? new Thickness(Space.M, 0, 0, 0) : new Thickness(0, Space.S, 0, 0);
    }
}
