<#
.SYNOPSIS
Checks that the Firmware page keeps the keyboard focus on the row a person
ticks, when ticking redraws the list, on the published app in pretend mode.

.DESCRIPTION
Run through run.ps1. The page is out of the menu until firmware edits are
released, and a practice run opens on it all the same. Focus is put on an
edit's tick box, the box is ticked over UI Automation, and the practice run's
pipe is asked to move focus on as Tab would: it says where focus was when
asked, which is how focus lost in the redraw shows as nowhere. Nothing is
written; ticking only changes the plan the page shows.
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

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
