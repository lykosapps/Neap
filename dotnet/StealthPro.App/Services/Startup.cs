using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace StealthPro.App.Services;

/// <summary>
/// Whether the app starts with Windows, as a shortcut in the user's Startup
/// folder.
/// </summary>
/// <remarks>
/// <para>
/// A shortcut in the Startup folder, not a Run-key entry. A Run entry that
/// was correct, enabled and pointing at the right file was never started:
/// across twelve logins the Shell-Core log shows Windows reading the Run key,
/// starting the other enabled entries around it, and passing over this one
/// without an event of any kind. There was no download mark on the file,
/// Smart App Control was off, and the drive was mounted long before. The one
/// difference from the entries Windows did start was the missing code
/// signature, and nothing documents that as a reason. Explorer processes the
/// Startup folder separately, so the app uses that, and every launch is
/// written to <see cref="AppLog"/> so a login says for certain whether it
/// worked.
/// </para>
/// <para>
/// It is per-user, needs no elevation, and is visible and switchable in Task
/// Manager's Startup tab like anything else.
/// </para>
/// <para>
/// The shortcut passes <c>--startup</c>, so a login puts the app in the
/// notification area rather than opening a window in front of whatever
/// somebody was about to do.
/// </para>
/// </remarks>
public static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string Flag = "--startup";

    /// <summary>The Run-key entry an early build wrote, converted to a shortcut on sight.</summary>
    private const string RunName = "Stealth Pro II Control";

    private static string StartupFolder => Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    private static string ShortcutPath => Path.Combine(StartupFolder, AppInfo.Name + ".lnk");

    /// <summary>The shortcut under the app's earlier name, replaced on sight.</summary>
    private static string EarlierShortcutPath => Path.Combine(StartupFolder, "Stealth Pro II Control.lnk");

    /// <summary>True when this launch came from starting with Windows.</summary>
    public static bool LaunchedAtLogin =>
        Environment.GetCommandLineArgs().Any(a =>
            string.Equals(a, Flag, StringComparison.OrdinalIgnoreCase));

    public static bool Enabled =>
        File.Exists(ShortcutPath) || File.Exists(EarlierShortcutPath) || HasRunEntry();

    /// <summary>
    /// Keep the setting pointing at this copy of the app, in the form that
    /// works.
    /// </summary>
    /// <remarks>
    /// Rewritten on every launch while it is on, so moving the app never
    /// leaves Windows launching something that is no longer there. A Run-key
    /// entry left from an older build is converted to a shortcut here, so the
    /// setting carries over without anybody having to turn it on again.
    /// </remarks>
    public static void Refresh()
    {
        try
        {
            if (!Enabled) return;
            WriteShortcut();
            RemoveEarlier();
        }
        catch (Exception ex)
        {
            // Not worth interrupting a launch for, but it would leave Windows
            // starting a copy that may no longer be there.
            AppLog.Write($"startup: could not refresh the shortcut: {ex.Message}");
        }
    }

    public static bool Set(bool enabled)
    {
        try
        {
            if (enabled)
            {
                WriteShortcut();
            }
            else
            {
                if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
            }
            RemoveEarlier();
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
        link.SetDescription(Strings.Format("Startup_Description", AppInfo.Name));
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

    /// <summary>Removes what an earlier build left behind to start it.</summary>
    private static void RemoveEarlier()
    {
        if (File.Exists(EarlierShortcutPath)) File.Delete(EarlierShortcutPath);
        RemoveRunEntry();
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
