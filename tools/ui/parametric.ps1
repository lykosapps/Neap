<#
.SYNOPSIS
Checks typing a frequency into the parametric equaliser on the published app
in pretend mode: a bare number takes the unit the field showed, and a
frequency with its unit is read as typed.

.DESCRIPTION
Run through run.ps1. The game equaliser opens in parametric mode with four
adjustments (100 Hz, 1 kHz, 4.9 kHz and 12 kHz). A frequency is typed into
the field over UI Automation's value pattern and taken as it is when the
field is left, which the practice run's pipe does by moving focus on as Tab
would.
#>

. "$PSScriptRoot\Ui.ps1"

function Find-Chip([int]$Number) {
    Search {
        $all = $script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Button)))
        @($all) | Where-Object { $_.Current.Name -like "Adjustment $Number, *" } | Select-Object -First 1
    }
}
# The field is named on its box and on the text inside it; the text takes a value.
function Find-Field {
    Search {
        $both = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty 'Frequency, typed'),
            (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Edit)))
        $script:Window.FindFirst($Scope::Descendants, $both)
    }
}
function Get-Field { (Find-Field).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value }

# Types into the field and leaves it, as a person does, and waits for what it settles on.
function Enter-Frequency([string]$Typed) {
    $field = Find-Field
    $field.SetFocus()
    $field.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Typed)
    Send-Pretend 'focus next' | Out-Null
    Wait-Stable { Get-Field } | Out-Null
}

Section 'typing a frequency in the parametric equaliser'
Start-NeapPretend -Page equaliser -Flags @('--parametric')
try {
    Set-NeapSize 1400 1400 'Frequency, typed'
    Wait-Until { $null -ne (Find-Chip 3) } 15 | Out-Null
    Check ($null -ne (Find-Chip 3)) 'the equaliser is in parametric mode with its adjustments'
    if (-not (Find-Chip 3)) { throw 'no adjustment to type into' }

    # The third adjustment shows kilohertz, so a bare number is kilohertz.
    Press (Find-Chip 3)
    Check (Wait-Until { (Get-Field) -eq '4.9 kHz' } 4) 'the third adjustment shows 4.9 kHz'
    Enter-Frequency '20'
    Check ((Get-Field) -eq '20 kHz') "a bare 20 over a kHz field is 20 kHz (the field reads '$(Get-Field)')"
    Check ($null -ne (Find-Chip 3) -and (Find-Chip 3).Current.Name -eq 'Adjustment 3, 20 kHz') 'and the adjustment says so'

    # A unit typed is taken as typed, with a comma between thousands as a separator.
    Enter-Frequency '1,500 Hz'
    Check ((Get-Field) -eq '1.5 kHz') "'1,500 Hz' is 1500 Hz, shown as 1.5 kHz (the field reads '$(Get-Field)')"
    Check ((Find-Chip 3).Current.Name -eq 'Adjustment 3, 1.5 kHz') 'and the adjustment says so'

    # Over a field showing hertz, a bare number is hertz.
    Press (Find-Chip 1)
    Check (Wait-Until { (Get-Field) -eq '100 Hz' } 4) 'the first adjustment shows 100 Hz'
    Enter-Frequency '250'
    Check ((Get-Field) -eq '250 Hz') "a bare 250 over a hertz field is 250 Hz (the field reads '$(Get-Field)')"

    Save-Shot 'parametric-typing' -Whole | ForEach-Object { $_.Dispose() }
}
finally { Stop-NeapPretend }

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
