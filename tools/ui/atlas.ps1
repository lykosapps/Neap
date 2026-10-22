<#
.SYNOPSIS
Checks the published app against the pretend Atlas Air, for what its tester
reported on issue 1: the master volume stays where it is put, Bluetooth says
whether it is in use, profiles become ready, mic monitoring is offered and
AI noise reduction is not, Home calls it by its model, and what the headset
reports can be copied.

.DESCRIPTION
Run through run.ps1. The app is launched once, in the dark theme, with the
pretend headset being an Atlas Air, and each page is opened from the rail. The
pretend headset is driven over its pipe. Copying puts text on the clipboard,
so what was there is put back afterwards.
#>

. "$PSScriptRoot\Ui.ps1"

$pipe = $null

function Ask([string]$Line) {
    $script:writer.WriteLine($Line)
    $reply = $script:reader.ReadLine() | ConvertFrom-Json
    if (-not $reply.ok) { throw "pretend headset: $Line -> $($reply.error)" }
    $reply.result
}

function Find-Typed([string]$Name, $Type) {
    Find-All $Name | Where-Object { $_.Current.ControlType -eq $Type } | Select-Object -First 1
}

Start-NeapPretend -Theme dark -Page home -Flags @('--atlas')
try {
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'Neap.Pretend', [System.IO.Pipes.PipeDirection]::InOut)
    $pipe.Connect(10000)
    $script:reader = New-Object System.IO.StreamReader($pipe)
    $script:writer = New-Object System.IO.StreamWriter($pipe)
    $script:writer.AutoFlush = $true
    Wait-Until { (Get-Texts) -contains 'Home' } 15 | Out-Null
    Set-NeapSize 1536 900 'Home'

    Section 'the name'
    Check (Wait-Until { (Get-Texts) -contains 'Atlas Air' } 5) 'Home calls it Atlas Air, not the name it holds'

    Section 'the master volume'
    Open-Page 'Audio'
    $slider = Find-Typed 'Master volume' ([System.Windows.Automation.ControlType]::Slider)
    Check ($null -ne $slider) 'the master volume slider is on the Audio page'
    if ($slider) {
        $range = $slider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
        $range.SetValue(70)
        # Every report from the headset repaints the slider from its mirror,
        # which is what sent it to 100 before; the battery gives a few.
        $seen = New-Object System.Collections.Generic.List[double]
        $clock = [Diagnostics.Stopwatch]::StartNew()
        $battery = 60
        while ($clock.Elapsed.TotalSeconds -lt 4) {
            if ($seen.Count % 5 -eq 0) { Ask "report 0x240 $battery" | Out-Null; $battery-- }
            $seen.Add($range.Current.Value)
            Start-Sleep -Milliseconds 100
        }
        Note "the slider read $(($seen | Select-Object -Unique) -join ', ') over $($seen.Count) samples"
        Check (-not ($seen -contains 100)) 'the slider never jumps to 100 as the headset reports'
        Check ($seen[-1] -eq 70) 'the slider stays where it was put'
        Check ((Ask 'value 0x260') -eq '70' -and (Ask 'value 0x2a0') -eq '100') 'the headset mirrors the level at 0x260 while 0x2a0 stays at 100'
    }
    Save-Shot 'atlas-audio' -Top 700 | ForEach-Object { $_.Dispose() }

    Section 'microphone'
    Open-Page 'Audio'
    Check (@(Find-All 'Microphone monitoring').Count -gt 0) 'microphone monitoring is offered'
    Check (@(Find-All 'Noise reduction').Count -eq 0) 'noise reduction is not offered'
    Save-Shot 'atlas-mic' -Top 700 | ForEach-Object { $_.Dispose() }

    Section 'Bluetooth'
    Open-Page 'Device'
    Check (Wait-Until { (Get-Texts) -contains 'Not in use' } 5) 'on its transmitter, Bluetooth says Not in use'
    Ask 'report 0x290 0' | Out-Null
    Check (Wait-Until { (Get-Texts) -contains 'In use' } 5) 'switched to Bluetooth, it says In use'
    Check (-not ((Get-Texts) -contains 'Connected')) 'it never says Connected'
    Ask 'report 0x290 1' | Out-Null
    Check (Wait-Until { (Get-Texts) -contains 'Not in use' } 5) 'back on its transmitter, it says Not in use again'

    Section 'copying what the headset reports'
    $expander = @($script:Window.FindAll($Scope::Descendants, (New-Condition IsExpandCollapsePatternAvailableProperty $true))) |
        Where-Object { $_.Current.ControlType -ne [System.Windows.Automation.ControlType]::ComboBox } | Select-Object -First 1
    if ($expander) { $expander.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() }
    $copy = $null
    Check (Wait-Until { $script:copy = Find-Named 'Copy everything the headset reports'; $null -ne $script:copy } 5) 'a Copy button is named for screen readers'
    Check ($script:copy -and $script:copy.Current.IsKeyboardFocusable) 'it can be reached from the keyboard'
    Save-Shot 'atlas-device' -Top 900 | ForEach-Object { $_.Dispose() }
    if ($script:copy) {
        $before = Get-Clipboard -Raw
        try {
            Set-Clipboard -Value 'nothing yet'
            Press $script:copy
            Wait-Until { (Get-Clipboard -Raw) -ne 'nothing yet' } 3 | Out-Null
            $copied = Get-Clipboard -Raw
            Check ($copied -match '0x110\s+usb_product_id\s+2260') 'the clipboard holds the list, product id included'
            Note "copied $(@($copied -split "`n").Count) lines"
        }
        finally { if ($null -ne $before) { Set-Clipboard -Value $before } }
    }

    Section 'profiles'
    Open-Page 'Profiles'
    $new = { Find-All 'New profile' | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button } | Sort-Object { $_.Current.BoundingRectangle.Y } | Select-Object -Last 1 }
    Check (Wait-Until { (& $new).Current.IsEnabled } 15) 'New profile is on once the headset has answered'
    Check (-not ((Get-Texts) -match 'Reading the headset')) 'nothing says it is still reading the settings'
    Save-Shot 'atlas-profiles' -Top 700 | ForEach-Object { $_.Dispose() }

    $log = Join-Path $env:LOCALAPPDATA 'Neap\Pretend\app.log'
    if (Test-Path $log) {
        Get-Content $log | Where-Object { $_ -match 'profiles:' } | Select-Object -Last 5 | ForEach-Object { Note "log: $_" }
    }
    $refusals = @(Ask 'refusals')
    Check ($refusals.Count -eq 0) "the pretend headset refused nothing ($($refusals.Count))"
}
finally {
    if ($pipe) { $pipe.Dispose() }
    Stop-NeapPretend
}

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
