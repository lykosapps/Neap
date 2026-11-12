<#
.SYNOPSIS
Checks the Firmware page on the published app in pretend mode: the keyboard
focus stays on a ticked row, an edit applied reads as active from the
pretend flash and as not installed again after Remove all edits, and a write that is
lost is put back, each in dark, light and high contrast; and both edits at once,
 which asks to plug in again for the second session, stopped there, with noise
cancellation's modes then offered in the headset's order; and a Swarm II
recovery while the headset is off, read again as it connects; and Apply refused
while Swarm II runs. A run takes
over the whole window, with the rest of it switched off, and offers no Close
until it ends.

.DESCRIPTION
Run through run.ps1. The page is out of the menu until firmware edits are
released, and a practice run opens on it all the same. Focus is put on an
edit's tick box, the box is ticked over UI Automation, and the practice run's
pipe is asked to move focus on as Tab would: it says where focus was when
asked, which is how focus lost in the redraw shows as nowhere. Then the
light edit is applied and taken off with Remove all edits, through the pretend writer, which
writes the pretend headset's flash, and once more with the first write lost.
Both runs are repeated in light and high contrast, with the window tall
enough to show the backups, for a person to look over. Nothing real is written.
#>

. "$PSScriptRoot\Ui.ps1"

function Find-Tick([string]$Name) {
    Search {
        $both = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty $Name),
            (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::CheckBox)))
        $script:Window.FindFirst($Scope::Descendants, $both)
    }
}
function Get-State($Element) { [string]$Element.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState }

Section 'the Firmware page keeps focus on a ticked row'
Start-NeapPretend -Page firmware -Flags @()
try {
    $names = 'No blinking when disconnected', 'Noise cancellation on the Mode button'
    Wait-Until { $null -ne (Find-Tick $names[0]) -or $null -ne (Find-Tick $names[1]) } 15 | Out-Null
    $name = $names | Where-Object { Find-Tick $_ } | Select-Object -First 1
    Check ($null -ne $name) 'the Firmware page opens in a practice run, with edits to tick'
    if (-not $name) { throw 'no edit to tick' }
    $tick = Find-Tick $name
    Check ($tick.Current.IsEnabled) "the tick box for '$name' can be used"
    Save-Shot 'firmware-before' -Whole | ForEach-Object { $_.Dispose() }

    $before = Get-State $tick
    $tick.SetFocus()
    # Focus is on the box, not a name for it that could be matched by luck: Tab from here says so.
    $tick.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Check (Wait-Until { (Get-State (Find-Tick $name)) -ne $before } 4) "ticking '$name' changes it from $before"
    Wait-Stable { (Get-Texts) -join '|' } | Out-Null
    Save-Shot 'firmware-after' -Whole | ForEach-Object { $_.Dispose() }

    $moved = Send-Pretend 'focus next'
    Check ($null -ne $moved -and $moved.from -like "*CheckBox `"$name`"*") "after the redraw the keyboard focus is still on '$name' (focus was on: $($moved.from))"

    # Ticked back, the same row keeps it again.
    $tick = Find-Tick $name
    $tick.SetFocus()
    $tick.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Check (Wait-Until { (Get-State (Find-Tick $name)) -eq $before } 4) "unticking '$name' puts it back to $before"
    Wait-Stable { (Get-Texts) -join '|' } | Out-Null
    $moved = Send-Pretend 'focus next'
    Check ($null -ne $moved -and $moved.from -like "*CheckBox `"$name`"*") "and the keyboard focus is still on '$name' (focus was on: $($moved.from))"

    # With no edit on the headset, the Mode button's jobs offer Neap's own cycle and name the firmware's as not installed.
    Open-Page 'Controls'
    Check ($null -ne (Find-Named 'Noise cancellation: cancelling, transparency and off')) "on factory firmware, Controls offers Neap's own cycle"
    $texts = Get-Texts
    $about = [array]::IndexOf($texts, 'With a firmware edit, the button steps through off, transparency and cancelling without Neap.')
    Check ($about -ge 0 -and $texts[$about + 1] -eq 'Not installed') "and under the jobs the firmware's cycle reads as not installed"
    Save-Shot 'firmware-controls-not-installed' -Whole | ForEach-Object { $_.Dispose() }
}
finally { Stop-NeapPretend }

$light ='No blinking when disconnected'
# Each edit's state is a chip with the state's word; the light edit is the one ticked.
function Get-StateCount([string]$State) { @(Get-Texts | Where-Object { $_ -eq $State }).Count }

# Presses a page button, then the same button in the dialog that asks first.
# The dialog can be a window of its own or drawn over the page, so its button
# is told from the page's by being a different element.
function Confirm-Press([string]$Name) {
    $page = Find-Anywhere $Name
    $first = $page.GetRuntimeId() -join '.'
    Press $page
    $asked = $null
    Check (Wait-Until {
        $script:asked = @(Find-Anywhere $Name) + @(Find-All $Name) | Where-Object { $_ -and ($_.GetRuntimeId() -join '.') -ne $first } | Select-Object -Last 1
        $null -ne $script:asked
    } 6) "pressing $Name asks first"
    Press $script:asked
}

# The run's Close, by its fixed name, not the window's own.
function Close-Run {
    Press (Find-Id 'TakeoverCloseButton')
    Wait-Until { -not (Find-Text $done) } 6 | Out-Null
}

$done = "It's now safe to unplug the cable and switch the headset on."
$putBack = "Neap couldn't confirm the change, so it put back what was there. It's safe to unplug the cable and switch the headset on, then try again."
$plugAgain = "Unplug the cable, then plug it back in. Don't press Power."
$plug = "Switch the headset off, then plug a USB-C cable from this PC into its left ear cup. Don't press Power."
$swarm = 'Swarm II could interrupt the change. Quit it, then try again.'
$stoppedPart = 'Stopped part way, with some edits applied. Unplug the cable, then choose Apply to finish.'

# Makes the window tall enough that the Backups card, under the run card, is in the picture.
function Show-Backups { Set-NeapSize 1400 1200 'Backups' }

# A backup row's Restore button, named for the edit and when it was saved.
function Find-BackupRestore {
    Search {
        $buttons = $script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Button)))
        @($buttons) | Where-Object { $_.Current.Name -like "Restore $light, saved *" } | Select-Object -First 1
    }
}

