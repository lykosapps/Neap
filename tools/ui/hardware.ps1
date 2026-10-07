<#
.SYNOPSIS
Release checks that need the real headset, driven through the real app.

.DESCRIPTION
Run through run.ps1, with the headset on and nobody using the PC: the window
comes to the front. Every part runs inside Protect-RealRun, which stops the
owner's own copy for the run and puts back the sign-in shortcut and the copy
afterwards. What needs hands or ears (unplugging, the wheel, hearing a mode)
is not here.

.PARAMETER Parts
Which parts to run: A the first look, at what Home says about the headset.
#>
param([string]$Parts = 'A')

. "$PSScriptRoot\Ui.ps1"

$probe = Join-Path $PSScriptRoot '..\..\src\Neap.Probe\bin\Release\net10.0\Neap.Probe.exe'

# The headset's settings by name, read by the probe: a second opinion that does
# not come from the app. The probe cannot read while the app has the headset, so
# the app is stopped first.
function Read-Headset {
    Stop-NeapReal
    Start-Sleep -Seconds 2
    $values = @{}
    foreach ($line in (& $probe named 2>&1)) {
        if ("$line" -match '^\s+0x[0-9a-fA-F]+\s+(\S+)\s+(.*?)\s*$') { $values[$Matches[1]] = $Matches[2] }
    }
    $values
}

function Find-Radio([string]$Name) {
    $both = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty $Name), (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::RadioButton)))
    $script:Window.FindFirst($Scope::Descendants, $both)
}

# Opens the app on a page, runs a block against it, and stops the app so the
# headset can be read by the probe.
function Use-Page([string]$Page, [scriptblock]$Block, [int]$Settle = 5) {
    $skip = @(Get-RealLog).Count
    Start-NeapReal -Page $Page
    if (-not $script:Window) { Check $false "the app opened on $Page"; return }
    Start-Sleep -Seconds $Settle
    & $Block
    Start-Sleep -Seconds 2
    Stop-NeapReal
    Get-RealLog $skip | Where-Object { $_ -and $_ -notmatch ' mix: named | apps: named ' } | ForEach-Object { Note "log: $_" }
}

# Chooses a noise mode on Audio, then reads what the headset kept.
function Set-Noise([string]$Mode, [Nullable[int]]$Blocking = $null) {
    Use-Page 'audio' {
        $radio = Find-Radio $Mode
        if (-not $radio) { Check $false "Audio offers $Mode"; return }
        if (-not $radio.GetCurrentPropertyValue([System.Windows.Automation.SelectionItemPattern]::IsSelectedProperty)) { Press $radio; Start-Sleep -Seconds 2 }
        if ($null -ne $Blocking) {
            $slider = Find-Named 'Blocking'
            if ($slider) { $slider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue([double]$Blocking); Start-Sleep -Seconds 2 }
            else { Check $false 'Audio has a Blocking slider' }
        }
    }
    Read-Headset
}

# The format Windows has for the headset's output, by the probe.
function Get-OutputFormat {
    $lines = @(& $probe formats 2>&1 | ForEach-Object { "$_" })
    ($lines | Where-Object { $_ -match '^\s+current:\s*(.+?)\s*$' } | Select-Object -First 1) -replace '^\s+current:\s*', ''
}

# Chooses an option of a drop-down by name, with the mouse's way (a pattern call).
function Select-Option([string]$Box, [string]$Option) {
    $combo = Find-Named $Box
    if (-not $combo) { Check $false "$Box is on the page"; return }
    try { $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() } catch { Press $combo }
    Start-Sleep -Milliseconds 800
    $item = Wait-Until { $script:found = Find-Anywhere-ListItem $Option; $null -ne $script:found } 4
    if (-not $item) { Check $false "$Box offers $Option"; return }
    $script:found.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
}
function Find-Anywhere-ListItem([string]$Name) {
    foreach ($window in $A::RootElement.FindAll($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id)))) {
        $both = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty $Name), (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem)))
        $hit = $window.FindFirst($Scope::Descendants, $both)
        if ($hit) { return $hit }
    }
}

