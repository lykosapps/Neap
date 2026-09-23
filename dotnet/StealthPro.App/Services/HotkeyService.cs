using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace StealthPro.App.Services;

/// <summary>Which way a shortcut moves the mix.</summary>
public enum MixKey { TowardGame, TowardChat, Balanced }

/// <summary>
/// One key combination. Modifiers are the RegisterHotKey flags; Key is a
/// virtual-key code.
/// </summary>
public sealed record Shortcut(uint Modifiers, uint Key)
{
    public const uint Alt = 0x0001, Control = 0x0002, Shift = 0x0004, Windows = 0x0008;

    /// <summary>
    /// <b>A shortcut without a modifier is not offered.</b> These are global:
    /// binding a bare key takes it away from every other program on the
    /// machine, and the person who did it would have no idea why their game
    /// stopped responding to it.
    /// </summary>
    public bool Sane => (Modifiers & (Alt | Control | Shift | Windows)) != 0;

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & Alt) != 0) parts.Add("Alt");
        if ((Modifiers & Shift) != 0) parts.Add("Shift");
        if ((Modifiers & Windows) != 0) parts.Add("Win");
        parts.Add(Name(Key));
        return string.Join(" + ", parts);
    }

    /// <summary>Readable names for the keys people actually pick.</summary>
    private static string Name(uint key) => key switch
    {
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x24 => "Home",
        0x23 => "End",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2D => "Insert",
        0x2E => "Delete",
        0x20 => "Space",
        0xBC => ",",
        0xBE => ".",
        0xBF => "/",
        0xDB => "[",
        0xDD => "]",
        >= 0x30 and <= 0x39 => ((char)key).ToString(),
        >= 0x41 and <= 0x5A => ((char)key).ToString(),
        >= 0x70 and <= 0x87 => $"F{key - 0x6F}",
        _ => $"key {key}",
    };
}

/// <summary>
/// Moving the mix from inside a game, without a window and without the wheel.
///
/// <b>Why this exists.</b> The headset's chat wheel only reaches the app
/// through the transmitter dock. Plugged into the small USB transmitter it
/// reports nothing at all — measured, with the volume wheel as a control — so
/// the one thing you reach for mid-game is gone and the on-screen slider is no
/// use with a game in front of it. This works on any transmitter, and
/// alongside the wheel where the wheel works.
///
/// <b>Registered against the thread, not a window.</b> A hotkey bound to the
/// main window would stop working the moment the window closed, which is most
/// of the time — the app lives in the notification area. Passing a null window
/// to RegisterHotKey binds to the calling thread instead and posts WM_HOTKEY to
/// its queue, so this owns a thread and pumps it. Registration has to happen on
/// that same thread, which is why the thread sets itself up rather than being
/// told what to do.
///
/// <b>A refused key says so.</b> Another program holding a combination is the
/// normal failure here and it is invisible — the keys simply do nothing. Every
/// registration is checked and what failed is shown beside the key it failed
/// for.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const uint ModNoRepeat = 0x4000;
    private const uint WmHotkey = 0x0312, WmQuit = 0x0012;
    private const int AlreadyRegistered = 1409;

    /// <summary>How far one press moves the mix, on the same 0-100 scale.</summary>
    private const int Step = 10;

    public static readonly IReadOnlyDictionary<MixKey, Shortcut> Defaults =
        new Dictionary<MixKey, Shortcut>
        {
            [MixKey.TowardGame] = new(Shortcut.Control | Shortcut.Alt, 0x22),   // Page Down
            [MixKey.TowardChat] = new(Shortcut.Control | Shortcut.Alt, 0x21),   // Page Up
            [MixKey.Balanced] = new(Shortcut.Control | Shortcut.Alt, 0x24),     // Home
        };

    public static string Describe(MixKey which) => which switch
    {
        MixKey.TowardGame => "Toward game",
        MixKey.TowardChat => "Toward chat",
        _ => "Back to balanced",
    };

    private readonly MixService _mix;
    private readonly DispatcherQueue _ui;
    private readonly object _gate = new();
    private readonly Dictionary<MixKey, Shortcut> _keys = new();
    private readonly Dictionary<MixKey, string> _trouble = new();

    private Thread? _pump;
    private uint _thread;

    public HotkeyService(MixService mix)
    {
        _mix = mix;
        _ui = DispatcherQueue.GetForCurrentThread();
        foreach (var (which, shortcut) in Defaults) _keys[which] = shortcut;
        foreach (var (which, shortcut) in AppSettings.Current.Shortcuts())
            if (shortcut.Sane) _keys[which] = shortcut;
    }

    /// <summary>A key was registered, refused, changed, or given up.</summary>
    public event Action? Changed;

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

    public void Enable(bool on)
    {
        if (on == Enabled) return;
        if (on) Begin(); else End();
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
            foreach (var (which, shortcut) in Defaults) _keys[which] = shortcut;
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
                        ? "another program already uses this"
                        : "Windows would not allow this one";

            lock (_gate)
            {
                _trouble.Clear();
                foreach (var (which, why) in refused) _trouble[which] = why;
            }
            ready.Set();

            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                if (message.Kind == WmHotkey)
                {
                    int id = (int)message.WParam;
                    _ui.TryEnqueue(() => Move((MixKey)(id - 1)));
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
    /// Everything goes through the mix's one Apply, so a press lands in the
    /// centre detent exactly as the wheel does — including its cue.
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
