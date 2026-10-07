using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// The wireless signal drawn as four rising bars, as many lit as the signal is
/// strong, with its word beside them.
/// </summary>
/// <remarks>
/// Drawn to the same height and gap as <see cref="BatteryMeter"/>, so the
/// readings along Home's header read as one kind of thing. The bars are a
/// picture of the word, so a screen reader is given the word.
/// </remarks>
public sealed class SignalMeter : UserControl
{
    private const int Bars = 4;
    private const double BarWidth = 5;
    private const double Rise = 4;

    private readonly Border[] _bars = new Border[Bars];

    public SignalMeter(string textClass)
    {
        var picture = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
        };
        for (int i = 0; i < Bars; i++)
        {
            _bars[i] = new Border
            {
                Width = BarWidth,
                Height = Rise * (i + 1),
                VerticalAlignment = VerticalAlignment.Bottom,
                CornerRadius = new CornerRadius(1),
            };
            picture.Children.Add(_bars[i]);
        }
        AutomationProperties.SetAccessibilityView(picture, AccessibilityView.Raw);

        Value = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Classes = { textClass } };
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { picture, Value } };
    }

    /// <summary>The word for the strength, beside the bars.</summary>
    public TextBlock Value { get; }

    /// <summary>Lights the bars for a strength, and words it.</summary>
    public void Show(SignalStrength strength, string text)
    {
        Value.Text = text;
        int lit = Bars - (int)strength;
        for (int i = 0; i < Bars; i++)
            _bars[i].Bind(Border.BackgroundProperty, this.GetResourceObservable(i < lit ? "NeapTextBrush" : "NeapDialTrackBrush"));
    }
}