# The three themes the run card, the chips and the backups are looked over in:
# dark, which also checks Remove all edits, then light and high contrast.
$themes = @(
    @{ Theme = 'dark'; Flags = @(); Name = '' },
    @{ Theme = 'light'; Flags = @(); Name = '-light' },
    @{ Theme = 'dark'; Flags = @('--highcontrast'); Name = '-dark--highcontrast' }
)

foreach ($theme in $themes) {
    $in = "$($theme.Theme) $($theme.Flags)".Trim()

    Section "an edit applied reads as active, and restoring factory reads as factory ($in)"
    Start-NeapPretend -Page firmware -Theme $theme.Theme -Flags $theme.Flags
    try {
        Check (Wait-Until { (Get-StateCount 'Not installed') -eq 2 } 15) 'both edits read as factory from the pretend flash'
        if ($theme.Name) { Save-Shot "firmware-chips$($theme.Name)" -Whole | ForEach-Object { $_.Dispose() } }
        Press (Find-Tick $light)
        Confirm-Press 'Apply'
        Check (Wait-Until { Find-Text $done } 60) 'applying ends done'
        Check (Wait-Until { (Get-StateCount 'Installed') -eq 1 -and (Get-StateCount 'Not installed') -eq 1 } 10) "and '$light' then reads as active, the other as factory"
        Save-Shot "firmware-run-done$($theme.Name)" -Whole | ForEach-Object { $_.Dispose() }
        Close-Run
        Show-Backups
        Check (Wait-Until { Test-Present (Find-BackupRestore) } 6) 'the backup the write left is listed, with its Restore button in view'
        Save-Shot "firmware-applied$($theme.Name)" -Whole | ForEach-Object { $_.Dispose() }

        if (-not $theme.Name) {
            Confirm-Press 'Remove all edits'
            Check (Wait-Until { Find-Text $done } 60) 'restoring factory ends done'
            Check (Wait-Until { (Get-StateCount 'Not installed') -eq 2 } 10) 'and both read as factory again'
            Close-Run
            $rows = @(Search { $script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Button))) } |
                Where-Object { $_.Current.Name -like "Restore $light, saved *" })
            Check ($rows.Count -eq 1) "after two writes to its part, '$light' lists only its newest backup (rows: $($rows.Count))"
            Check ((Test-Present (Find-Anywhere 'Restore from file')) -and (Test-Present (Find-Anywhere 'Show in folder'))) 'Restore from file and Show in folder are offered'
        }
    }
    finally { Stop-NeapPretend }

    Section "a write that is lost is put back ($in)"
    Start-NeapPretend -Page firmware -Theme $theme.Theme -Flags (@('--firmware-reverts') + $theme.Flags)
    try {
        Check (Wait-Until { (Get-StateCount 'Not installed') -eq 2 } 15) 'both edits start as factory'
        Press (Find-Tick $light)
        Confirm-Press 'Apply'
        Check (Wait-Until { Find-Text $putBack } 60) 'the run says the section was put back'
        Show-Backups
        Save-Shot "firmware-put-back$($theme.Name)" -Whole | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }
}

