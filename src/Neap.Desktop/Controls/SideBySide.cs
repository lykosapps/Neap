using Avalonia.Controls;

namespace Neap.Desktop.Controls;

/// <summary>
/// Two cards side by side when there is room for both, and one above the
/// other when there is not.
/// </summary>
/// <remarks>
/// <para>
/// A settings row narrower than its slider and reading cuts the reading off,
/// so two rows share the width only while each still has room for them.
/// </para>
/// <para>
/// A card hidden, as for a function the headset doesn't have, gives up its
/// place, so the other moves to the first rather than leaving a gap beside it.
/// </para>
/// </remarks>
public sealed class SideBySide : Grid
{
    /// <summary>The narrowest width at which two rows, each with an icon, a slider and its reading, fit side by side.</summary>
    private const double BesideFrom = 740;

    public SideBySide()
    {
        ColumnSpacing = 12;
        ColumnDefinitions = new ColumnDefinitions("*,*");
        RowDefinitions = new RowDefinitions("Auto,Auto");
        SizeChanged += (_, _) => Arrange();
        Children.CollectionChanged += (_, e) =>
        {
            foreach (var child in e.NewItems?.OfType<Control>() ?? [])
                child.PropertyChanged += (_, change) =>
                {
                    if (change.Property == IsVisibleProperty) Arrange();
                };
        };
    }

    protected override Type StyleKeyOverride => typeof(Grid);

    private void Arrange()
    {
        bool beside = Bounds.Width >= BesideFrom;
        RowSpacing = beside ? 0 : 4;
        var shown = Children.Where(child => child.IsVisible).Take(2).ToList();
        for (int i = 0; i < shown.Count; i++)
        {
            var child = shown[i];
            SetColumn(child, beside ? i : 0);
            SetRow(child, beside ? 0 : i);
            SetColumnSpan(child, beside ? 1 : 2);
        }
    }
}
