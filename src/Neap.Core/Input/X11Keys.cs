namespace Neap.Core.Input;

/// <summary>
/// What a key is called to the X window system, from what it is called to
/// Windows, which is how shortcuts are kept.
/// </summary>
/// <remarks>
/// A shortcut is stored as a Windows virtual-key code, so a person's settings
/// mean the same on either system and the screen that records one needs only
/// one table. X names a key by its keysym, and a modifier by a bit of the
/// state a key press carries.
/// </remarks>
public static class X11Keys
{
    /// <summary>The state bits a press carries for the modifiers a shortcut can use.</summary>
    public const uint Shift = 0x1, Control = 0x4, Alt = 0x8, Super = 0x40;

    /// <summary>The bits that mean a lock is on, which a shortcut ignores: NumLock is Mod2 and CapsLock is Lock.</summary>
    public const uint CapsLock = 0x2, NumLock = 0x10;

    /// <summary>Every modifier bit a shortcut can name; the rest of a press's state is not part of the shortcut.</summary>
    public const uint ShortcutBits = Shift | Control | Alt | Super;

    /// <summary>The state bits for a shortcut's modifiers.</summary>
    public static uint Modifiers(bool control, bool alt, bool shift, bool windows) =>
        (control ? Control : 0) | (alt ? Alt : 0) | (shift ? Shift : 0) | (windows ? Super : 0);

    /// <summary>The keysym for a Windows virtual-key code, or null for a key no shortcut can use.</summary>
    /// <remarks>The keys are the ones a shortcut is offered: letters, digits, function keys, the cursor and paging keys and a few punctuation marks.</remarks>
    public static uint? KeySym(uint virtualKey) => virtualKey switch
    {
        >= 0x41 and <= 0x5A => virtualKey + 0x20,
        >= 0x30 and <= 0x39 => virtualKey,
        >= 0x70 and <= 0x87 => 0xFFBE + (virtualKey - 0x70),
        0x21 => 0xFF55,
        0x22 => 0xFF56,
        0x23 => 0xFF57,
        0x24 => 0xFF50,
        0x25 => 0xFF51,
        0x26 => 0xFF52,
        0x27 => 0xFF53,
        0x28 => 0xFF54,
        0x2D => 0xFF63,
        0x2E => 0xFFFF,
        0x20 => 0x20,
        0xBC => 0x2C,
        0xBE => 0x2E,
        0xBF => 0x2F,
        0xDB => 0x5B,
        0xDD => 0x5D,
        _ => null,
    };

    /// <summary>The modifier bits of a press with the locks taken away, as a shortcut names them.</summary>
    public static uint WithoutLocks(uint state) => state & ShortcutBits;
}
