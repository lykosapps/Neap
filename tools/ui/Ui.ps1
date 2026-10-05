<#
.SYNOPSIS
Shared helpers for the screen checks in this folder. Dot-source it:

    . "$PSScriptRoot\Ui.ps1"

.DESCRIPTION
Each check launches the published app with --pretend, so nothing reaches a
real headset or Windows' audio, and drives it through UI Automation. Injected
clicks do not reach WinUI content, so every control is operated through its
UI Automation pattern.

Three things here exist because they cost time before:

  - One launch serves every window size. The app is started once per theme and
    the window is resized in place, with the layout waited for by polling.
  - Waits poll every 60 ms rather than sleeping a fixed time.
  - A picture is only kept if the screen was drawn. A black frame is retaken,
    and said so if it stays black.

Run a check through run.ps1, not directly: it publishes if the sources are
newer than the build, and runs the check from outside the Claude tools'
package, where the screen is drawn.
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices; using System.Text;
public static class NeapUi {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr w, out Rect r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr w, IntPtr dc, uint f);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr w, int x, int y, int cx, int cy, bool repaint);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr w);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);
}
'@
[NeapUi]::SetProcessDPIAware() | Out-Null

$A = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]
$script:Fails = 0
$script:Exe = Join-Path $PSScriptRoot '..\..\src\Neap.App\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\Neap.exe'
$script:Out = Join-Path $PSScriptRoot '..\..\TestResults\ui'
$script:Proc = $null; $script:Window = $null; $script:Handle = [IntPtr]::Zero

# Whether this process runs inside an app package, as the Claude tools do. There
# the screen is not drawn for a picture, and the app's folder is a copy.
function Test-InPackage {
    $length = 0
    [NeapUi]::GetCurrentPackageFullName([ref]$length, $null) -ne 15700
}

function Check([bool]$ok, [string]$what) {
    if ($ok) { Write-Host "  ok    $what" } else { Write-Host "  FAIL  $what" -ForegroundColor Red; $script:Fails++ }
}
function Note([string]$what) { Write-Host "  note  $what" }
function Section([string]$what) { Write-Host "== $what" }

function Wait-Until([scriptblock]$Condition, [double]$Seconds = 6) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    do {
        if (& $Condition) { return $true }
        Start-Sleep -Milliseconds 60
    } while ($clock.Elapsed.TotalSeconds -lt $Seconds)
    [bool](& $Condition)
}

# True once a reading has come out the same three times running, so a layout
# that is still settling after a resize is not measured halfway.
function Wait-Stable([scriptblock]$Read, [double]$Seconds = 3) {
    $last = $null; $same = 0
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt $Seconds) {
        $now = & $Read
        if ($now -eq $last) { $same++ } else { $same = 0; $last = $now }
        if ($same -ge 3) { return $true }
        Start-Sleep -Milliseconds 80
    }
    $false
}

function New-Condition([string]$Property, $Value) {
    New-Object System.Windows.Automation.PropertyCondition($A::$Property, $Value)
}
function Find-Named([string]$Name) { $script:Window.FindFirst($Scope::Descendants, (New-Condition NameProperty $Name)) }
function Find-Id([string]$Id) { $script:Window.FindFirst($Scope::Descendants, (New-Condition AutomationIdProperty $Id)) }
function Test-Present($Element) { $null -ne $Element -and -not $Element.Current.IsOffscreen }
function Get-Texts {
    @($script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Text)))) |
        ForEach-Object { $_.Current.Name }
}
function Get-Popups {
    # A menu or flyout is a window of its own, so it is searched for in all of them.
    foreach ($window in $A::RootElement.FindAll($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id)))) {
        $window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::MenuItem)))
    }
}
function Press($Element) { $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Open-Menu($Element) {
    try { $Element.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() } catch { Press $Element }
}
function Close-Menu($Element) {
    try { $Element.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse() } catch { Write-Verbose 'no menu to close' }
}

# Starts the app against the pretend headset, behind every other window so it
# takes nothing from whoever is at the machine.
function Start-NeapPretend {
    param([string]$Theme = 'dark', [string]$Page = 'home', [switch]$KeepSettings, [string[]]$Flags = @('--update'))
    $already = Get-CimInstance Win32_Process -Filter "Name='Neap.exe'" | Where-Object { $_.CommandLine -match '--pretend' }
    if ($already) { throw 'a practice run of Neap is already open; only one can run at a time' }
    if (-not $KeepSettings) { Remove-Item (Join-Path $env:LOCALAPPDATA 'Neap\Pretend') -Recurse -Force -ErrorAction SilentlyContinue }
    $arguments = @('--pretend', '--behind', '--page', $Page, "--$Theme") + $Flags
    $script:Proc = Start-Process $script:Exe -ArgumentList $arguments -PassThru
    $script:Window = $null
    Wait-Until {
        $script:Window = $A::RootElement.FindFirst($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id)))
        $null -ne $script:Window
    } 20 | Out-Null
    $script:Handle = [IntPtr]$script:Window.Current.NativeWindowHandle
}
function Stop-NeapPretend {
    if ($script:Proc) {
        Stop-Process -Id $script:Proc.Id -Force -ErrorAction SilentlyContinue
        $script:Proc.WaitForExit(4000) | Out-Null
    }
}

