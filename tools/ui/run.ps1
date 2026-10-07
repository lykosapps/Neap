<#
.SYNOPSIS
Runs one of the screen checks in this folder against the published app in
pretend mode.

.DESCRIPTION
Publishes first if any source file is newer than the build. If it is run from
inside an app package, as the Claude tools are, it starts itself again outside
it, where the screen is drawn and pictures are not black, and follows the
log as it grows. Logs and pictures go in TestResults\ui.

Only one practice run of the app can be open at a time, so run one check at a
time.

.PARAMETER Check
The check to run: the name of a script in this folder, such as banner, or all
for every one of them.

.PARAMETER Parts
Which parts of the check to run, if it has parts. All by default.

.PARAMETER TimeoutSeconds
How long to follow the check before giving up on it.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\ui\run.ps1 all

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\ui\run.ps1 banner

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\ui\run.ps1 banner -Parts AB
#>
param(
    [Parameter(Mandatory, Position = 0)][string]$Check,
    [string]$Parts = '',
    [int]$TimeoutSeconds = 600,
    [switch]$Inside,
    [switch]$NoPublish
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# Every check, one after another. Only one practice run of the app can be open
# at a time, so they never overlap.
if ($Check -eq 'all') {
    $worst = 0
    foreach ($name in 'banner', 'settings', 'profiles') {
        Write-Host "### $name"
        & $PSCommandPath $name -TimeoutSeconds $TimeoutSeconds
        $worst = [Math]::Max($worst, $LASTEXITCODE)
    }
    exit $worst
}

$script = Join-Path $PSScriptRoot "$Check.ps1"
if (-not (Test-Path $script)) { throw "there is no check named $Check in tools\ui" }

Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices; using System.Text;
public static class NeapRun {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);
}
'@
function Test-InPackage { $length = 0; [NeapRun]::GetCurrentPackageFullName([ref]$length, $null) -ne 15700 }

# Publishes when a source file is newer than the build, so a check is never run
# against what was built before the last change.
function Publish-IfStale {
    $dll = Join-Path $root 'src\Neap.Desktop\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\Neap.Desktop.dll'
    $newest = Get-ChildItem (Join-Path $root 'src') -Recurse -File -Include *.cs, *.axaml, *.resw, *.csproj |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $props = Get-Item (Join-Path $root 'Directory.Build.props')
    $changed = [Math]::Max($newest.LastWriteTime.Ticks, $props.LastWriteTime.Ticks)
    if ((Test-Path $dll) -and (Get-Item $dll).LastWriteTime.Ticks -ge $changed) {
        Write-Host 'the build is up to date'
        return
    }
    Write-Host 'publishing, since a source file is newer than the build'
    dotnet publish (Join-Path $root 'src\Neap.Desktop') -c Release -r win-x64 -f net10.0-windows10.0.19041.0 --self-contained --nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw 'the publish failed' }
}

if (-not $Inside) {
    if (-not $NoPublish) { Publish-IfStale }
    if (Test-InPackage) {
        $logs = Join-Path $root 'TestResults\ui'
        New-Item -ItemType Directory -Force -Path $logs | Out-Null
        $log = Join-Path $logs "$Check.txt"
        Remove-Item $log -ErrorAction SilentlyContinue
        $extra = if ($Parts) { " -Parts $Parts" } else { '' }
        $command = 'cmd /c powershell -NoProfile -ExecutionPolicy Bypass -File "{0}" {1}{2} -Inside > "{3}" 2>&1' -f $PSCommandPath, $Check, $extra, $log
        $started = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $command }
        if ($started.ReturnValue -ne 0) { throw "could not start the check outside the package ($($started.ReturnValue))" }

        # Follow the log until the check says it has finished.
        $shown = 0; $code = $null
        $clock = [Diagnostics.Stopwatch]::StartNew()
        while ($clock.Elapsed.TotalSeconds -lt $TimeoutSeconds -and $null -eq $code) {
            Start-Sleep -Milliseconds 400
            if (-not (Test-Path $log)) { continue }
            $stream = [IO.File]::Open($log, 'Open', 'Read', 'ReadWrite')
            $text = (New-Object IO.StreamReader($stream)).ReadToEnd().TrimStart([char]0xFEFF)
            $stream.Dispose()
            if ($text.Length -gt $shown) { Write-Host -NoNewline $text.Substring($shown); $shown = $text.Length }
            if ($text -match '== finished, exit (\d+)') { $code = [int]$Matches[1] }
        }
        if ($null -eq $code) {
            Write-Host "the check had not finished after $TimeoutSeconds s; stopping it"
            Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'ui\\run.ps1.* -Inside' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
            Get-CimInstance Win32_Process -Filter "Name='Neap.exe'" | Where-Object { $_.CommandLine -match '--pretend' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
            exit 2
        }
        Write-Host "(log: $log)"
        exit $code
    }
}

if ($Parts) { & $script -Parts $Parts } else { & $script }
$code = $LASTEXITCODE
Write-Host "== finished, exit $code"
exit $code
