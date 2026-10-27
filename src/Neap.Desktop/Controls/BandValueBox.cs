using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Neap.Desktop.Controls;

/// <summary>
/// A field for one value of a parametric band, typed or stepped with the arrow
/// keys, in figures of one width so the value does not jitter as it changes.
/// </summary>
/// <remarks>
/// It reads nothing itself. What is typed is handed on when the field is left or
/// Enter is pressed, and the screen answers with what to show: the value as it
/// took it, or the old one when the text was not a number. The reading is
/// <see cref="Neap.Core.Presets.BandEntry"/> and
/// <see cref="Neap.Core.Presets.FrequencyEntry"/>.
/// </remarks>
public sealed class BandValueBox : UserControl
{
    private readonly TextBox _box = new()
    {
        FontFeatures = FontFeatureCollection.Parse("tnum"),
        MinWidth = 0,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Right,
    };

    private string _shown = string.Empty;

    public BandValueBox()
    {
        Content = _box;
        _box.KeyDown += OnKeyDown;
        _box.LostFocus += (_, _) => Commit();
    }

    /// <summary>Raised with what was typed, when the field is left or Enter is pressed and the text differs from what was shown.</summary>
    public event Action<string>? Entered;

    /// <summary>Raised for an arrow key: 1 or -1, and whether it was a large step.</summary>
    public event Action<int, bool>? Stepped;

    /// <summary>Shows a value, without raising <see cref="Entered"/>, unless the person is typing in the field.</summary>
    /// <param name="text">The text to show.</param>
    public void Show(string text)
    {
        _shown = text;
        if (!_box.IsFocused) _box.Text = text;
    }

    /// <summary>Whether the field can be changed.</summary>
    public bool Editable
    {
        get => _box.IsEnabled;
        set => _box.IsEnabled = value;
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
        int step = e.Key switch { Key.Up => 1, Key.Down => -1, Key.PageUp => 1, Key.PageDown => -1, _ => 0 };
        if (step != 0)
        {
            Stepped?.Invoke(step, e.Key is Key.PageUp or Key.PageDown);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            Commit();
            e.Handled = true;
        }
    }

    private void Commit()
    {
        string typed = _box.Text ?? string.Empty;
        if (typed != _shown) Entered?.Invoke(typed);
        _box.Text = _shown;
    }
}
