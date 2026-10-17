<#
.SYNOPSIS
Checks the max loudness boost control on the Audio page in pretend mode: that
it shows for the Stealth Pro II, describes itself, starts at Normal, follows
the slider, and stays hidden on the Atlas Air, which does not answer for it.

.DESCRIPTION
Run through run.ps1. Launches the published app in pretend mode on the Audio
page, once as the Stealth Pro II and once as the Atlas Air.

.PARAMETER Parts
Which parts to run: A the Stealth Pro II, B the Atlas Air. Both by default.
#>
param([string]$Parts = 'AB')

. "$PSScriptRoot\Ui.ps1"

$row = 'Max loudness boost'

function Find-SliderNamed([string]$Name) {
    @($script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Slider)))) |
        Where-Object { $_.Current.Name -eq $Name } | Select-Object -First 1
}


if ($Parts.Contains('A')) {
    Section 'the Stealth Pro II: the boost is there and follows the slider'
    Start-NeapPretend -Page audio -Flags @()
    try {
        Set-NeapSize 1400 1200 'Master volume'
        Wait-Until { $null -ne (Find-Named $row) } 10 | Out-Null
        $control = Find-Named $row
        Check ($null -ne $control) "the '$row' control is on the Audio page"
        Check ((Get-Texts) -contains '-12 dB') 'it starts at the stock level, -12 dB'

        $slider = Find-SliderNamed $row
        $rv = $null
        if ($slider -and $slider.TryGetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern, [ref]$rv)) { $rv.SetValue(6) }
        Check (Wait-Until { (Get-Texts) -contains '-6 dB' } 5) 'a +6 dB boost reads as -6 dB on the headset'
        if ($rv) { $rv.SetValue(0) }
        Check (Wait-Until { (Get-Texts) -contains '-12 dB' } 5) 'back at the bottom it reads -12 dB again'

        Save-Shot 'loudness-stealth' -Whole | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('B')) {
    Section 'the Atlas Air: the boost is hidden, as it does not answer for it'
    Start-NeapPretend -Page audio -Flags @('--atlas')
    try {
        # The master volume row names its slider, which FindFirst reaches
        # without the whole-tree read that can throw just after the window opens.
        Wait-Until { $null -ne (Find-Named 'Master volume') } 15 | Out-Null
        Check ($null -ne (Find-Named 'Master volume')) 'the Audio page is up for the Atlas Air'
        Check (-not (Wait-Until { $null -ne (Find-Named $row) } 3)) "the '$row' control is not there on the Atlas Air"
        Save-Shot 'loudness-atlas' -Whole | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }
}

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
