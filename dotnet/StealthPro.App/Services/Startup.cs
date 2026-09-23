using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace StealthPro.App.Services;

/// <summary>
/// Whether the app starts with Windows.
///
/// <b>A shortcut in the Startup folder, not a Run-key entry.</b> The Run entry
/// was correct, enabled and pointing at the right file, and Windows never
/// once started it: twelve logins in the Shell-Core log since it was written,
/// each one reading the Run key, starting the other enabled entries around
/// it, and passing over ours without an event of any kind. No download mark
/// on the file, Smart App Control off, the drive mounted long before. The one
/// thing it had that the entries Windows did start lacked was the absence of a
/// code signature, and nothing documents that as a reason. Explorer processes
/// the Startup folder separately, so the app uses that instead, and every
/// launch is written to <see cref="AppLog"/> so the next login says for
/// certain whether it worked.
///
/// Still per-user, still no elevation, and still visible and switchable in
/// Task Manager's Startup tab like anything else.
///
/// The shortcut passes <c>--startup</c>, so a login puts the app in the
/// notification area rather than opening a window in front of whatever
/// somebody was about to do. That is the difference between a utility that
/// runs with Windows and one that greets you every morning.
/// </summary>
public static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "Stealth Pro II Control";
    private const string ShortcutName = "Stealth Pro II Control.lnk";
    public const string Flag = "--startup";

    private static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName);

    /// <summary>True when this launch came from starting with Windows.</summary>
    public static bool LaunchedAtLogin =>
        Environment.GetCommandLineArgs().Any(a =>
            string.Equals(a, Flag, StringComparison.OrdinalIgnoreCase));

    public static bool Enabled => File.Exists(ShortcutPath) || HasRunEntry();

    /// <summary>
    /// Keep the setting pointing at this copy of the app, in the form that
    /// works.
    ///
    /// Rewritten on every launch while it is on, so moving the app never
    /// leaves Windows launching something that is no longer there. And an old
    /// Run entry is converted here, so turning the setting on under the old
    /// build carries over without anybody having to find it again.
    /// </summary>
    public static void Refresh()
    {
        try
        {
            if (!Enabled) return;
            WriteShortcut();
            RemoveRunEntry();
        }
        catch { }
    }

    public static bool Set(bool enabled)
    {
        try
        {
            if (enabled)
            {
                WriteShortcut();
                RemoveRunEntry();
            }
            else
            {
                if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
                RemoveRunEntry();
            }
            return Enabled == enabled;
        }
        catch { return false; }
    }

    private static void WriteShortcut()
    {
        string exe = Environment.ProcessPath ?? "";
        if (exe.Length == 0) throw new InvalidOperationException("no process path");

        var link = (IShellLinkW)new ShellLink();
        link.SetPath(exe);
        link.SetArguments(Flag);
        link.SetWorkingDirectory(Path.GetDirectoryName(exe) ?? "");
        link.SetDescription("Starts Stealth Pro II Control in the notification area.");
        link.SetIconLocation(exe, 0);
        ((IPersistFile)link).Save(ShortcutPath, true);
        Marshal.FinalReleaseComObject(link);
    }

    private static bool HasRunEntry()
    {
        try
        {
            using var run = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
            return run?.GetValue(RunName) is string;
        }
        catch { return false; }
    }

    private static void RemoveRunEntry()
    {
        using var run = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        run?.DeleteValue(RunName, throwOnMissingValue: false);
    }

    // The shell's own shortcut object. Written against the interface directly
    // rather than through WScript.Shell, which some machines have switched off.

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int max, IntPtr data, uint flags);
        void GetIDList(out IntPtr list);
        void SetIDList(IntPtr list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int max);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int max);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int max);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short key);
        void SetHotkey(short key);
        void GetShowCmd(out int show);
        void SetShowCmd(int show);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int max, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
}
