#if WINDOWS
using System.Drawing;
using System.Runtime.Versioning;
using Avalonia.Platform;
using H.NotifyIcon.Core;

namespace Neap.Desktop;

/// <summary>
/// A notification from the notification area, which Windows draws as a toast.
/// </summary>
/// <remarks>
/// <para>
/// A portable program has no shortcut for Windows to attribute a toast to, and
/// one without it is silently dropped. A notification from a notification-area
/// icon needs nothing of the sort, and is what the old app used.
/// </para>
/// <para>
/// The icon that shows it is one of its own, made for the notification and
/// taken away once it has had its time, so the one the app keeps is not
/// disturbed.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows5.1.2600")]
internal static class WindowsBalloon
{
    /// <summary>How long the icon stays after showing, which is longer than Windows shows a notification for.</summary>
    private static readonly TimeSpan Stay = TimeSpan.FromSeconds(20);

    private static TrayIcon? _icon;
    private static Icon? _picture;

    /// <summary>Shows a notification, calling <paramref name="selected"/> if it is selected.</summary>
    public static void Show(string title, string message, Action selected)
    {
        Remove();

        _picture ??= new Icon(AssetLoader.Open(new Uri("avares://Neap.Desktop/Assets/app.ico")));
        var icon = new TrayIcon { Icon = _picture.Handle, ToolTip = title };
        icon.MessageWindow.MouseEventReceived += (_, args) =>
        {
            if (args.MouseEvent == MouseEvent.BalloonToolTipClicked) selected();
        };
        icon.Create();
        icon.ShowNotification(title, message);
        _icon = icon;

        _ = Task.Delay(Stay).ContinueWith(_ =>
        {
            if (ReferenceEquals(_icon, icon)) Remove();
        }, TaskScheduler.Default);
    }

    private static void Remove()
    {
        var going = _icon;
        _icon = null;
        if (going is null) return;
        try { going.Dispose(); }
        catch (InvalidOperationException ex) { AppLog.Write($"could not take the notification's icon away: {ex.Message}"); }
    }
}
#endif
