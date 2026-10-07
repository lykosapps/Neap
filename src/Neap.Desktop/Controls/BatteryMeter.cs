using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Neap.Desktop.Controls;

/// <summary>
/// A battery drawn as a bar, so how full it is shows at a glance, with its
/// percentage beside it in words.
/// </summary>
/// <remarks>
/// The bar is a picture of the number, so a screen reader is given the number
/// and not the bar. The fill turns yellow at a fifth, red at a tenth, and
/// green while charging, in the same tones as the connection dot; none of them
/// is the only way to read the level.
/// </remarks>
public sealed class BatteryMeter : UserControl
{
    private const double BodyWidth = 40;
    private const double BodyHeight = 18;
    private const double Inset = 2;
    private const int Low = 20;
    private const int Critical = 10;

    private readonly Border _fill = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        CornerRadius = new CornerRadius(1),
    };

    public BatteryMeter(string textClass)
    {
        var body = new Border
        {
            Width = BodyWidth,
            Height = BodyHeight,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(Inset - 1),
            Child = _fill,
        };
        body.Bind(Border.BorderBrushProperty, this.GetResourceObservable("NeapTextSecondaryBrush"));
        // An unlit track, so what is left shows as well as what is there.
        body.Bind(Border.BackgroundProperty, this.GetResourceObservable("NeapDialTrackBrush"));

        // The terminal on the end that makes it read as a battery.
        var nub = new Border
        {
            Width = 2,
            Height = 6,
            Margin = new Thickness(1, 0, 0, 0),
            CornerRadius = new CornerRadius(0, 1, 1, 0),
        };
        nub.Bind(Border.BackgroundProperty, this.GetResourceObservable("NeapTextSecondaryBrush"));

        Value = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Classes = { textClass } };

        var picture = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { body, nub },
        };
        AutomationProperties.SetAccessibilityView(picture, AccessibilityView.Raw);

        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { picture, Value } };
    }

    /// <summary>The percentage, or what stands in for it, beside the bar.</summary>
    public TextBlock Value { get; }

    /// <summary>Fills the bar to a charge and words it; a null charge leaves the bar empty.</summary>
    public void Show(int? percent, bool charging, string text)
    {
        Value.Text = text;
        int shown = Math.Clamp(percent ?? 0, 0, 100);
        _fill.Width = Math.Round((BodyWidth - 2 * Inset) * shown / 100);

        string tone = charging ? "NeapToneGoodBrush"
            : shown <= Critical ? "NeapToneCriticalBrush"
            : shown <= Low ? "NeapToneCautionBrush"
            : "NeapTextBrush";
        _fill.Bind(Border.BackgroundProperty, this.GetResourceObservable(tone));
    }
}
