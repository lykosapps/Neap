using System.Runtime.InteropServices;
using System.Runtime.Versioning;
namespace Neap.Services;

/// <summary>
/// Global keyboard shortcuts that move the mix from inside a game, without a
/// window and without the wheel, taken from Windows.
/// </summary>
/// <remarks>
/// <para>
/// The headset's chat wheel only reaches the app through the Charging Dock.
/// Through the USB Transmitter it reports nothing at all (measured, with the
/// volume wheel as a control), so the one thing you reach for mid-game is gone
/// and the on-screen dial is no use with a game in front of it. Shortcuts
/// work on any transmitter, and alongside the wheel where the wheel works.
/// </para>
/// <para>
/// Keys are registered against a thread, not a window. A hotkey bound to the
/// main window stops working the moment the window closes, which is most of
/// the time: the app lives in the notification area. Passing a null window to
/// RegisterHotKey binds to the calling thread and posts WM_HOTKEY to its queue,
/// so this owns a thread and pumps it. Registration has to happen on that same
/// thread, which is why the thread sets itself up rather than being told what
/// to do.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class HotkeyService : MixHotkeys
{
    private const uint ModNoRepeat = 0x4000;
    private const uint WmHotkey = 0x0312, WmQuit = 0x0012;
    private const int AlreadyRegistered = 1409;

    private Thread? _pump;
    private uint _thread;

    public HotkeyService(MixService mix) : base(mix)
    {
    }

    protected override IReadOnlyDictionary<MixKey, string> Start(IReadOnlyDictionary<MixKey, Shortcut> wanted)
    {
        var refused = new Dictionary<MixKey, string>();
        var ready = new ManualResetEventSlim();

        _pump = new Thread(() =>
        {
            _thread = GetCurrentThreadId();

            foreach (var (which, shortcut) in wanted)
                if (!RegisterHotKey(IntPtr.Zero, (int)which + 1,
                        shortcut.Modifiers | ModNoRepeat, shortcut.Key))
                    refused[which] = Marshal.GetLastWin32Error() == AlreadyRegistered
                        ? Strings.Get("Key_Taken")
                        : Strings.Get("Key_Refused");
            ready.Set();

            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                if (message.Kind == WmHotkey)
                {
                    // The id given to RegisterHotKey above, one more than the key.
                    var which = (MixKey)(message.WParam.ToInt64() - 1);
                    if (Enum.IsDefined(which)) Pressed(which);
                }

            foreach (var which in wanted.Keys) UnregisterHotKey(IntPtr.Zero, (int)which + 1);
        })
        { IsBackground = true, Name = "hotkeys" };

        _pump.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
        return refused;
    }

    protected override void LetGo()
    {
        var going = _pump;
        _pump = null;
        if (going is null) return;
        PostThreadMessage(_thread, WmQuit, IntPtr.Zero, IntPtr.Zero);
        going.Join(TimeSpan.FromSeconds(1));
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Message message, IntPtr window, uint first, uint last);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint thread, uint message, IntPtr w, IntPtr l);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    /// <summary>MSG, as Windows lays it out.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Window;
        public uint Kind;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X, Y;
    }
}
