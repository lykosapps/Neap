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

    public X11Hotkeys(MixService mix) : base(mix)
    {
    }

    /// <summary>Whether there is an X display to take keys from.</summary>
    public static bool Available => X11KeyGrabber.Available;

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
