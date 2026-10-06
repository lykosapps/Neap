using Avalonia;
using Avalonia.Controls;
using Neap.Core;

namespace Neap.Desktop;

/// <summary>Where the window opens: where it was left, or centred on the main screen the first time.</summary>
/// <remarks>
/// <para>
/// The first size is big enough for the equaliser's ten bands side by side,
/// the widest thing in the app and the one that reads badly when cramped.
/// Placements are kept in pixels, which is how a screen's own work area is
/// given, and the window is sized in the units its toolkit uses.
/// </para>
/// <para>
/// A pretend run always opens at the first size, so its screenshots can be
/// compared from one run to the next.
/// </para>
/// </remarks>
public partial class MainWindow
{
    private const int FirstWidth = 1180, FirstHeight = 900;

    /// <summary>Opens the window where it was left, kept inside a screen that still exists.</summary>
    private void Place()
    {
        if (Screens.Primary is not { } primary) return;
        double scale = primary.Scaling;

        Box? saved = !Pretend.Active && AppSettings.Current.Window is [int x, int y, int w, int h]
            ? new Box(x, y, w, h) : null;
        Box? savedArea = null;
        if (saved is { } box)
        {
            var (cx, cy) = WindowPlacement.Centre(box);
            savedArea = Screens.ScreenFromPoint(new PixelPoint(cx, cy)) is { } screen ? Of(screen.WorkingArea) : null;
        }

        var place = WindowPlacement.Fit(saved, savedArea, Of(primary.WorkingArea),
            (int)(FirstWidth * scale), (int)(FirstHeight * scale));
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint(place.X, place.Y);
        Width = place.Width / scale;
        Height = place.Height / scale;

        static Box Of(PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>Records where the window is, for the next launch. Not while maximised or minimised.</summary>
    private void Remember()
    {
        if (Pretend.Active || WindowState != WindowState.Normal) return;
        double scale = RenderScaling;
        var (at, width, height) = (Position, (int)(Bounds.Width * scale), (int)(Bounds.Height * scale));
        AppSettings.Update(s => s.Window = [at.X, at.Y, width, height]);
    }
}
