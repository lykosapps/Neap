using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Neap.Core;
using Neap.Core.Mix;

namespace Neap.Desktop;

/// <summary>
/// The mix shown for a moment at the foot of the screen as the headset's
/// wheel or the keys move it, over a game, without taking the keyboard or a
/// click.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MixOverlay"/> decides when it shows and where. One window is
/// made the first time and shown again for each move, so a spin of the wheel
/// moves one picture rather than opening a window per step.
/// </para>
/// <para>
/// Windows only: there the window can be told never to take the keyboard and
/// to let clicks through to the game under it, and the system says when a
/// game has the screen to itself. Elsewhere it is not made.
/// </para>
/// </remarks>
public partial class MixOverlayWindow : Window
{
    private const int ExtendedStyle = -20;
    private const int Transparent = 0x20, ToolWindow = 0x80, Layered = 0x80000, NoActivate = 0x08000000;
    private const uint ByAlpha = 0x2;
    private const int RunningFullScreen = 3, PresentationMode = 4;

    private static MixOverlayWindow? _shown;
    private static bool _saidScreenUnknown;

    private readonly DispatcherTimer _going = new() { Interval = MixOverlay.ShownFor };

    public MixOverlayWindow()
    {
        InitializeComponent();
        Title = Strings.Get("MixOverlay_Name");
        _going.Tick += (_, _) =>
        {
            _going.Stop();
            Hide();
        };
        Opened += (_, _) => PassThrough();
        SizeChanged += (_, _) => Place();
    }

    /// <summary>Shows the overlay as the mix moves, from now on, where the system allows it.</summary>
    public static void Watch()
    {
        if (!OperatingSystem.IsWindows()) return;
        AppServices.Mix.Moved += OnMoved;
    }

    private static void OnMoved(MixCause cause)
    {
        if (!MixOverlay.Shows(cause, AppSettings.Current.MixOverlay, NeapInFront(), ScreenTaken())) return;
        _shown ??= new MixOverlayWindow();
        _shown.ShowMix(AppServices.Mix.Mix);
    }

    /// <summary>Shows the overlay once, with the mix as it is, as its switch is turned on.</summary>
    /// <remarks>
    /// Shown though Neap is in front, where a move of the mix would not show
    /// it: the switch is in Neap, and without this the person never sees what
    /// they turned on until a game is running.
    /// </remarks>
    public static void Preview()
    {
        if (!OperatingSystem.IsWindows()) return;
        _shown ??= new MixOverlayWindow();
        _shown.ShowMix(AppServices.Mix.Mix);
    }

    /// <summary>Draws the mix, shows the window if it is not showing, puts it in place, and starts its time over.</summary>
    /// <remarks>Put in place each time, as the screens, their scaling or the taskbar can change while Neap runs.</remarks>
    private void ShowMix(int mix)
    {
        Paint(mix);
        if (!IsVisible) Show();
        Place();
        _going.Stop();
        _going.Start();
    }

    private void Paint(int mix)
    {
        double width = Track.Width;
        double middle = width / 2, at = width * mix / 100;
        var lean = MixLevels.Lean(mix);
        Canvas.SetLeft(Fill, Math.Min(middle, at));
        Fill.Width = Math.Abs(at - middle);
        Fill.IsVisible = lean != MixLean.Balanced;
        Fill.Classes.Set("game", lean is MixLean.GameOnly or MixLean.TowardGame);
        Fill.Classes.Set("chat", lean is MixLean.ChatOnly or MixLean.TowardChat);
        Canvas.SetLeft(Mark, Math.Clamp(at - Mark.Width / 2, 0, width - Mark.Width));
    }

    /// <summary>Puts it at the foot of the main screen, clear of the system's volume flyout.</summary>
    private void Place()
    {
        if (Screens.Primary is not { } screen || Bounds.Height <= 0) return;
        double scale = RenderScaling;
        var work = screen.WorkingArea;
        var box = MixOverlay.Place(new Box(work.X, work.Y, work.Width, work.Height),
            (int)Math.Ceiling(Bounds.Width * scale), (int)Math.Ceiling(Bounds.Height * scale),
            (int)Math.Round(MixOverlay.SystemFlyoutBand * scale));
        var position = new PixelPoint(box.X, box.Y);
        if (Position != position) Position = position;
    }

    /// <summary>Tells the system the window never takes the keyboard, is no app of its own, and lets clicks through.</summary>
    /// <remarks>Clicks pass through only a layered window, so it is made one, drawn fully opaque.</remarks>
    private void PassThrough()
    {
        if (TryGetPlatformHandle()?.Handle is not { } handle)
        {
            AppLog.Write("mix overlay: the window has no handle, so clicks on it do not pass through");
            return;
        }
        int style = GetWindowLong(handle, ExtendedStyle);
        if (SetWindowLong(handle, ExtendedStyle, style | Transparent | ToolWindow | Layered | NoActivate) == 0
            || !SetLayeredWindowAttributes(handle, 0, 255, ByAlpha))
            AppLog.Write($"mix overlay: could not let clicks through: error {Marshal.GetLastWin32Error()}");
    }

    /// <summary>Whether the window in front is one of Neap's.</summary>
    /// <remarks>
    /// Asked of the system rather than taken from <see cref="WindowPresence"/>,
    /// which counts Neap as in front until a window of its own has come
    /// forward: started at sign-in into the notification area, no window has.
    /// No window in front, as while the screen is locked, is none of Neap's.
    /// </remarks>
    private static bool NeapInFront() =>
        GetWindowThreadProcessId(GetForegroundWindow(), out uint process) != 0 && process == Environment.ProcessId;

    /// <summary>Whether an exclusive full-screen game or a presentation has the screen.</summary>
    /// <remarks>Where the system cannot say, it is taken as having it: a game knocked out of full screen costs more than a missed overlay.</remarks>
    private static bool ScreenTaken()
    {
        int result = SHQueryUserNotificationState(out int state);
        if (result == 0) return state is RunningFullScreen or PresentationMode;
        if (!_saidScreenUnknown)
            AppLog.Write($"mix overlay: the system did not say whether a game has the screen (0x{result:X8}), so it is not shown");
        _saidScreenUnknown = true;
        return true;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr window, int index, int value);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr window, uint key, byte alpha, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
