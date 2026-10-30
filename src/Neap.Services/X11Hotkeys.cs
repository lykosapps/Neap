using System.Runtime.Versioning;
using Neap.Core.Input;

namespace Neap.Services;

/// <summary>
/// Global keyboard shortcuts that move the mix, taken from the X window system.
/// </summary>
/// <remarks>
/// A shortcut is kept as a Windows virtual-key code, so the same settings and
/// the same screen serve both systems; <see cref="X11Keys"/> says what each is
/// called to X. Where X has no name for a key, or the system will not give
/// it, that is said beside the key, as it is where another program holds it.
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed class X11Hotkeys : MixHotkeys
{
    private X11KeyGrabber? _grabber;

    static X11Hotkeys()
    {
        X11KeyGrabber.Trouble = line => AppLog.Write($"keys: {line}");
    }

    public X11Hotkeys(MixService mix) : base(mix)
    {
        if (X11Keys.OnlyX11Programs())
        {
            Limit = Strings.Get("Mix_KeysOnWayland");
            AppLog.Write("keyboard shortcuts: this is a Wayland session, so they are heard only while an X11 program is in front");
        }
    }

    /// <summary>Why there is no X display to take keys from, for the app log, or null when there is one.</summary>
    public static string? Unavailable => X11KeyGrabber.Unavailable;

    /// <remarks>Set on a Wayland desktop, where X is a compatibility layer that hears keys only for its own programs.</remarks>
    public override string? Limit { get; }

    protected override IReadOnlyDictionary<MixKey, string> Start(IReadOnlyDictionary<MixKey, Shortcut> wanted)
    {
        var refused = new Dictionary<MixKey, string>();
        var grabs = new List<KeyGrab>();
        foreach (var (which, shortcut) in wanted)
        {
            if (X11Keys.KeySym(shortcut.Key) is not uint keysym)
            {
                refused[which] = Strings.Get("Key_Refused");
                continue;
            }

            uint modifiers = X11Keys.Modifiers(
                control: (shortcut.Modifiers & Shortcut.Control) != 0,
                alt: (shortcut.Modifiers & Shortcut.Alt) != 0,
                shift: (shortcut.Modifiers & Shortcut.Shift) != 0,
                windows: (shortcut.Modifiers & Shortcut.Windows) != 0);
            grabs.Add(new KeyGrab((int)which, keysym, modifiers));
        }

        _grabber = new X11KeyGrabber(grabs, id => Pressed((MixKey)id));
        foreach (var (id, why) in _grabber.Failed)
            refused[(MixKey)id] = Strings.Get(why == GrabFailure.Taken ? "Key_Taken" : "Key_Refused");
        return refused;
    }

    protected override void LetGo()
    {
        _grabber?.Dispose();
        _grabber = null;
    }
}
