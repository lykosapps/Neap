<#
.SYNOPSIS
Checks the loudness boost control on the Audio page in pretend mode: that
it shows for the Stealth Pro II with its boost on the slider and the headset's
level apart from it, in the slider's help text, and that on the Atlas Air it
is not there at all. A recording started in Settings lists the boost's writes.

.DESCRIPTION
Run through run.ps1. Launches the published app in pretend mode on the Audio
page, once as the Stealth Pro II and once as the Atlas Air; then once more to
record a change of the boost and read the saved file.

.PARAMETER Parts
Which parts to run: A the Stealth Pro II, B the Atlas Air, C a recording of
the boost being changed. All by default.
#>
param([string]$Parts = 'ABC')

. "$PSScriptRoot\Ui.ps1"

$row = 'Loudness boost'
$caution = 'Turn it up slowly to protect your hearing.'

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
        Check ((Get-Texts) -contains $caution) 'the hearing caution shows with it'
        $slider = Find-SliderNamed $row
        Check ($slider -and $slider.Current.HelpText -eq 'Headset level: -12 dB') 'the headset level reads -12 dB, apart from the slider'

        $rv = $null
        if ($slider -and $slider.TryGetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern, [ref]$rv)) {
            Check ($rv.Current.Maximum -eq 18) 'the slider tops out at +18 dB'
            Check ($rv.Current.Minimum -eq -12) 'the slider bottoms out at -12 dB, below the headset''s normal level'
            $rv.SetValue(18)
        }
        Check (Wait-Until { (Get-Texts) -contains '+18 dB' } 5) 'at the top the slider reads +18 dB'
        Check (Wait-Until { $slider.Current.HelpText -eq 'Headset level: +6 dB' } 5) 'and the headset level reads +6 dB'

        # A quick sweep up from the bottom, as a drag does, then a pause: the
        # slider stays where it was left rather than bouncing back to a step it passed.
        if ($rv) {
            foreach ($step in 0, 3, 6, 9, 12, 15, 18) { $rv.SetValue($step); Start-Sleep -Milliseconds 40 }
            Start-Sleep -Seconds 3
            Check ($rv.Current.Value -eq 18) "after a quick sweep the slider stays at +18 dB (it is at +$($rv.Current.Value))"
            Check ($slider.Current.HelpText -eq 'Headset level: +6 dB') 'and the headset level settles at +6 dB'
        }
        if ($rv) { $rv.SetValue(-12) }
        Check (Wait-Until { (Get-Texts) -contains '-12 dB' } 5) 'at the bottom the slider reads -12 dB'
        Check (Wait-Until { $slider.Current.HelpText -eq 'Headset level: -24 dB' } 5) 'and the headset level reads -24 dB'
        if ($rv) { $rv.SetValue(0) }
        Check (Wait-Until { $slider.Current.HelpText -eq 'Headset level: -12 dB' } 5) 'back at +0 the headset level reads -12 dB again'

        Save-Shot 'loudness-stealth' -Whole | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('B')) {
    Section 'the Atlas Air: no boost'
    Start-NeapPretend -Page audio -Flags @('--atlas')
    try {
        # The master volume row names its slider, which FindFirst reaches
        # without the whole-tree read that can throw just after the window opens.
        Wait-Until { $null -ne (Find-Named 'Master volume') } 15 | Out-Null
        Check ($null -ne (Find-Named 'Master volume')) 'the Audio page is up for the Atlas Air'
        Check (-not ((Get-Texts) -contains $row)) "there is no '$row' row"
        Check ($null -eq (Find-SliderNamed $row)) 'no slider'
        Check ($null -eq (Find-Named $caution)) 'and no hearing caution'
        Save-Shot 'loudness-atlas' -Whole | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('C')) {
    Section 'a recording lists the loudness boost writes'
    # A practice run saves its recordings in its own folder, so any file there is this run's.
    $folder = Join-Path $env:LOCALAPPDATA 'Neap\Pretend'
    Start-NeapPretend -Page settings -Flags @()
    try {
        Wait-Until { $null -ne (Find-Named 'Start') } 10 | Out-Null
        $start = Find-Named 'Start'
        Check ($null -ne $start) 'Settings offers to start a recording'
        Press $start
        Check (Wait-Until { $null -ne (Find-Named 'Stop and save') } 5) 'a recording is under way'

        # The page is changed from inside the app, so the window stays where it is.
        Send-Pretend 'page audio' | Out-Null
        Set-NeapSize 1400 1200 'Master volume'
        Wait-Until { $null -ne (Find-SliderNamed $row) } 10 | Out-Null
        $slider = Find-SliderNamed $row
        $rv = $null
        if ($slider -and $slider.TryGetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern, [ref]$rv)) { $rv.SetValue(6) }
        Check ($null -ne $rv -and (Wait-Until { $slider.Current.HelpText -eq 'Headset level: -6 dB' } 5)) 'the boost is changed to +6 dB, and the headset follows'

        Send-Pretend 'page settings' | Out-Null
        Wait-Until { $null -ne (Find-Named 'Stop and save') } 10 | Out-Null
        Press (Find-Named 'Stop and save')
        # The card's line is the button's help text, not a text of its own, so the file is waited for.
        Check (Wait-Until { $null -ne (Get-ChildItem $folder -Filter '*.txt' -File -ErrorAction SilentlyContinue | Select-Object -First 1) } 30) 'the recording is saved'
        Start-Sleep -Milliseconds 500

        $file = Get-ChildItem $folder -Filter '*.txt' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
        Check ($null -ne $file) 'the saved file is in the practice run''s folder'
        if ($file) {
            $lines = @(Get-Content $file.FullName | Where-Object { $_ -match '^\S+\s+write\s+loudness_level\s' })
            $lines | ForEach-Object { Note $_.Trim() }
            Check ($lines.Count -ge 1) 'the recording lists the loudness boost write'
        }
    }
    finally { Stop-NeapPretend }
}

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
