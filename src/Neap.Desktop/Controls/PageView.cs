using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Neap.Desktop.Controls;

/// <summary>A page's frame: its content in one column, held to a readable width and scrolled when it does not fit.</summary>
/// <remarks>
/// A control rather than a style, because the scroll, the margin and the
/// column are three elements; the pages then list only their own content,
/// and a change to the frame is made once.
/// </remarks>
public sealed class PageView : ContentControl
{
    /// <summary>Gets the scroller the column sits in, once the control is shown.</summary>
    public ScrollViewer? Scroller { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Scroller = e.NameScope.Find<ScrollViewer>("Scroller");
    }
}
