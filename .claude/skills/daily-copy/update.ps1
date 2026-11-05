<#
.SYNOPSIS
Publishes this checkout into the owner's try-it folder, restarts the copy there,
and says whether it came up with the headset connected.

.DESCRIPTION
Refuses when anything is uncommitted, or when this checkout is behind main, so
the try-it copy only ever moves forward to committed work. Stops only a copy
running from the try-it folder, copies the Release publish over it, and starts
it again with --startup, into the notification area as at sign-in, through
Win32_Process.Create so it runs
outside the desktop app's package and uses the real app folder. The real log is
read the same way, from outside the package, and the copy counts as healthy
when a "headset: Connected" line follows the restart.

Exits 0 when healthy, 2 when the copy started but the headset reads as off or
quiet, and 1 on any failure.

.PARAMETER Target
The try-it folder. CLAUDE.local.md names it.

.PARAMETER WaitSeconds
How long to wait for the headset to connect after the restart.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File .claude\skills\daily-copy\update.ps1 -Target D:\neap\build
#>
param(
    [Parameter(Mandatory)][string]$Target,
    [int]$WaitSeconds = 60
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$exeName = 'Neap.Desktop.exe'
$Target = (Resolve-Path $Target).Path

if (@(git -C $root status --porcelain).Count -gt 0) { Write-Host 'FAIL  this checkout has uncommitted changes; commit first'; exit 1 }
git -C $root merge-base --is-ancestor main HEAD
if ($LASTEXITCODE -ne 0) { Write-Host 'FAIL  this checkout is behind main; the try-it copy would go backwards'; exit 1 }
$commit = git -C $root log -1 --format='%h %s'

dotnet publish (Join-Path $root 'src\Neap.Desktop') -c Release -r win-x64 -f net10.0-windows10.0.19041.0 --self-contained --nologo -v quiet
if ($LASTEXITCODE -ne 0) { Write-Host 'FAIL  publish failed'; exit 1 }
$publish = Join-Path $root 'src\Neap.Desktop\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish'

# Only the copy in the try-it folder: other sessions' copies and practice runs are left alone.
$running = @(Get-CimInstance Win32_Process -Filter "Name='$exeName'" |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($Target, [StringComparison]::OrdinalIgnoreCase) })
# Started as at sign-in, into the notification area, so no window takes the screen.
$command = "`"$(Join-Path $Target $exeName)`" --startup"
foreach ($p in $running) { Stop-Process -Id $p.ProcessId -Force }
foreach ($p in $running) { Wait-Process -Id $p.ProcessId -Timeout 15 -ErrorAction SilentlyContinue }

robocopy $publish $Target /E /NFL /NDL /NJH /NJS /NP /R:3 /W:1 | Out-Null
if ($LASTEXITCODE -ge 8) { Write-Host "FAIL  copying into $Target failed (robocopy $LASTEXITCODE)"; exit 1 }

$restarted = Get-Date
$started = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $command; CurrentDirectory = $Target }
if ($started.ReturnValue -ne 0) { Write-Host "FAIL  could not start the copy ($($started.ReturnValue))"; exit 1 }
Write-Host "ok    published $commit and restarted: $command"

# The log is read by a process started outside the package, which writes what
# it finds to a folder the package sees as it is.
$out = Join-Path $root 'TestResults\daily-copy'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$found = Join-Path $out 'headset-lines.txt'
$last = $null
$deadline = $restarted.AddSeconds($WaitSeconds)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 5
    Remove-Item $found -ErrorAction SilentlyContinue
    $read = "powershell -NoProfile -Command `"Select-String -Path `$env:LOCALAPPDATA\Neap\app.log -Pattern '  headset: ' | Select-Object -Last 20 | ForEach-Object Line | Set-Content -Encoding utf8 '$found'`""
    Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $read } | Out-Null
    for ($i = 0; $i -lt 10 -and -not (Test-Path $found); $i++) { Start-Sleep -Milliseconds 500 }
    if (-not (Test-Path $found)) { continue }
    $since = @(Get-Content $found | Where-Object {
            $at = [DateTime]::MinValue
            [DateTime]::TryParseExact($_.Substring(0, [Math]::Min(21, $_.Length)), 'yyyy-MM-dd HH:mm:ss.f', $null, 'None', [ref]$at) -and $at -ge $restarted.AddSeconds(-1)
        })
    if ($since) { $last = $since[-1] }
    if ($since | Where-Object { $_ -match '  headset: Connected' }) { Write-Host "ok    $($since | Where-Object { $_ -match '  headset: Connected' } | Select-Object -Last 1)"; exit 0 }
}
if ($last) { Write-Host "note  the copy started, but the headset is not connected: $last"; exit 2 }
Write-Host "FAIL  no headset line in the real log within $WaitSeconds seconds of the restart"
exit 1
