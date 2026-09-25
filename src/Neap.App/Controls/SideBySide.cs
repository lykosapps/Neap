using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Neap.App.Controls;

/// <summary>
/// Two cards side by side when there is room for both, and one above the
/// other when there is not.
/// </summary>
/// <remarks>
/// A settings row narrower than its slider and reading cuts the reading off,
/// so two rows share the width only while each still has room for them.
/// </remarks>
public sealed class SideBySide : Grid
{
    /// <summary>The narrowest width at which two rows, each with an icon, a slider and its reading, fit side by side.</summary>
    private const double BesideFrom = 740;

    public SideBySide()
    {
        ColumnSpacing = 12;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        SizeChanged += (_, _) => Arrange();
    }

    private void Arrange()
    {
        bool beside = ActualWidth >= BesideFrom;
        RowSpacing = beside ? 0 : 4;
        for (int i = 0; i < Children.Count && i < 2; i++)
        {
            var child = (FrameworkElement)Children[i];
            SetColumn(child, beside ? i : 0);
            SetRow(child, beside ? 0 : i);
            SetColumnSpan(child, beside ? 1 : 2);
        }
    }
}
