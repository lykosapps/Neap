using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Neap.Desktop.Controls;

/// <summary>
/// A battery drawn as a bar, so how full it is shows at a glance, with its
/// percentage beside it in words.
/// </summary>
/// <remarks>
/// The bar is a picture of the number, so a screen reader is given the number
/// and not the bar. The fill turns yellow at a fifth and red at a tenth. While
/// charging it is green and a bolt stands beside it, named for a screen
/// reader. None of the colours is the only way to read the level or the
/// charging.
/// </remarks>
public sealed class BatteryMeter : UserControl
{
    private const double BodyWidth = 32;
    private const double BodyHeight = 16;
    private const double Inset = 2;
    private const int Low = 20;
    private const int Critical = 10;

    private readonly Border _fill = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        CornerRadius = new CornerRadius(1),
    };

    private readonly StackPanel _picture;
    private readonly PathIcon _bolt;

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

        _picture = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { body, nub },
        };
        AutomationProperties.SetAccessibilityView(_picture, AccessibilityView.Raw);

        // Said as well as shown, so charging is not only a colour and a picture.
        _bolt = new PathIcon
        {
            Width = 14,
            Height = 14,
            IsVisible = false,
            Data = (Geometry)Application.Current!.FindResource("IconCharging")!,
        };
        _bolt.Bind(TemplatedControl.ForegroundProperty, this.GetResourceObservable("NeapToneGoodBrush"));
        AutomationProperties.SetName(_bolt, Strings.Get("Battery_Charging"));

        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _picture, _bolt, Value } };
    }

    /// <summary>The percentage, or what stands in for it, beside the bar.</summary>
    public TextBlock Value { get; }

    /// <summary>Fills the bar to a charge and words it; with no charge to show there is no bar, as an empty one would say the battery is empty.</summary>
    public void Show(int? percent, bool charging, string text)
    {
        Value.Text = text;
        _bolt.IsVisible = charging;
        _picture.IsVisible = percent is not null;
        int shown = Math.Clamp(percent ?? 0, 0, 100);
        _fill.Width = Math.Round((BodyWidth - 2 * Inset) * shown / 100);

        string tone = charging ? "NeapToneGoodBrush"
            : shown <= Critical ? "NeapToneCriticalBrush"
            : shown <= Low ? "NeapToneCautionBrush"
            : "NeapTextBrush";
        _fill.Bind(Border.BackgroundProperty, this.GetResourceObservable(tone));
    }
}
