<#
.SYNOPSIS
Checks the Firmware page on the published app in pretend mode: the keyboard
focus stays on a ticked row, an edit applied reads as active from the
pretend flash and back to factory after restoring stock, and a write that is
lost is put back.

.DESCRIPTION
Run through run.ps1. The page is out of the menu until firmware edits are
released, and a practice run opens on it all the same. Focus is put on an
edit's tick box, the box is ticked over UI Automation, and the practice run's
pipe is asked to move focus on as Tab would: it says where focus was when
asked, which is how focus lost in the redraw shows as nowhere. Then the
light edit is applied and stock restored through the pretend writer, which
writes the pretend headset's flash, and once more with the first write lost.
Nothing real is written.
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
    $names = 'Light off when disconnected', 'Noise cycle'
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
}
finally { Stop-NeapPretend }

$light = 'Light off when disconnected'
function Test-LightReads([string]$State) { (Get-Texts) -like "Low risk*$State" }

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

# The run card's Close, not the window's own, which sits at the top.
function Close-Run {
    Press (Find-Lowest 'Close')
    Wait-Until { -not (Find-Text 'Done. The headset has the edits you ticked.') } 6 | Out-Null
}

Section 'an edit applied reads as active, and restoring stock reads as factory'
Start-NeapPretend -Page firmware -Flags @()
try {
    Check (Wait-Until { Test-LightReads 'Factory' } 15) "'$light' reads as factory from the pretend flash"
    Press (Find-Tick $light)
    Confirm-Press 'Apply'
    Check (Wait-Until { Find-Text 'Done. The headset has the edits you ticked.' } 60) 'applying ends done'
    Check (Wait-Until { Test-LightReads 'Active' } 10) "and '$light' then reads as active"
    Save-Shot 'firmware-applied' -Whole | ForEach-Object { $_.Dispose() }
    Close-Run

    Confirm-Press 'Restore to stock'
    Check (Wait-Until { Find-Text 'Done. The headset has the edits you ticked.' } 60) 'restoring stock ends done'
    Check (Wait-Until { Test-LightReads 'Factory' } 10) "and '$light' reads as factory again"
}
finally { Stop-NeapPretend }

Section 'a write that is lost is put back'
Start-NeapPretend -Page firmware -Flags @('--firmware-reverts')
try {
    Check (Wait-Until { Test-LightReads 'Factory' } 15) "'$light' starts as factory"
    Press (Find-Tick $light)
    Confirm-Press 'Apply'
    Check (Wait-Until { Find-Text "A section didn't read back as written, so Neap put back what was there. The headset starts as before, but the edits may be only partly on." } 60) 'the run says the section was put back'
    Save-Shot 'firmware-put-back' -Whole | ForEach-Object { $_.Dispose() }
}
finally { Stop-NeapPretend }

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
