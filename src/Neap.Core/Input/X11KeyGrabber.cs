using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Neap.Core.Input;

/// <summary>Why a key could not be taken.</summary>
public enum GrabFailure
{
    /// <summary>Another program already holds that combination.</summary>
    Taken,

    /// <summary>The system would not give it, or has no such key.</summary>
    Refused,
}

/// <summary>One key combination to take for the whole desktop.</summary>
/// <param name="Id">What is reported when it is pressed.</param>
/// <param name="KeySym">The key, as X names it.</param>
/// <param name="Modifiers">The modifier bits, from <see cref="X11Keys"/>.</param>
public readonly record struct KeyGrab(int Id, uint KeySym, uint Modifiers);

/// <summary>
/// Takes key combinations from the X window system for the whole desktop, so a
/// press is heard whichever program is in front, and reports each one.
/// </summary>
/// <remarks>
/// <para>
/// Works on a connection of its own, on a thread of its own, so it keeps
/// listening with the window closed. A key is taken with its lock keys both
/// on and off, since NumLock or CapsLock being on would otherwise stop it.
/// </para>
/// <para>
/// X says a key is already held by another program with an error that
/// arrives on its own later, and Xlib's default is to end the program on
/// one. So the error is caught for the length of each request, the way
/// Xlib's own documentation has it, and the previous handler is put back.
/// </para>
/// <para>
/// A desktop on Wayland has no keys for the whole desktop; its X programs, as
/// most games are, are on a compatibility layer that hears a press only while
/// one of them is in front. That is as far as this reaches there.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed class X11KeyGrabber : IDisposable
{
    private const string Library = "libX11.so.6";
    private const int KeyPress = 2, BadAccess = 10, GrabModeAsync = 1;
    private const int EventSize = 192, StateOffset = 80, KeycodeOffset = 84, ErrorCodeOffset = 32;

    private static readonly Lazy<bool> CanConnect = new(Connects);
    private static readonly ErrorHandler Handler = OnError;
    private static int _lastError;

    private readonly Action<int> _pressed;
    private readonly Dictionary<int, GrabFailure> _failed = new();
    private readonly Thread _thread;
    private volatile bool _stopping;

    private delegate int ErrorHandler(IntPtr display, IntPtr error);

    /// <summary>Whether this system has an X display to take keys from, and the library to talk to it.</summary>
    public static bool Available => OperatingSystem.IsLinux() && CanConnect.Value;

    /// <summary>Starts listening for these keys, and waits to learn which could be taken.</summary>
    /// <param name="keys">The combinations to take.</param>
    /// <param name="pressed">Called, on this thread, with the id of each key as it is pressed.</param>
    public X11KeyGrabber(IReadOnlyList<KeyGrab> keys, Action<int> pressed)
    {
        _pressed = pressed;
        var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Listen(keys, ready)) { IsBackground = true, Name = "x11-keys" };
        _thread.Start();

        // A handle the thread has not yet set is left to it: disposing under it would fail its Set.
        if (ready.Wait(TimeSpan.FromSeconds(2))) ready.Dispose();
    }

    /// <summary>The keys that could not be taken, and why. Settled once construction returns.</summary>
    public IReadOnlyDictionary<int, GrabFailure> Failed
    {
        get
        {
            lock (_failed) return new Dictionary<int, GrabFailure>(_failed);
        }
    }

    public void Dispose()
    {
        _stopping = true;
        _thread.Join(TimeSpan.FromSeconds(1));
    }

    private static bool Connects()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))) return false;
        if (!NativeLibrary.TryLoad(Library, out var library)) return false;
        NativeLibrary.Free(library);
        var display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero) return false;
        XCloseDisplay(display);
        return true;
    }

    private static int OnError(IntPtr display, IntPtr error)
    {
        _lastError = Marshal.ReadByte(error, ErrorCodeOffset);
        return 0;
    }

    private void Fail(int id, GrabFailure why)
    {
        lock (_failed) _failed[id] = why;
    }

    private void Listen(IReadOnlyList<KeyGrab> keys, ManualResetEventSlim ready)
    {
        var display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
        {
            foreach (var key in keys) Fail(key.Id, GrabFailure.Refused);
            ready.Set();
            return;
        }

        var root = XDefaultRootWindow(display);
        var wanted = new Dictionary<(int Code, uint Modifiers), int>();
        var taken = new List<(int Code, uint Modifiers)>();
        foreach (var key in keys)
        {
            int code = XKeysymToKeycode(display, key.KeySym);
            if (code == 0)
            {
                Fail(key.Id, GrabFailure.Refused);
                continue;
            }

            var why = Grab(display, root, code, key.Modifiers, taken);
            if (why is { } failure) Fail(key.Id, failure);
            else wanted[(code, key.Modifiers)] = key.Id;
        }
        ready.Set();

        var buffer = Marshal.AllocHGlobal(EventSize);
        try
        {
            while (!_stopping)
            {
                while (XPending(display) > 0)
                {
                    XNextEvent(display, buffer);
                    if (Marshal.ReadInt32(buffer) != KeyPress) continue;
                    uint state = X11Keys.WithoutLocks((uint)Marshal.ReadInt32(buffer, StateOffset));
                    int code = Marshal.ReadInt32(buffer, KeycodeOffset);
                    if (wanted.TryGetValue((code, state), out int id)) _pressed(id);
                }
                Thread.Sleep(30);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            foreach (var (code, modifiers) in taken) XUngrabKey(display, code, modifiers, root);
            XCloseDisplay(display);
        }
    }

    /// <summary>Takes a key with each combination of the lock keys, or says why it could not.</summary>
    private static GrabFailure? Grab(IntPtr display, nuint root, int code, uint modifiers, List<(int Code, uint Modifiers)> taken)
    {
        var previous = XSetErrorHandler(Marshal.GetFunctionPointerForDelegate(Handler));
        try
        {
            foreach (uint locks in new[] { 0u, X11Keys.CapsLock, X11Keys.NumLock, X11Keys.CapsLock | X11Keys.NumLock })
            {
                _lastError = 0;
                XGrabKey(display, code, modifiers | locks, root, 0, GrabModeAsync, GrabModeAsync);
                XSync(display, 0);
                if (_lastError == 0)
                {
                    taken.Add((code, modifiers | locks));
                    continue;
                }

                foreach (var (heldCode, heldModifiers) in taken.Where(t => t.Code == code && (t.Modifiers & X11Keys.ShortcutBits) == modifiers).ToList())
                {
                    XUngrabKey(display, heldCode, heldModifiers, root);
                    taken.Remove((heldCode, heldModifiers));
                }
                XSync(display, 0);
                return _lastError == BadAccess ? GrabFailure.Taken : GrabFailure.Refused;
            }
            return null;
        }
        finally
        {
            XSetErrorHandler(previous);
        }
    }

    [DllImport(Library)]
    private static extern IntPtr XOpenDisplay(IntPtr name);

    [DllImport(Library)]
    private static extern void XCloseDisplay(IntPtr display);

    [DllImport(Library)]
    private static extern nuint XDefaultRootWindow(IntPtr display);

    [DllImport(Library)]
    private static extern byte XKeysymToKeycode(IntPtr display, nuint keysym);

    [DllImport(Library)]
    private static extern void XGrabKey(IntPtr display, int code, uint modifiers, nuint window, int ownerEvents, int pointerMode, int keyboardMode);

    [DllImport(Library)]
    private static extern void XUngrabKey(IntPtr display, int code, uint modifiers, nuint window);

    [DllImport(Library)]
    private static extern void XSync(IntPtr display, int discard);

    [DllImport(Library)]
    private static extern int XPending(IntPtr display);

    [DllImport(Library)]
    private static extern void XNextEvent(IntPtr display, IntPtr buffer);

    [DllImport(Library)]
    private static extern IntPtr XSetErrorHandler(IntPtr handler);
}
