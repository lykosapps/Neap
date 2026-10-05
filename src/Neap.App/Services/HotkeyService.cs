using System.Runtime.InteropServices;
namespace Neap.App.Services;

/// <summary>
/// Global keyboard shortcuts that move the mix from inside a game, without a
/// window and without the wheel.
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
/// <para>
/// A refused key says so. Another program holding a combination is the normal
/// failure here, and it is invisible: the keys simply do nothing. Every
/// registration is checked, and what failed is shown beside the key it failed
/// for.
/// </para>
/// </remarks>
public sealed class HotkeyService : IHotkeys
{
    private const uint ModNoRepeat = 0x4000;
    private const uint WmHotkey = 0x0312, WmQuit = 0x0012;
    private const int AlreadyRegistered = 1409;

    /// <summary>How far one press moves the mix, on the same 0-100 scale.</summary>
    private const int Step = 10;

    private readonly MixService _mix;
    private readonly object _gate = new();
    private readonly Dictionary<MixKey, Shortcut> _keys = new();
    private readonly Dictionary<MixKey, string> _trouble = new();

    private Thread? _pump;
    private uint _thread;

    public HotkeyService(MixService mix)
    {
        _mix = mix;
        foreach (var (which, shortcut) in MixShortcuts.Defaults) _keys[which] = shortcut;
        foreach (var (which, shortcut) in AppSettings.Current.Shortcuts())
            if (shortcut.Sane) _keys[which] = shortcut;
    }

    /// <summary>A key was registered, refused, changed, or given up.</summary>
    public event Action? Changed;

    public bool Supported => true;

    public bool Enabled => _pump is not null;

    public Shortcut Key(MixKey which)
    {
        lock (_gate) return _keys[which];
    }

    /// <summary>What Windows said when it refused this one, or null.</summary>
    public string? Trouble(MixKey which)
    {
        lock (_gate) return _trouble.GetValueOrDefault(which);
    }

    public void Enable(bool enabled)
    {
        if (enabled == Enabled) return;
        if (enabled) Begin(); else End();
    }

    /// <summary>
    /// Rebind one of them. Re-registers the lot rather than the one, because
    /// registration belongs to the pump thread and restarting it is simpler
    /// than talking to it.
    /// </summary>
    public void Rebind(MixKey which, Shortcut shortcut)
    {
        if (!shortcut.Sane) return;
        bool was = Enabled;
        if (was) End();
        lock (_gate) _keys[which] = shortcut;
        AppSettings.Update(s => s.SetShortcut(which, shortcut));
        if (was) Begin(); else Changed?.Invoke();
    }

    public void ResetToDefaults()
    {
        bool was = Enabled;
        if (was) End();
        lock (_gate)
            foreach (var (which, shortcut) in MixShortcuts.Defaults) _keys[which] = shortcut;
        AppSettings.Update(s => s.ClearShortcuts());
        if (was) Begin(); else Changed?.Invoke();
    }

    private void Begin()
    {
        Dictionary<MixKey, Shortcut> wanted;
        lock (_gate) wanted = new Dictionary<MixKey, Shortcut>(_keys);

        var ready = new ManualResetEventSlim();

        _pump = new Thread(() =>
        {
            _thread = GetCurrentThreadId();

            var refused = new Dictionary<MixKey, string>();
            foreach (var (which, shortcut) in wanted)
                if (!RegisterHotKey(IntPtr.Zero, (int)which + 1,
                        shortcut.Modifiers | ModNoRepeat, shortcut.Key))
                    refused[which] = Marshal.GetLastWin32Error() == AlreadyRegistered
                        ? Strings.Get("Key_Taken")
                        : Strings.Get("Key_Refused");

            lock (_gate)
            {
                _trouble.Clear();
                foreach (var (which, why) in refused) _trouble[which] = why;
            }
            ready.Set();

            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                if (message.Kind == WmHotkey)
                {
                    // The id given to RegisterHotKey above, one more than the key.
                    var which = (MixKey)(message.WParam.ToInt64() - 1);
                    if (Enum.IsDefined(which)) Platform.Post(() => Move(which));
                }

            foreach (var which in wanted.Keys) UnregisterHotKey(IntPtr.Zero, (int)which + 1);
        })
        { IsBackground = true, Name = "hotkeys" };

        _pump.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
        Changed?.Invoke();
    }

    private void End()
    {
        var going = _pump;
        _pump = null;
        if (going is null) return;
        PostThreadMessage(_thread, WmQuit, IntPtr.Zero, IntPtr.Zero);
        going.Join(TimeSpan.FromSeconds(1));
        lock (_gate) _trouble.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// Move the mix one step. It goes through <see cref="MixService.Apply"/>,
    /// so a press lands in the centre detent exactly as the wheel does,
    /// including its cue.
    /// </summary>
    private void Move(MixKey which)
    {
        if (!_mix.Running) return;
        int now = _mix.Mix;
        _mix.Apply(which switch
        {
            MixKey.TowardGame => now - Step,
            MixKey.TowardChat => now + Step,
            _ => 50,
        }, "keyboard");
    }

    public void Dispose() => End();

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