Section 'with Swarm II running, Apply says to quit it and writes nothing; once it quits, Apply works'
Start-NeapPretend -Page firmware -Flags @()
try {
    Check (Wait-Until { (Get-StateCount 'Not installed') -eq 2 } 15) 'both edits start as factory'
    Send-Pretend 'start Turtle Beach Swarm II' | Out-Null
    Press (Find-Tick $light)
    Press (Find-Anywhere 'Apply')
    Check (Wait-Until { Find-Text $swarm } 6) 'pressing Apply says to quit Swarm II'
    Press (Find-Id 'CloseButton')
    Check (Wait-Until { -not (Find-Text $swarm) } 6) 'OK closes it'
    Check ((Get-StateCount 'Not installed') -eq 2 -and -not (Find-Text $plug)) 'and no run started'
    Send-Pretend 'stop Turtle Beach Swarm II' | Out-Null
    Confirm-Press 'Apply'
    Check (Wait-Until { Find-Text $done } 60) 'with Swarm II closed, applying ends done'
}
finally { Stop-NeapPretend }

Section 'a Swarm II recovery while the headset is off reads as factory once it connects again'
Start-NeapPretend -Page firmware -Flags @()
try {
    Check (Wait-Until { (Get-StateCount 'Not installed') -eq 2 } 15) 'both edits start as factory'
    Press (Find-Tick $light)
    Confirm-Press 'Apply'
    Check (Wait-Until { Find-Text $done } 60) 'applying ends done'
    Close-Run
    Check (Wait-Until { (Get-StateCount 'Installed') -eq 1 } 10) "'$light' reads as active"
    Send-Pretend 'off' | Out-Null
    Check (Wait-Until { (Get-Texts) -notcontains 'Headset connected' } 30) 'Neap sees the headset switch off'
    Send-Pretend 'recover' | Out-Null
    Send-Pretend 'on' | Out-Null
    Check (Wait-Until { (Get-StateCount 'Not installed') -eq 2 } 30) 'once the headset connects again, both read as factory, as the recovery left them'
    Check (-not (Test-Present (Find-Anywhere 'Check again'))) 'with every state known, Check again is not offered'
}
finally { Stop-NeapPretend }

Section 'both edits at once: the second session asks to plug in again, and Stop there says part is written'
Start-NeapPretend -Page firmware -Flags @()
try {
    Check (Wait-Until { (Get-StateCount 'Not installed') -eq 2 } 15) 'both edits start as factory'
    Press (Find-Tick $light)
    Press (Find-Tick 'Noise cancellation on the Mode button')
    Confirm-Press 'Apply'
    Check (Wait-Until { Find-Text $plugAgain } 90) 'after the first session is written and read back, the run asks to unplug and plug in again'
    Check (-not (Test-Present (Find-Id 'TakeoverCloseButton'))) 'while the run waits, it offers no Close'
    Check (-not (Find-Anywhere 'Apply').Current.IsEnabled -and -not (Find-Anywhere 'Remove all edits').Current.IsEnabled) 'and the page behind it is switched off'
    Save-Shot 'firmware-plug-again' -Whole | ForEach-Object { $_.Dispose() }
    Press (Find-Anywhere 'Stop')
    Check (Wait-Until { Find-Text $stoppedPart } 15) 'Stop there says part of the change is written'
    Check (Wait-Until { (Get-StateCount 'Installed') -eq 1 -and (Get-StateCount 'Not installed') -eq 1 } 10) 'and the chips show what was read back: noise cycle active, the light factory'
    Close-Run
    Check ((Get-State (Find-Tick $light)) -eq 'On' -and (Find-Anywhere 'Apply').Current.IsEnabled) 'the light stays ticked and Apply can be pressed to finish'
    Save-Shot 'firmware-stopped-part' -Whole | ForEach-Object { $_.Dispose() }

    # With the noise cycle on the headset, the modes are offered in the order its Mode button steps through them.
    Open-Page 'Audio'
    $radio = New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::RadioButton)
    $order = @((Find-Id 'noise_control').FindAll($Scope::Children, $radio) | ForEach-Object { $_.Current.Name }) -join ', '
    Check ($order -eq 'Off, Transparency, Cancelling') "Audio offers noise cancellation's modes in the headset's order (shown: $order)"
    Save-Shot 'firmware-noise-order' | ForEach-Object { $_.Dispose() }

    # And the Mode button's jobs say the firmware runs the cycle, with the way back to this page.
    Open-Page 'Controls'
    Check ($null -ne (Find-Named 'Noise cancellation: off, transparency and cancelling')) "the Mode button's noise cancellation job is named as the headset's cycle"
    Check ($null -eq (Find-Named 'Noise cancellation: cancelling, transparency and off')) "and Neap's own cycle is not offered"
    $texts = Get-Texts
    $about = [array]::IndexOf($texts, 'With a firmware edit, the button steps through off, transparency and cancelling without Neap.')
    Check ($about -ge 0 -and $texts[$about + 1] -eq 'Installed') "under the jobs, the firmware's cycle is named and reads as installed"
    Save-Shot 'firmware-controls-installed' -Whole | ForEach-Object { $_.Dispose() }
    Press (Find-Anywhere 'Open Firmware')
    Check (Wait-Until { $null -ne (Find-Tick $light) } 6) 'Open Firmware goes to the Firmware page'
}
finally { Stop-NeapPretend }

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
