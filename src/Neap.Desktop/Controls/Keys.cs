using Avalonia.Input;

namespace Neap.Desktop.Controls;

/// <summary>What a key press means to the system's hotkeys, which name keys by virtual-key code.</summary>
internal static class Keys
{
    /// <summary>Whether the key is a modifier on its own.</summary>
    public static bool IsModifier(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System;

    /// <summary>The virtual-key code for the keys people pick for a shortcut, or null for any other.</summary>
    /// <remarks>
    /// The same keys <see cref="Shortcut"/> names: letters, digits, function
    /// keys, the cursor and paging keys, and the few punctuation keys.
    /// </remarks>
    public static uint? VirtualKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => 0x41u + (uint)(key - Key.A),
        >= Key.D0 and <= Key.D9 => 0x30u + (uint)(key - Key.D0),
        >= Key.F1 and <= Key.F24 => 0x70u + (uint)(key - Key.F1),
        Key.PageUp => 0x21,
        Key.PageDown => 0x22,
        Key.End => 0x23,
        Key.Home => 0x24,
        Key.Left => 0x25,
        Key.Up => 0x26,
        Key.Right => 0x27,
        Key.Down => 0x28,
        Key.Insert => 0x2D,
        Key.Delete => 0x2E,
        Key.Space => 0x20,
        Key.OemComma => 0xBC,
        Key.OemPeriod => 0xBE,
        Key.OemQuestion => 0xBF,
        Key.OemOpenBrackets => 0xDB,
        Key.OemCloseBrackets => 0xDD,
        _ => null,
    };
}