Protect-RealRun {
    if ($Parts.Contains('A')) {
        Section 'the first look'
        $skip = @(Get-RealLog).Count
        Start-NeapReal -Page home
        Check ($null -ne $script:Window) 'the real app opened a window'
        if ($script:Window) {
            Start-Sleep -Seconds 8
            Note ('window: ' + $script:Window.Current.Name)
            foreach ($t in @(Get-Texts | Where-Object { $_ })) { Note "text: $t" }
            Save-Shot 'hardware-home' -Whole | ForEach-Object { $_.Dispose() }
        }
        Get-RealLog $skip | Select-Object -First 40 | ForEach-Object { Note "log: $_" }
    }

    if ($Parts.Contains('B')) {
        Section 'noise control, as the headset keeps it'
        $start = Read-Headset
        Note "started with anc=$($start.anc), level=$($start.anc_level)"
        try {
            $v = Set-Noise 'Noise cancellation' 60
            Note "noise cancellation at 60%: anc=$($v.anc), level=$($v.anc_level)"
            Check ($v.anc -eq '1' -and [int]$v.anc_level -gt 0) 'noise cancellation is on, with a level above zero'
            $level = $v.anc_level
            $v = Set-Noise 'Transparency'
            Note "transparency: anc=$($v.anc), level=$($v.anc_level)"
            Check ($v.anc -eq '1' -and $v.anc_level -eq '0') 'transparency is noise cancellation at zero'
            $v = Set-Noise 'Noise cancellation'
            Note "back to noise cancellation: anc=$($v.anc), level=$($v.anc_level)"
            Check ($v.anc -eq '1' -and $v.anc_level -eq $level) 'noise cancellation returns at the level it had'
            $v = Set-Noise 'Off'
            Note "off: anc=$($v.anc), level=$($v.anc_level)"
            Check ($v.anc -eq '0') 'off turns noise cancellation off'
        }
        finally {
            # Put the headset back as it was found.
            $mode = if ($start.anc -eq '0') { 'Off' } elseif ($start.anc_level -eq '0') { 'Transparency' } else { 'Noise cancellation' }
            $v = Set-Noise $mode
            Check ($v.anc -eq $start.anc -and $v.anc_level -eq $start.anc_level) "the headset is back as found ($mode)"
        }
    }

    if ($Parts.Contains('O')) {
        Section 'the Off mode on its own'
        $v = Set-Noise 'Off'
        Note "off: anc=$($v.anc), level=$($v.anc_level)"
        $v = Set-Noise 'Transparency'
        Note "transparency again: anc=$($v.anc), level=$($v.anc_level)"
    }

    if ($Parts.Contains('F')) {
        Section 'the audio format'
        $start = Get-OutputFormat
        Note "started at $start"
        try {
            Use-Page 'device' {
                Select-Option 'Headset output' '24-bit, 48 kHz (DVD quality)'
                Start-Sleep -Seconds 3
                $now = Get-OutputFormat
                Check ($now -eq '24-bit, 48 kHz (DVD quality)') "the mouse's choice reached Windows: $now"
                Select-Option 'Headset output' '16-bit, 48 kHz (DVD quality)'
                Start-Sleep -Seconds 3
                $now = Get-OutputFormat
                Check ($now -eq '16-bit, 48 kHz (DVD quality)') "a second choice reached Windows: $now"
                Check ($null -ne (Find-Named 'Headset output')) 'the app is still open and on the Device page'
            }
        }
        finally {
            Use-Page 'device' {
                Select-Option 'Headset output' $start
                Start-Sleep -Seconds 3
                Check ((Get-OutputFormat) -eq $start) "the format is back as found: $(Get-OutputFormat)"
            }
        }
    }

    if ($Parts.Contains('R')) {
        Section 'putting the format back to 24-bit, 96 kHz'
        Use-Page 'device' { Select-Option 'Headset output' '24-bit, 96 kHz (Studio quality)'; Start-Sleep -Seconds 3 }
        Check ((Get-OutputFormat) -eq '24-bit, 96 kHz (Studio quality)') "the format is $(Get-OutputFormat)"
    }

    if ($Parts.Contains('K')) {
        Section 'the mix, the shortcuts and recovery'
        $settings = Get-Content (Join-Path $env:LOCALAPPDATA 'Neap\app-settings.json') -Raw | ConvertFrom-Json
        Note ("chat apps: {0}; shortcuts were on: {1}" -f ($settings.chat_apps -join ', '), $settings.mix_hotkeys)
        Note ("volumes before: " + ((& $probe volumes) -join ' | '))
        $skip = @(Get-RealLog).Count
        Start-NeapReal -Page controls
        Start-Sleep -Seconds 7
        # The switch does not report its state to automation, so the saved setting is read.
        $savedKeys = { (Get-Content (Join-Path $env:LOCALAPPDATA 'Neap\app-settings.json') -Raw | ConvertFrom-Json).mix_hotkeys -eq $true }
        $keys = Find-Named 'Keyboard shortcuts'
        Check ($null -ne $keys) 'the shortcuts switch is on the Controls page'
        $wasOn = & $savedKeys
        if (-not $wasOn) { Press $keys; Start-Sleep -Seconds 2 }
        Check (& $savedKeys) 'the shortcuts are on'

        $shell = New-Object -ComObject WScript.Shell
        1..4 | ForEach-Object { $shell.SendKeys('^%{PGUP}'); Start-Sleep -Milliseconds 600 }
        Start-Sleep -Seconds 3
        $moved = @(& $probe volumes)
        Note ("volumes after four presses toward chat: " + ($moved -join ' | '))
        Check (@($moved | Where-Object { $_ -match '\s0\.\d\d' }).Count -gt 0) 'the keys moved the mix: some application is below full volume'

        # Ending the app the hard way, with the mix away from the middle.
        Stop-Process -Id $script:Proc.Id -Force
        Start-Sleep -Seconds 2
        Note ("volumes after the app was ended: " + ((& $probe volumes) -join ' | '))
        Start-NeapReal -Page controls
        Start-Sleep -Seconds 8
        $back = @(& $probe volumes)
        Note ("volumes after starting it again: " + ($back -join ' | '))
        Check (@($back | Where-Object { $_ -match '\s0\.\d\d' }).Count -eq 0) 'every other application is back at full volume'

        $shell.SendKeys('^%{HOME}')
        Start-Sleep -Seconds 3
        if (-not $wasOn) {
            Start-Sleep -Seconds 1
            $keys = Find-Named 'Keyboard shortcuts'
            if ($keys) { Press $keys; Start-Sleep -Seconds 2 }
            Check (-not (& $savedKeys)) 'the shortcuts are back off, as found'
        }
        Stop-NeapReal
        Get-RealLog $skip | Where-Object { $_ -match 'mix|keys|shortcut|hotkey' -and $_ -notmatch 'named ' } | ForEach-Object { Note "log: $_" }
        Note ("volumes at the end: " + ((& $probe volumes) -join ' | '))
    }

    if ($Parts.Contains('T')) {
        Section 'the spare battery and the transmitters'
        Stop-NeapReal
        Start-Sleep -Seconds 2
        $slots = @(& $probe transmitters 2>&1 | ForEach-Object { "$_" })
        $slots | ForEach-Object { Note "probe: $_" }
        Use-Page 'device' {
            $texts = @(Get-Texts | Where-Object { $_ })
            $at = [Array]::IndexOf($texts, 'Spare battery')
            Check ($at -ge 0) 'the Device page shows the spare battery'
            if ($at -ge 0) { Note ("page: Spare battery " + $texts[$at + 1]) }
            Note ('page: ' + (($texts | Select-Object -Skip 12 -First 25) -join ' | '))
        } 8
    }

    if ($Parts.Contains('L')) {
        Section 'starting when you sign in'
        $shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\Neap.lnk'
        $read = { if (Test-Path $shortcut) { $l = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut); "$($l.TargetPath) $($l.Arguments)" } else { 'none' } }
        Start-NeapReal -Page settings
        Start-Sleep -Seconds 6
        $after = & $read
        Note "the shortcut after the app started: $after"
        Check ($after -match 'Neap\.Desktop\.exe --startup$') 'with the switch on, the sign-in entry points at the running copy and starts it quietly'
        $switch = Find-Named 'Start when you sign in'
        Check ($null -ne $switch) 'the switch is on Settings'
        if ($switch) {
            Press $switch; Start-Sleep -Seconds 2
            Check ((& $read) -eq 'none') 'turning it off removes the entry'
            $switch = Find-Named 'Start when you sign in'
            Press $switch; Start-Sleep -Seconds 2
            Check ((& $read) -match 'Neap\.Desktop\.exe --startup$') 'turning it on writes the entry again'
        }
        Stop-NeapReal
        Start-Sleep -Seconds 2
        $quiet = Start-Process $script:Exe -ArgumentList '--startup' -PassThru
        Start-Sleep -Seconds 8
        $window = $A::RootElement.FindFirst($Scope::Children, (New-Condition ProcessIdProperty ([int]$quiet.Id)))
        Check (-not $quiet.HasExited) 'started the way sign-in starts it, the app keeps running'
        Check ($null -eq $window) 'and it opens no window'
        Stop-Process -Id $quiet.Id -Force -ErrorAction SilentlyContinue
    }

    if ($Parts.Contains('N')) {
        Section 'putting noise control back to transparency'
        $v = Set-Noise 'Transparency'
        Check ($v.anc -eq '1' -and $v.anc_level -eq '0') "noise control is transparency (anc=$($v.anc), level=$($v.anc_level))"
    }

    if ($Parts.Contains('D')) {
        Section 'what the Controls page offers'
        Use-Page 'controls' {
            foreach ($e in $script:Window.FindAll($Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
                if ($e.Current.Name) { Note ("{0} | {1} | off={2}" -f $e.Current.ControlType.ProgrammaticName.Replace('ControlType.', ''), $e.Current.Name, $e.Current.IsOffscreen) }
            }
        } 8
    }

    if ($Parts.Contains('P')) {
        Section 'what the probe reads from the headset with no app open'
        $values = Read-Headset
        $values.GetEnumerator() | Sort-Object Key | ForEach-Object { Note ("{0} = {1}" -f $_.Key, $_.Value) }
    }
}
