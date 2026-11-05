<#
.SYNOPSIS
Checks the mix overlay on the published app in pretend mode: turning the
headset's chat wheel shows the mix at the foot of the main screen, centred,
clear of the system's volume flyout and no bigger than it, without taking
the keyboard, with clicks passing through it, and gone again soon after the
wheel stops. The settings flyout on Home's mix card holds its switch and the
keyboard shortcuts'; switched off there, the wheel shows nothing.

.DESCRIPTION
Run through run.ps1. The wheel is turned over the practice run's pipe, as the
headset reports it. On a desktop of its own no window is in front, so Neap
is not, as when someone is playing. That it never takes the keyboard is read
from how the window is marked, as a desktop of its own has no keyboard.
Whether it stays away from an exclusive full-screen game is not checked
here: that is the system's own report, and the rule that follows it is a
test in Core.
#>

. "$PSScriptRoot\Ui.ps1"

Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class NeapOverlay {
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] public static extern int GetWindowLong(IntPtr w, int index);
}
'@
Add-Type -AssemblyName System.Windows.Forms

$band = 88
$styles = @{ Transparent = 0x20; ToolWindow = 0x80; Layered = 0x80000; NoActivate = 0x08000000 }

# The practice run's overlay, if it is showing.
function Find-Overlay {
    $A::RootElement.FindAll($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id))) |
        Where-Object { $_.Current.Name -eq 'Game and chat mix' -and -not $_.Current.IsOffscreen } | Select-Object -First 1
}
# Turns the wheel toward chat: the first count is where it starts, the rest move the mix.
function Turn-Wheel {
    foreach ($count in 50, 55, 60, 65, 70) {
        Send-Pretend "report game_chat_mix $count" | Out-Null
        Start-Sleep -Milliseconds 150
    }
}
# Starts a practice run with a chat app chosen, so the mix is running, and the overlay switched on or off.
function Start-WithMix([string]$Theme, [bool]$Overlay) {
    $folder = Join-Path $env:LOCALAPPDATA 'Neap\Pretend'
    Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $folder | Out-Null
    $on = if ($Overlay) { 'true' } else { 'false' }
    "{`"chat_apps`":[`"Discord`"],`"mix_overlay`":$on}" | Set-Content (Join-Path $folder 'app-settings.json') -Encoding ascii
    Start-NeapPretend -Theme $Theme -KeepSettings -Flags @()
    Wait-Until { $null -ne (Send-Pretend 'mix') } 10 | Out-Null
}

function Show-LogTail {
    $log = Join-Path $env:LOCALAPPDATA 'Neap\Pretend\app.log'
    if (Test-Path $log) { Get-Content $log -Tail 12 | ForEach-Object { Note "log: $_" } }
}

foreach ($theme in 'dark', 'light') {
    Section "the mix overlay, $theme"
    Start-WithMix $theme $true
    try {
        Turn-Wheel
        Check (Wait-Until { $null -ne (Find-Overlay) } 4) 'turning the wheel shows the mix'
        $overlay = Find-Overlay
        if (-not $overlay) {
            Show-LogTail
            $A::RootElement.FindAll($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id))) |
                ForEach-Object { Note "window: '$($_.Current.Name)' offscreen=$($_.Current.IsOffscreen) at $($_.Current.BoundingRectangle)" }
            continue
        }
        $handle = [IntPtr]$overlay.Current.NativeWindowHandle

        $work = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
        $box = $overlay.Current.BoundingRectangle
        $scale = [NeapUi]::GetDpiForWindow($handle) / 96
        Check ($box.Width -le 180 * $scale -and $box.Height -le 40 * $scale) "its window is no bigger than the system's own volume flyout: $([Math]::Round($box.Width / $scale)) by $([Math]::Round($box.Height / $scale))"
        Check ([Math]::Abs(($box.Left + $box.Width / 2) - ($work.Left + $work.Width / 2)) -le 2) "it is centred across the screen ($($box.Left),$($box.Top) $($box.Width)x$($box.Height) in $work)"
        Check ($box.Bottom -le $work.Bottom - $band * $scale + 1) 'its foot is clear of the band where the system draws its volume flyout'
        Check ($box.Bottom -ge $work.Bottom - ($band * $scale) - $box.Height - 2) 'and it sits just above that band, at the foot of the screen'

        $style = [NeapOverlay]::GetWindowLong($handle, -20)
        foreach ($name in $styles.Keys) { Check (($style -band $styles[$name]) -ne 0) "the window is marked $name" }

        $script:Window = $overlay
        $script:Handle = $handle
        Save-Shot "overlay-$theme" -Whole | ForEach-Object { $_.Dispose() }

        Check (Wait-Until { $null -eq (Find-Overlay) } 4) 'it goes soon after the wheel stops'
        Turn-Wheel
        Check (Wait-Until { $null -ne (Find-Overlay) } 4) 'turning the wheel again shows it again'
    }
    finally { Stop-NeapPretend }
}

Section 'the mix overlay, switched off from the mix card'
Start-WithMix 'dark' $true
try {
    $saved = { (Get-Content (Join-Path $env:LOCALAPPDATA 'Neap\Pretend\app-settings.json') -Raw | ConvertFrom-Json).mix_overlay }
    $button = Find-Named 'Mix settings'
    Check ($null -ne $button) 'the mix card on Home has a settings button'
    if ($button) { Press $button }
    Check (Wait-Until { $null -ne (Find-Anywhere 'Show the mix as it moves') } 4) 'its flyout has the overlay switch'
    Check ($null -ne (Find-Anywhere 'Keyboard shortcuts')) 'and the keyboard shortcuts switch'
    Save-ListShot 'overlay-settings'
    Press (Find-Anywhere 'Show the mix as it moves')
    Check (Wait-Until { (& $saved) -eq $false } 4) 'switching it off is saved'

    $start = Send-Pretend 'mix'
    Turn-Wheel
    Check (Wait-Until { (Send-Pretend 'mix') -ne $start } 4) 'the wheel still moves the mix'
    Start-Sleep -Milliseconds 800
    Check ($null -eq (Find-Overlay)) 'with the overlay off, the wheel shows nothing'
}
finally { Stop-NeapPretend }
