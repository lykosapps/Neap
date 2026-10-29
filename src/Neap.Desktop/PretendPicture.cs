using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Neap.Desktop;

/// <summary>Saves a picture of a list open over one of the app's windows, for a pretend run's test script.</summary>
/// <remarks>
/// An open list is a window of its own, which a picture of the main window
/// leaves out and the system draws black for a script on a desktop of its
/// own, so the app draws it instead.
/// </remarks>
internal static class PretendPicture
{
    public static bool Save(string file) => Dispatcher.UIThread.Invoke(() =>
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return false;
        var open = desktop.Windows.SelectMany(window => window.GetVisualDescendants().OfType<Popup>())
            .FirstOrDefault(popup => popup.IsOpen && popup.Child is { Bounds.Width: > 0 });
        if (open?.Child is not { } list) return false;
        using var picture = new RenderTargetBitmap(new PixelSize((int)list.Bounds.Width, (int)list.Bounds.Height), new Vector(96, 96));
        picture.Render(list);
        picture.Save(file, new PngBitmapEncoderOptions());
        AppLog.Write($"saved a picture of an open list to {file}");
        return true;
    });
}
