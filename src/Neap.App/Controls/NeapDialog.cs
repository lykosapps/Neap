using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Neap.App.Controls;

/// <summary>A dialog in Neap's colours rather than Windows' grey.</summary>
/// <remarks>
/// A dialog takes its colours from its own style's dictionary before the
/// app's, and it opens outside the page, so the colours the pages get never
/// reach it. It carries Themes/Controls.xaml in its own resources instead,
/// which it looks in first, so it follows a change of theme as the pages do.
/// </remarks>
public sealed class NeapDialog : ContentDialog
{
    public NeapDialog()
    {
        DefaultStyleKey = typeof(ContentDialog);
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/Controls.xaml") });
    }
}
