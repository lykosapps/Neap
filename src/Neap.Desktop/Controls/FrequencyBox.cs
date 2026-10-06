using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Neap.Core.Presets;

namespace Neap.Desktop.Controls;

/// <summary>
/// A field for one adjustment's frequency: shown in hertz below 1 kHz and
/// kilohertz above, stepped by the arrow keys.
/// </summary>
/// <remarks>
/// <para>
/// What is typed is read leniently (<see cref="FrequencyEntry.Parse"/>) and
/// taken when the field is left or Enter is pressed, kept inside the range
/// the headset can act in. Anything that is not a number puts back what was
/// there rather than changing it.
/// </para>
/// <para>
/// The arrow keys step by one hertz and Page Up and Page Down by ten. The
/// figures are of one width, so the value does not jitter as it steps.
/// </para>
/// </remarks>
public sealed class FrequencyBox : UserControl
{
    private const double SmallChange = 1, LargeChange = 10;

    private readonly TextBox _box = new()
    {
        FontFeatures = FontFeatureCollection.Parse("tnum"),
        MinWidth = 0,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };

    private double _value = Adjustment.LowestFrequency;

    public FrequencyBox()
    {
        Content = _box;
        _box.KeyDown += OnKeyDown;
        _box.LostFocus += (_, _) => Commit();
    }

    /// <summary>Raised when the person changes the value, with the new frequency in hertz.</summary>
    public event Action<int>? Changed;

    /// <summary>Shows a frequency, without raising <see cref="Changed"/>.</summary>
    public void Show(double hertz)
    {
        _value = hertz;
        _box.Text = FrequencyText.Of(hertz);
    }

    /// <remarks>The name given to the field is the text box's, which is what a screen reader lands on.</remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AutomationProperties.NameProperty)
            AutomationProperties.SetName(_box, change.GetNewValue<string?>());
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        double? step = e.Key switch
        {
            Key.Up => SmallChange,
            Key.Down => -SmallChange,
            Key.PageUp => LargeChange,
            Key.PageDown => -LargeChange,
            _ => null,
        };
        if (step is double change)
        {
            Take(_value + change);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            Commit();
            e.Handled = true;
        }
    }

    /// <summary>Takes what was typed, or puts back what was there.</summary>
    private void Commit()
    {
        if (FrequencyEntry.Parse(_box.Text ?? "") is double typed) Take(typed);
        else Show(_value);
    }

    private void Take(double hertz)
    {
        int clamped = (int)Math.Round(Math.Clamp(hertz, Adjustment.LowestFrequency, Adjustment.HighestFrequency));
        bool changed = clamped != Math.Round(_value);
        Show(clamped);
        if (changed) Changed?.Invoke(clamped);
    }
}
