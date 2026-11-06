<#
.SYNOPSIS
Has a published copy of the app draw one page in pretend mode into pictures,
one for each theme, behind other windows and without taking the keyboard.

.DESCRIPTION
For checking a page while building it. The app opens with --behind, draws
the page with --snapshot once it has settled, and exits; nothing comes to the
front, so it can run while somebody uses the machine. The themes are drawn
at the same time, each with pretend settings of its own, so the whole takes
about ten seconds.

.PARAMETER Exe
The published Neap.Desktop.exe to run. This checkout's own publish by default.

.PARAMETER Page
The page to draw: home, audio, equaliser, controls, profiles, device or settings.

.PARAMETER Theme
Which of dark, light and contrast (high contrast over dark) to draw, separated
by commas. All three by default.

.PARAMETER Out
The folder for the pictures, named <page>-<theme>.png. TestResults\ui\snap by default.

.PARAMETER Wait
Seconds to let the page settle before the picture. Eight covers the
equaliser reading its presets, the slowest page to settle.

.PARAMETER Folder
The start of the pretend settings folders in %LOCALAPPDATA%\Neap, a plain
name such as PretendMine; each theme adds its own name to it.

.PARAMETER More
Further app flags, such as '--scrolled','end' or '--parametric'.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\ui\snap.ps1 -Page equaliser

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\ui\snap.ps1 -Page home -Theme light
#>
param(
    [string]$Exe = '',
    [Parameter(Mandatory)][string]$Page,
    [string]$Theme = 'dark,light,contrast',
    [string]$Out = '',
    [int]$Wait = 8,
    [string]$Folder = '',
    [string[]]$More = @()
)
if (-not $Exe) { $Exe = Join-Path $PSScriptRoot '..\..\src\Neap.Desktop\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\Neap.Desktop.exe' }
if (-not $Out) { $Out = Join-Path $PSScriptRoot '..\..\TestResults\ui\snap' }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Out = (Resolve-Path $Out).Path
if ($Folder) { $env:NEAP_PRETEND_FOLDER = $Folder }

# All themes at once, each in a pretend folder of its own, so the settle time is paid once.
$runs = foreach ($name in $Theme -split ',' | ForEach-Object { $_.Trim() }) {
    $flags = switch ($name) {
        'dark' { @('--dark') }
        'light' { @('--light') }
        'contrast' { @('--dark', '--highcontrast') }
        default { throw "no theme named $name; the themes are dark, light and contrast" }
    }
    $file = Join-Path $Out "$Page-$name.png"
    Remove-Item $file -ErrorAction SilentlyContinue
    $env:NEAP_PRETEND_FOLDER = $(if ($Folder) { "$Folder-$name" } else { "PretendSnap-$name" })
    $arguments = @('--pretend', '--behind', '--page', $Page) + $flags + $More + @('--wait', $Wait, '--snapshot', "`"$file`"")
    [pscustomobject]@{ Name = $name; File = $file; Process = Start-Process $Exe -ArgumentList $arguments -PassThru }
}

$failed = 0
foreach ($run in $runs) {
    if (-not $run.Process.WaitForExit(90000)) { $run.Process.Kill(); Write-Host "FAIL  $Page in $($run.Name) did not finish its picture"; $failed++ }
    elseif (Test-Path $run.File) { Write-Host "ok    $($run.File)" }
    else { Write-Host "FAIL  $Page in $($run.Name) made no picture"; $failed++ }
}
exit $(if ($failed) { 1 } else { 0 })
