using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Neap.App.Controls;

/// <summary>
/// A row of a slider and what goes with it, such as its reading and a mute:
/// the slider at its preferred width while there is room, narrower when there
/// isn't, and everything else always at its own size.
/// </summary>
/// <remarks>
/// <para>
/// A settings row measures its control with unlimited width while it sits
/// beside the row's name, and with the row's own width once it wraps under
/// it. A slider given a fixed width runs its reading off the edge of a narrow
/// row; a slider left to stretch shrinks to its smallest while it sits beside
/// the name. This one takes its preferred width in the first case and gives
/// way in the second, so two rows side by side keep matching lines.
/// </para>
/// </remarks>
public sealed class SliderLine : Panel
{
    /// <summary>Gets or sets the space between items.</summary>
    public double Spacing { get; set; } = 12;

    /// <summary>Gets or sets the slider's width when there is room for it.</summary>
    public double Preferred { get; set; } = 220;

    /// <summary>Gets or sets the item that gives way when the row is short of room.</summary>
    public UIElement? Flexible { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        double fixedWidth = 0, height = 0;
        int shown = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible) continue;
            shown++;
            if (child == Flexible) continue;
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            fixedWidth += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        double gaps = Spacing * Math.Max(0, shown - 1);
        double flexible = FlexibleWidth(availableSize.Width, fixedWidth + gaps);
        if (Flexible is { Visibility: Visibility.Visible } slider)
        {
            slider.Measure(new Size(flexible, availableSize.Height));
            height = Math.Max(height, slider.DesiredSize.Height);
        }
        else flexible = 0;
        return new Size(fixedWidth + gaps + flexible, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double fixedWidth = 0;
        int shown = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible) continue;
            shown++;
            if (child != Flexible) fixedWidth += child.DesiredSize.Width;
        }
        double flexible = FlexibleWidth(finalSize.Width, fixedWidth + Spacing * Math.Max(0, shown - 1));

        double x = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible) continue;
            double width = child == Flexible ? flexible : child.DesiredSize.Width;
            double y = (finalSize.Height - child.DesiredSize.Height) / 2;
            child.Arrange(new Rect(x, Math.Max(0, y), width, child.DesiredSize.Height));
            x += width + Spacing;
        }
        return finalSize;
    }

    private double FlexibleWidth(double available, double taken) =>
        double.IsInfinity(available) ? Preferred : Math.Clamp(available - taken, 0, Preferred);
}
