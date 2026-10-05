namespace Neap.Services;

/// <summary>Which way a shortcut moves the mix.</summary>
public enum MixKey { TowardGame, TowardChat, Balanced }

/// <summary>
/// One key combination. Modifiers are the RegisterHotKey flags; Key is a
/// virtual-key code.
/// </summary>
public sealed record Shortcut(uint Modifiers, uint Key)
{
    public const uint Alt = 0x0001, Control = 0x0002, Shift = 0x0004, Windows = 0x0008;

    /// <summary>Whether the shortcut has at least one modifier. One without is not offered.</summary>
    /// <remarks>
    /// These are global: binding a bare key takes it away from every other
    /// program on the machine, and the person who did it would have no idea
    /// why their game stopped responding to it.
    /// </remarks>
    public bool Sane => (Modifiers & (Alt | Control | Shift | Windows)) != 0;

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & Control) != 0) parts.Add(Strings.Get("Key_Ctrl"));
        if ((Modifiers & Alt) != 0) parts.Add(Strings.Get("Key_Alt"));
        if ((Modifiers & Shift) != 0) parts.Add(Strings.Get("Key_Shift"));
        if ((Modifiers & Windows) != 0) parts.Add(Strings.Get("Key_Win"));
        parts.Add(Name(Key));
        return string.Join(" + ", parts);
    }

    /// <summary>Readable names for the keys people actually pick.</summary>
    private static string Name(uint key) => key switch
    {
        0x21 => Strings.Get("Key_PageUp"),
        0x22 => Strings.Get("Key_PageDown"),
        0x24 => Strings.Get("Key_Home"),
        0x23 => Strings.Get("Key_End"),
        0x25 => Strings.Get("Key_Left"),
        0x26 => Strings.Get("Key_Up"),
        0x27 => Strings.Get("Key_Right"),
        0x28 => Strings.Get("Key_Down"),
        0x2D => Strings.Get("Key_Insert"),
        0x2E => Strings.Get("Key_Delete"),
        0x20 => Strings.Get("Key_Space"),
        0xBC => ",",
        0xBE => ".",
        0xBF => "/",
        0xDB => "[",
        0xDD => "]",
        >= 0x30 and <= 0x39 => ((char)key).ToString(),
        >= 0x41 and <= 0x5A => ((char)key).ToString(),
        >= 0x70 and <= 0x87 => $"F{key - 0x6F}",
        _ => Strings.Format("Key_Other", key),
    };
}

/// <summary>The shortcuts the mix starts with.</summary>
public static class MixShortcuts
{
    public static readonly IReadOnlyDictionary<MixKey, Shortcut> Defaults =
        new Dictionary<MixKey, Shortcut>
        {
            [MixKey.TowardGame] = new(Shortcut.Control | Shortcut.Alt, 0x22),   // Page Down
            [MixKey.TowardChat] = new(Shortcut.Control | Shortcut.Alt, 0x21),   // Page Up
            [MixKey.Balanced] = new(Shortcut.Control | Shortcut.Alt, 0x24),     // Home
        };
}
