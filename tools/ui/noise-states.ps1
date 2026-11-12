<#
.SYNOPSIS
Draws every state of noise cancellation and the Mode button's cycle on the
published app in pretend mode, for a person to look over: on factory firmware
with each mode chosen and with Neap's own cycle, then with the firmware's
cycle applied, then with it taken off again.

.DESCRIPTION
Run through run.ps1. The practice run opens on the Firmware page, so the page
is in the menu and the Mode button's card names the firmware's cycle. The edit
is applied and removed through the pretend writer, which writes the pretend
headset's flash. Pictures are named noise-NN-*.png, in the order taken. It
checks only what it needs to carry on; firmware.ps1 and the walk check the
rest. Nothing real is written.
#>

. "$PSScriptRoot\Ui.ps1"

$script:Shots = 0
function Save-State([string]$Name) {
    Wait-Stable { (Get-Texts) -join '|' } | Out-Null
    $script:Shots++
    Save-Shot ('noise-{0:00}-{1}' -f $script:Shots, $Name) -Whole | ForEach-Object { $_.Dispose() }
}

# A choice inside a group found by its automation id, such as one of the three modes.
function Select-In([string]$Group, [string]$Name) {
    $choice = Search { (Find-Id $Group).FindFirst($Scope::Descendants, (New-Condition NameProperty $Name)) }
    if (-not $choice) { throw "no '$Name' in $Group" }
    Press $choice
    Wait-Stable { (Get-Texts) -join '|' } | Out-Null
}

# Presses a page button, then the same button in the dialog that asks first.
function Confirm-Press([string]$Name) {
    $page = Find-Anywhere $Name
    $first = $page.GetRuntimeId() -join '.'
    Press $page
    $script:asked = $null
    Wait-Until {
        $script:asked = @(Find-Anywhere $Name) + @(Find-All $Name) | Where-Object { $_ -and ($_.GetRuntimeId() -join '.') -ne $first } | Select-Object -Last 1
        $null -ne $script:asked
    } 6 | Out-Null
    Press $script:asked
}

function Wait-RunDone {
    $done = "It's now safe to unplug the cable and switch the headset on."
    Check (Wait-Until { Find-Text $done } 60) 'the run ends done'
    Press (Find-Id 'TakeoverCloseButton')
    Wait-Until { -not (Find-Text $done) } 6 | Out-Null
}

# Audio with each mode chosen, Home, and the Mode button's card.
function Save-Modes([string]$Stage, [string[]]$Modes) {
    Open-Page 'Audio'
    foreach ($mode in $Modes) {
        Select-In 'noise_control' $mode
        Save-State "$Stage-audio-$($mode.ToLowerInvariant())"
    }
    Open-Page 'Home'
    Save-State "$Stage-home"
    Open-Page 'Controls'
    Save-State "$Stage-controls"
}

Section 'every state of noise cancellation and the Mode button'
Start-NeapPretend -Page firmware -Flags @()
try {
    Check (Wait-Until { @(Get-Texts | Where-Object { $_ -eq 'Not installed' }).Count -eq 2 } 15) 'the practice run starts on factory firmware'

    Save-Modes 'factory' @('Off', 'Transparency', 'Cancelling')
    Select-In 'mode_button_function' 'Noise cancellation: off, transparency and cancelling'
    Save-State 'factory-controls-neaps-cycle'
    Open-Page 'Firmware'
    Save-State 'factory-firmware'

    Press (Search {
        $both = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty 'Noise cancellation on the Mode button'),
            (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::CheckBox)))
        $script:Window.FindFirst($Scope::Descendants, $both)
    })
    Confirm-Press 'Apply'
    Wait-RunDone
    Check (Wait-Until { @(Get-Texts | Where-Object { $_ -eq 'Installed' }).Count -eq 1 } 10) 'the noise cycle reads as installed'
    Save-State 'edit-firmware'
    Save-Modes 'edit' @('Off', 'Transparency', 'Cancelling')

    Open-Page 'Firmware'
    Confirm-Press 'Remove all edits'
    Wait-RunDone
    Open-Page 'Controls'
    Save-State 'removed-controls'
}
finally { Stop-NeapPretend }

Write-Host "$script:Shots pictures, $script:Fails failures"
exit ([int]($script:Fails -gt 0))
