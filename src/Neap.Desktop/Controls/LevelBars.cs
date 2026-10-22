using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;

namespace Neap.Desktop.Controls;

/// <summary>
/// A row of small bars that light from the left to show a live level.
/// </summary>
/// <remarks>
/// The bars share the row's width, so a row given a width keeps its count and
/// spreads them out. The lit colour comes from a style class on the row
/// (<c>game</c> or <c>chat</c>, the accent otherwise), so a theme change
/// repaints it without code. It is decoration for the eye: whatever it sits
/// beside carries the reading for a screen reader.
/// </remarks>
public sealed class LevelBars : UserControl
{
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<LevelBars, int>(nameof(Count), defaultValue: 24);

    private readonly UniformGrid _row = new() { Rows = 1 };
    private int _shown = -1;

    public LevelBars()
    {
        Content = _row;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        Build();
    }

    /// <summary>Gets or sets how many bars the row has.</summary>
    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty) Build();
    }

    private void Build()
    {
        _row.Children.Clear();
        _row.Columns = Count;
        for (int i = 0; i < Count; i++)
            _row.Children.Add(new Rectangle { Height = 6, RadiusX = 1, RadiusY = 1, Margin = new Thickness(Space.Hairline / 2, 0) });
        _shown = -1;
        Paint(0);
    }

    /// <summary>Gets how many bars are lit.</summary>
    public int Lit => Math.Max(_shown, 0);

    /// <summary>Lights the first bars, restyling only when the count changed.</summary>
    public void Paint(int lit)
    {
        if (lit == _shown) return;
        for (int i = 0; i < _row.Children.Count; i++)
        {
            _row.Children[i].Classes.Set("lit", i < lit);
            _row.Children[i].Classes.Set("unlit", i >= lit);
        }
        _shown = lit;
    }
}
