<#
.SYNOPSIS
Runs tools\pretend.ps1, or another screen check, on a desktop of its own, so the
app coming to the front interrupts nobody.

.DESCRIPTION
Windows keeps what is in front per desktop. The check, and so the app it
drives, is started on a second desktop that the person never sees, so every
window it brings forward comes forward there. Its output goes to
TestResults\pretend-quiet.txt, which this script follows until the check says
it has finished.

If an app built from this folder, the practice app or a real one, ever has a
window on the person's own desktop, the run is stopped at once. Neap's own
copy, started from anywhere else, is left alone.

A check that sends keys cannot be run this way: keys go to the desktop the
person is working on, whatever desktop the check runs on.

.PARAMETER Run
The script and its arguments, relative to the repository. The pretend walk by
default; `tools\ui\run.ps1 hardware -Parts BFLT -Inside` runs parts of the
headset checks.

.PARAMETER TimeoutSeconds
How long to follow the run before giving up on it.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\pretend-quiet.ps1
#>
param(
    [string]$Run = 'tools\pretend.ps1 -Unattended',
    [int]$TimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

Add-Type -Namespace Neap -Name Desktops -MemberDefinition @'
[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
public static extern System.IntPtr CreateDesktop(string name, string device, System.IntPtr mode, int flags, uint access, System.IntPtr security);
[DllImport("user32.dll", SetLastError = true)]
public static extern bool CloseDesktop(System.IntPtr desktop);
public delegate bool WindowVisitor(System.IntPtr window, System.IntPtr state);
[DllImport("user32.dll")]
public static extern bool EnumWindows(WindowVisitor visitor, System.IntPtr state);
[DllImport("user32.dll")]
public static extern uint GetWindowThreadProcessId(System.IntPtr window, out uint process);
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct StartupInfo
{
    public int Size; public string Reserved; public string Desktop; public string Title;
    public int X, Y, Width, Height, XChars, YChars, Fill, Flags;
    public short ShowWindow, Reserved2;
    public System.IntPtr Reserved3, StdIn, StdOut, StdError;
}
[StructLayout(LayoutKind.Sequential)]
public struct ProcessInfo { public System.IntPtr Process, Thread; public int ProcessId, ThreadId; }
[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
public static extern bool CreateProcess(string application, string command, System.IntPtr processSecurity, System.IntPtr threadSecurity,
    bool inherit, uint flags, System.IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo process);
[DllImport("kernel32.dll")]
public static extern bool CloseHandle(System.IntPtr handle);
'@

# PowerShell turns $null into "" for a string, which these calls refuse.
$none = [NullString]::Value
$name = 'NeapQuiet'
# DESKTOP_ALL, so the check can create windows and read them there.
$desktop = [Neap.Desktops]::CreateDesktop($name, $none, [IntPtr]::Zero, 0, 0x000F01FF, [IntPtr]::Zero)
if ($desktop -eq [IntPtr]::Zero) { throw "could not create a desktop ($([Runtime.InteropServices.Marshal]::GetLastWin32Error()))" }

$log = Join-Path $root 'TestResults\pretend-quiet.txt'
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
Remove-Item $log -ErrorAction SilentlyContinue

# The copies of the app that this folder built, as opposed to the person's own copy.
function Get-BuiltApps {
    $built = Join-Path $root 'src'
    @(Get-CimInstance Win32_Process -Filter "Name='Neap.Desktop.exe'" |
        Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($built, [StringComparison]::OrdinalIgnoreCase) })
}

# Whether one of those has a window on the desktop this script runs on, which is the person's.
function Test-OnMyDesktop {
    $mine = @(Get-BuiltApps | ForEach-Object ProcessId)
    if ($mine.Count -eq 0) { return $false }
    $script:leaked = $false
    $visitor = [Neap.Desktops+WindowVisitor] {
        param($window, $state)
        $owner = [uint32]0
        [Neap.Desktops]::GetWindowThreadProcessId($window, [ref]$owner) | Out-Null
        if ($owner -in $mine) { $script:leaked = $true }
        $true
    }
    [Neap.Desktops]::EnumWindows($visitor, [IntPtr]::Zero) | Out-Null
    $script:leaked
}

function Stop-Run {
    $script = ($Run -split ' ')[0]
    Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'powershell' -and $_.CommandLine -match [regex]::Escape($script) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
    Get-BuiltApps | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
}

try {
    $startup = New-Object Neap.Desktops+StartupInfo
    $startup.Size = [Runtime.InteropServices.Marshal]::SizeOf($startup)
    $startup.Desktop = "WinSta0\$name"
    $process = New-Object Neap.Desktops+ProcessInfo
    $script, $arguments = $Run -split ' ', 2
    $command = 'cmd /c powershell -NoProfile -ExecutionPolicy Bypass -File "{0}" {1} > "{2}" 2>&1 & echo == finished >> "{2}"' -f (Join-Path $root $script), $arguments, $log
    # Inherited by the run, so a check that would send a key press refuses:
    # keys go to the person's desktop, not this one.
    $env:NEAP_QUIET_DESKTOP = '1'
    if (-not [Neap.Desktops]::CreateProcess($none, $command, [IntPtr]::Zero, [IntPtr]::Zero, $false, 0, [IntPtr]::Zero, $root, [ref]$startup, [ref]$process)) {
        throw "could not start the run ($([Runtime.InteropServices.Marshal]::GetLastWin32Error()))"
    }
    [Neap.Desktops]::CloseHandle($process.Process) | Out-Null
    [Neap.Desktops]::CloseHandle($process.Thread) | Out-Null

    $shown = 0; $done = $false; $text = ''
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt $TimeoutSeconds -and -not $done) {
        Start-Sleep -Milliseconds 300
        if (Test-OnMyDesktop) {
            Stop-Run
            Write-Host 'the app opened a window on your own desktop, so the run was stopped'
            exit 3
        }
        if (-not (Test-Path $log)) { continue }
        $stream = [IO.File]::Open($log, 'Open', 'Read', 'ReadWrite')
        $text = (New-Object IO.StreamReader($stream)).ReadToEnd().TrimStart([char]0xFEFF)
        $stream.Dispose()
        if ($text.Length -gt $shown) { Write-Host -NoNewline $text.Substring($shown); $shown = $text.Length }
        $done = $text -match '== finished\s*$'
    }
    if (-not $done) {
        Write-Host "the run had not finished after $TimeoutSeconds s; stopping it"
        Stop-Run
        exit 2
    }
    exit $(if ($text -match '(?m)^\s*FAIL ' -or $text -match 'CategoryInfo' -or $text -match '(?m)^== finished, exit [1-9]') { 1 } else { 0 })
}
finally { [Neap.Desktops]::CloseDesktop($desktop) | Out-Null }
