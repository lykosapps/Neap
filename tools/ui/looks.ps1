<#
.SYNOPSIS
Draws every page of the published app in pretend mode, in the dark, light and
high contrast themes, for a person to look over.

.DESCRIPTION
Each page is drawn by the app itself (--snapshot) into TestResults\ui as
looks-<theme>-<page>.png, with the window opened behind everything and
without taking the keyboard, so it can run while somebody uses the machine.
It needs no screen and no UI Automation, so it can run straight from a
Claude tool. It checks only that each picture is made; whether a page looks
right is for the person reading the pictures. Publish first, or run it
through run.ps1, which publishes when a source file is newer.

.PARAMETER Parts
Which themes to draw: D dark, L light, H high contrast. All by default.
#>
param([string]$Parts = 'DLH')

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = Join-Path $root 'src\Neap.Desktop\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\Neap.Desktop.exe'
$out = Join-Path $root 'TestResults\ui'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$fails = 0

$pages = 'home', 'audio', 'equaliser', 'controls', 'profiles', 'device', 'settings'
$themes = @(
    @{ Part = 'D'; Name = 'dark'; Flags = @('--dark') }
    @{ Part = 'L'; Name = 'light'; Flags = @('--light') }
    @{ Part = 'H'; Name = 'contrast'; Flags = @('--dark', '--highcontrast') }
)

foreach ($theme in $themes | Where-Object { $Parts.Contains($_.Part) }) {
    Write-Host "== the pages in $($theme.Name)"
    foreach ($page in $pages) {
        $file = Join-Path $out "looks-$($theme.Name)-$page.png"
        Remove-Item $file -ErrorAction SilentlyContinue
        $arguments = @('--pretend', '--behind', '--page', $page) + $theme.Flags + @('--wait', '15', '--snapshot', "`"$file`"")
        $proc = Start-Process $exe -ArgumentList $arguments -PassThru
        if (-not $proc.WaitForExit(90000)) { Stop-Process -Id $proc.Id -Force; Write-Host "  FAIL  $page did not finish its picture"; $fails++; continue }
        if (Test-Path $file) { Write-Host "  ok    $page" } else { Write-Host "  FAIL  $page made no picture"; $fails++ }
    }
}

Write-Host "$fails failures"
exit ([int]($fails -gt 0))
