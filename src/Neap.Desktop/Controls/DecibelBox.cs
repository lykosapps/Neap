using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Neap.Core.Presets;

namespace Neap.Desktop.Controls;

/// <summary>
/// A field for one band's decibels: one decimal place, signed, stepped by the
/// arrow keys.
/// </summary>
/// <remarks>
/// <para>
/// What is typed is read leniently (<see cref="Db.Parse"/>) and taken when the
/// field is left or Enter is pressed. Anything that is not a number, including
/// a cleared field, puts back what was there rather than changing it.
/// </para>
/// <para>
/// The arrow keys step by half a decibel and Page Up and Page Down by one, for
/// exact values and for use without a mouse. The figures are of one width, so
/// the value does not jitter as it steps.
/// </para>
/// </remarks>
public sealed class DecibelBox : UserControl
{
    private const double SmallChange = 0.5, LargeChange = 1;

    private readonly TextBox _box = new()
    {
        TextAlignment = TextAlignment.Center,
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        FontFeatures = FontFeatureCollection.Parse("tnum"),
        MinWidth = 0,
        Padding = new Thickness(2, 6),
    };

    private double _value;

    public DecibelBox()
    {
        Content = _box;
        _box.KeyDown += OnKeyDown;
        _box.LostFocus += (_, _) => Commit();
    }

    /// <summary>The lowest value, in decibels.</summary>
    public double Minimum { get; set; } = -9;

    /// <summary>The highest value, in decibels.</summary>
    public double Maximum { get; set; } = 9;

    /// <summary>Raised when the person changes the value, with the new value in decibels.</summary>
    public event Action<double>? Changed;

    /// <summary>The menu for the field, which opens in place of the text box's own cut and paste.</summary>
    public ContextMenu? Menu
    {
        get => _box.ContextMenu;
        set => _box.ContextMenu = value;
    }

    /// <summary>Shows a value, without raising <see cref="Changed"/>.</summary>
    public void Show(double decibels)
    {
        _value = decibels;
        _box.Text = Db.Text((int)Math.Round(decibels * 10));
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
        if (Db.Parse(_box.Text ?? "") is double typed) Take(typed);
        else Show(_value);
    }

    private void Take(double decibels)
    {
        double clamped = Math.Round(Math.Clamp(decibels, Minimum, Maximum) * 10) / 10;
        bool changed = clamped != _value;
        Show(clamped);
        if (changed) Changed?.Invoke(clamped);
    }
}
