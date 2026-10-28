<#
.SYNOPSIS
Checks that the parametric equaliser follows a gain another app changes, on
the published app in pretend mode.

.DESCRIPTION
Run through run.ps1. The pretend headset, on Bass Boost at +5 dB for band 1,
reports +2 dB for it, as the phone app or Swarm II would, and the band's gain
in the table follows it while its filter and frequency stay. Nothing here
changes a real headset.
#>
param([string]$Parts = 'A')

. "$PSScriptRoot\Ui.ps1"

# The gain shown in a band's field: the text box inside the named control.
function Get-Field([string]$Name) {
    foreach ($element in Find-All $Name) {
        $edit = $element.FindFirst($Scope::Subtree, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Edit)))
        $pattern = $null
        if ($edit -and $edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) { return $pattern.Current.Value }
    }
}

if ($Parts.Contains('A')) {
    Section 'another app changes a gain'
    Start-NeapPretend -Page equaliser -Flags @('--parametric', '--wired')
    try {
        Set-NeapSize 1400 1500 'Equaliser'
        Check (Wait-Until { $null -ne (Find-Named 'Band 1 in use') } 20) 'the table is there'
        Press (Find-Named 'Band 1 in use')
        Check (Wait-Until { (Get-Field 'Band 1 gain') -eq '0.0' } 4) 'band 1 is in use at 0 dB'
        $frequency = Get-Field 'Band 1 frequency'

        Send-Pretend 'report eq_band_1 20' | Out-Null
        Check (Wait-Until { (Get-Field 'Band 1 gain') -eq '+2.0' } 6) 'band 1 follows the new gain of +2 dB'
        Check ((Get-Field 'Band 1 frequency') -eq $frequency) "and keeps its frequency, $frequency"

        Send-Pretend 'report eq_band_2 -30' | Out-Null
        Start-Sleep -Seconds 2
        Check ((Get-Field 'Band 2 gain') -eq '0.0') 'a band not in use is left alone'
    }
    finally { Stop-NeapPretend }
}
