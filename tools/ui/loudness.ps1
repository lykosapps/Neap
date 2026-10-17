<#
.SYNOPSIS
Checks the max loudness boost control on the Audio page in pretend mode: that
it shows for the Stealth Pro II with its boost on the slider and the headset's
level apart from it, and stays hidden on the Atlas Air, as it is for the
Stealth Pro II only.

.DESCRIPTION
Run through run.ps1. Launches the published app in pretend mode on the Audio
page, once as the Stealth Pro II and once as the Atlas Air.

.PARAMETER Parts
Which parts to run: A the Stealth Pro II, B the Atlas Air. Both by default.
#>
param([string]$Parts = 'AB')

. "$PSScriptRoot\Ui.ps1"

$row = 'Max loudness boost (Stealth Pro II only)'

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
        Check ((Get-Texts) -contains '+0 dB') 'the slider starts at +0 dB'
        Check ((Get-Texts) -contains 'Headset level: -12 dB') 'the headset level reads -12 dB, apart from the slider'

        $slider = Find-SliderNamed $row
        $rv = $null
        if ($slider -and $slider.TryGetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern, [ref]$rv)) {
            Check ($rv.Current.Maximum -eq 18) 'the slider tops out at +18 dB'
            $rv.SetValue(18)
        }
        Check (Wait-Until { (Get-Texts) -contains '+18 dB' } 5) 'at the top the slider reads +18 dB'
        Check (Wait-Until { (Get-Texts) -contains 'Headset level: +6 dB' } 5) 'and the headset level reads +6 dB'
        if ($rv) { $rv.SetValue(0) }
        Check (Wait-Until { (Get-Texts) -contains 'Headset level: -12 dB' } 5) 'back at +0 the headset level reads -12 dB again'

        Save-Shot 'loudness-stealth' -Whole | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('B')) {
    Section 'the Atlas Air: the boost is hidden, as it is for the Stealth Pro II only'
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
