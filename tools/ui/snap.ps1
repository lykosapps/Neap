<#
.SYNOPSIS
Has a published copy of the app draw one page in pretend mode into a picture,
behind other windows and without taking the keyboard.

.DESCRIPTION
For checking a page while building it. The app opens with --behind, draws
the page with --snapshot once it has settled, and exits; nothing comes to the
front, so it can run while somebody uses the machine. Each caller can keep
its own pretend settings with -Folder, so two can run at once.

.PARAMETER Exe
The published Neap.Desktop.exe to run. This checkout's own publish by default.

.PARAMETER Page
The page to draw: home, audio, equaliser, controls, profiles, device or settings.

.PARAMETER Theme
dark, light or contrast (high contrast over dark).

.PARAMETER Out
Where to save the picture.

.PARAMETER Folder
The pretend settings folder under %LOCALAPPDATA%\Neap, such as Pretend\mine.

.PARAMETER More
Further app flags, such as '--scrolled','end' or '--parametric'.
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\..\src\Neap.Desktop\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\Neap.Desktop.exe'),
    [Parameter(Mandatory)][string]$Page,
    [ValidateSet('dark', 'light', 'contrast')][string]$Theme = 'dark',
    [Parameter(Mandatory)][string]$Out,
    [string]$Folder = '',
    [string[]]$More = @()
)
if ($Folder) { $env:NEAP_PRETEND_FOLDER = $Folder }
$flags = switch ($Theme) { 'dark' { @('--dark') } 'light' { @('--light') } 'contrast' { @('--dark', '--highcontrast') } }
Remove-Item $Out -ErrorAction SilentlyContinue
$arguments = @('--pretend', '--behind', '--page', $Page) + $flags + $More + @('--wait', '15', '--snapshot', "`"$Out`"")
$proc = Start-Process $Exe -ArgumentList $arguments -PassThru
if (-not $proc.WaitForExit(90000)) { $proc.Kill(); Write-Host "FAIL  $Page did not finish its picture"; exit 1 }
if (Test-Path $Out) { Write-Host "ok    $Out"; exit 0 }
Write-Host "FAIL  $Page made no picture"
exit 1
