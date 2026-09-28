using System.Runtime.InteropServices;

namespace Neap.Core;

/// <summary>Which program is behind a process, asked with the least access Windows has.</summary>
/// <remarks>
/// Only the right to ask where a process's program file is, which Windows
/// gives even for protected processes and which reads nothing of the
/// program's memory. Reading a program's description from the running
/// program needs the right to read its memory, which anti-cheat software in
/// a game watches for; the same description is in the file on disk.
/// </remarks>
public static class Programs
{
    private const uint QueryLimitedInformation = 0x1000;

    /// <summary>The full path of the program behind a process, or null when there is none or Windows won't say.</summary>
    public static string? PathOf(uint pid)
    {
        if (pid == 0) return null;
        IntPtr process = OpenProcess(QueryLimitedInformation, false, pid);
        if (process == IntPtr.Zero) return null;
        try
        {
            var path = new char[1024];
            uint length = (uint)path.Length;
            return QueryFullProcessImageNameW(process, 0, path, ref length) ? new string(path, 0, (int)length) : null;
        }
        finally { CloseHandle(process); }
    }

    /// <summary>The process name of the program behind a process, "witcher3" for witcher3.exe; empty when unknown.</summary>
    public static string NameOf(uint pid) => PathOf(pid) is { } path ? Path.GetFileNameWithoutExtension(path) : "";

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, [Out] char[] path, ref uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