# Resizes the window, in physical pixels, and waits for the layout to settle by
# watching where the named element ends up.
function Set-NeapSize([int]$Width, [int]$Height, [string]$Anchor) {
    $rect = New-Object NeapUi+Rect
    [NeapUi]::GetWindowRect($script:Handle, [ref]$rect) | Out-Null
    [NeapUi]::MoveWindow($script:Handle, $rect.Left, $rect.Top, $Width, $Height, $true) | Out-Null
    Wait-Stable {
        $e = Find-Named $Anchor
        if ($e) { $b = $e.Current.BoundingRectangle; "$($b.X),$($b.Y),$($b.Width),$($b.Height)" }
    } | Out-Null
}
function Get-NeapRect { $rect = New-Object NeapUi+Rect; [NeapUi]::GetWindowRect($script:Handle, [ref]$rect) | Out-Null; $rect }

# Saves the top of the window, and the whole of it if asked. Returns the bitmap
# for measuring; the caller disposes it. A black frame is retaken, and said so
# if it stays black.
function Save-Shot([string]$Name, [int]$Top = 300, [switch]$Whole) {
    New-Item -ItemType Directory -Force -Path $script:Out | Out-Null
    for ($try = 0; $try -lt 5; $try++) {
        $rect = Get-NeapRect
        $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $dc = $graphics.GetHdc()
        [NeapUi]::PrintWindow($script:Handle, $dc, 2) | Out-Null
        $graphics.ReleaseHdc($dc); $graphics.Dispose()
        $lit = 0
        foreach ($x in 100, 300, 500) { foreach ($y in 150, 300, 450) { if ($x -lt $bitmap.Width -and $y -lt $bitmap.Height -and $bitmap.GetPixel($x, $y).GetBrightness() -gt 0.04) { $lit++ } } }
        if ($lit -gt 2) { break }
        if ($try -lt 4) { $bitmap.Dispose(); Start-Sleep -Milliseconds 300 }
    }
    if ($lit -le 2) { Note "the screen is black for ${Name}: the picture is not trustworthy" }
    $crop = $bitmap.Clone((New-Object System.Drawing.Rectangle(0, 0, $bitmap.Width, [Math]::Min($Top, $bitmap.Height))), $bitmap.PixelFormat)
    $crop.Save((Join-Path $script:Out "$Name-top.png")); $crop.Dispose()
    if ($Whole) { $bitmap.Save((Join-Path $script:Out "$Name.png")) }
    $bitmap
}

function Get-Luminance($Colour) {
    $channels = @($Colour.R, $Colour.G, $Colour.B) | ForEach-Object {
        $v = $_ / 255.0
        if ($v -le 0.03928) { $v / 12.92 } else { [Math]::Pow(($v + 0.055) / 1.055, 2.4) }
    }
    0.2126 * $channels[0] + 0.7152 * $channels[1] + 0.0722 * $channels[2]
}
function Get-ContrastRatio($A1, $B1) {
    $high = [Math]::Max((Get-Luminance $A1), (Get-Luminance $B1)); $low = [Math]::Min((Get-Luminance $A1), (Get-Luminance $B1))
    [Math]::Round(($high + 0.05) / ($low + 0.05), 2)
}

# The commonest colour in a box is its ground; the colour furthest from it in
# brightness is its text. Worked out once for each distinct colour.
function Get-BoxColours($Bitmap, $Box, $Origin) {
    $x0 = [int]($Box.X - $Origin.Left); $y0 = [int]($Box.Y - $Origin.Top)
    $counts = @{}
    for ($y = [Math]::Max($y0, 0); $y -lt [Math]::Min($y0 + [int]$Box.Height, $Bitmap.Height); $y++) {
        for ($x = [Math]::Max($x0, 0); $x -lt [Math]::Min($x0 + [int]$Box.Width, $Bitmap.Width); $x++) {
            $key = $Bitmap.GetPixel($x, $y).ToArgb(); $counts[$key] = 1 + [int]$counts[$key]
        }
    }
    $groundKey = 0; $most = -1
    foreach ($entry in $counts.GetEnumerator()) { if ($entry.Value -gt $most) { $most = $entry.Value; $groundKey = $entry.Key } }
    $ground = [System.Drawing.Color]::FromArgb($groundKey); $groundLuminance = Get-Luminance $ground
    $textKey = $groundKey; $far = -1
    foreach ($key in $counts.Keys) {
        $d = [Math]::Abs((Get-Luminance ([System.Drawing.Color]::FromArgb($key))) - $groundLuminance)
        if ($d -gt $far) { $far = $d; $textKey = $key }
    }
    @{ Ground = $ground; Text = [System.Drawing.Color]::FromArgb($textKey) }
}
function Test-Contrast($Bitmap, $Element, [string]$What, [double]$Needs = 4.5) {
    $colours = Get-BoxColours $Bitmap $Element.Current.BoundingRectangle (Get-NeapRect)
    $ratio = Get-ContrastRatio $colours.Text $colours.Ground
    Check ($ratio -ge $Needs) ("{0}: contrast {1}:1 (needs {2}:1)" -f $What, $ratio, $Needs)
}
