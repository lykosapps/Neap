<#
.SYNOPSIS
Runs headset checks 40 and 41 (docs/design/HEADSET-CHECKS.md) one after the other in one copy of the app, stopping at the
first result that is not the one expected.

.DESCRIPTION
Real runs write the headset's firmware and need the owner's go first. The test build is published only if the source
has changed (prepare-headset-check.ps1 -PublishOnly), and one copy of it serves both checks, so the headset is seen once
and can stay off or on its cable between them. Lines starting "OWNER:" are the person's part, said at the moment it is
theirs.

  40. Both edits ticked and applied: the plan says 3 sections and 2 plug-ins; after the first session the run asks to
      unplug and plug in again; it ends Done with both edits Active.
  41. The newest backup of the light section restored: it ends Done, the light reads Factory and the noise cycle Active.

-Pretend runs the same steps on the pretend headset, writing nothing, to check the script itself on the desk.
#>
param([switch]$Pretend)
. "$PSScriptRoot\Ui.ps1"

$light = 'Light off when disconnected'
$cycle = 'Noise cancellation cycle'
$plug = "Switch the headset off, then plug a USB-C cable from this PC into its left ear cup. Don't press Power."
$plugAgain = "Unplug the cable, then plug it back in. Don't press Power."
$done = 'Done. The headset has the edits you ticked. Unplug the cable, and the headset starts by itself in about half a minute.'
$outcomes = '^(Done\. |Restored\. |Written, but|Stopped|A section didn|Something stopped|Nothing was written)'

function Find-Tick([string]$Name) {
    Search {
        $both = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty $Name),
            (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::CheckBox)))
        $script:Window.FindFirst($Scope::Descendants, $both)
    }
}
function Confirm-Press([string]$Name, $Page = $null) {
    $page = if ($Page) { $Page } else { Find-Anywhere $Name }
    $first = $page.GetRuntimeId() -join '.'
    Press $page
    Check (Wait-Until {
        $script:asked = @(Find-Anywhere $Name) + @(Find-All $Name) | Where-Object { $_ -and ($_.GetRuntimeId() -join '.') -ne $first } | Select-Object -Last 1
        $null -ne $script:asked
    } 6) "pressing $Name asks first"
    Press $script:asked
}
function Find-NewestLightBackup {
    Search {
        $buttons = $script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Button)))
        @($buttons) | Where-Object { $_.Current.Name -like "Restore $light, saved *" } | Select-Object -First 1
    }
}
function Get-Chips { @(Get-Texts | Where-Object { $_ -in 'Active', 'Factory', 'Neither', "Can't tell" }) }
function Owner([string]$What) { Note "$(Get-Date -Format HH:mm:ss) OWNER: $What" }

# Waits for the run to end, saying the person's part as each plug-in comes, and returns the outcome's words.
function Wait-Run {
    [void](Wait-Until {
        if (-not $script:asked -and (Find-Text $plugAgain)) { $script:asked = $true; Owner 'unplug the cable, then plug it back in' }
        Get-Texts | Where-Object { $_ -match $outcomes }
    } 600)
    @(Get-Texts | Where-Object { $_ -match $outcomes }) | Select-Object -First 1
}

function Invoke-Sequence {
    Check (Wait-Until { Find-Named $light } 30) 'the Firmware page lists the edits (if not, switch the headset on so Neap sees it)'
    Check (Wait-Until { @(Get-Chips | Where-Object { $_ -eq 'Factory' }).Count -eq 2 } 30) 'both edits read as factory before check 40'
    if (@(Get-Chips | Where-Object { $_ -eq 'Factory' }).Count -ne 2) { throw "not starting: the edits read $(Get-Chips -join ', ')" }

    Section 'check 40: both edits at once'
    Press (Find-Tick $light)
    Press (Find-Tick $cycle)
    Check (Wait-Until { (Get-Texts) -contains "Sections of the headset's firmware to write: 3. Times you plug the headset in: 2." } 6) 'the plan says 3 sections and 2 plug-ins'
    Confirm-Press 'Apply'
    Check (Wait-Until { Find-Text $plug } 15) 'the run waits at the plug-in step'
    Owner 'switch the headset off and plug the cable in'
    $script:asked = $false
    $outcome = Wait-Run
    Check $script:asked 'after the first session the run asked to unplug and plug in again'
    Note "outcome: $outcome"
    Check ($outcome -eq $done) 'check 40 ends Done'
    Check (Wait-Until { @(Get-Chips | Where-Object { $_ -eq 'Active' }).Count -eq 2 } 10) 'both edits read as active'
    Save-Shot 'sequence-check40' -Whole | ForEach-Object { $_.Dispose() }
    if ($outcome -ne $done) { throw 'stopping: check 40 did not end as expected' }
    Press (Find-Lowest 'Close')
    Owner 'unplug the cable; the headset starts by itself'

    Section 'check 41: the newest backup of the light section restored'
    $backup = $null
    Check (Wait-Until { $script:backup = Find-NewestLightBackup; $null -ne $script:backup } 10) 'the light section''s newest backup is listed'
    Note "restoring: $($script:backup.Current.Name)"
    Confirm-Press 'Restore' $script:backup
    Check (Wait-Until { Find-Text $plug } 15) 'the restore waits at the plug-in step'
    Owner 'switch the headset off and plug the cable in'
    $outcome = Wait-Run
    Note "outcome: $outcome"
    Check ($outcome -like 'Restored. *') 'check 41 ends Restored'
    Check (Wait-Until { (Get-Chips) -contains 'Active' -and (Get-Chips) -contains 'Factory' } 10) 'the light reads factory again and the noise cycle stays active'
    Save-Shot 'sequence-check41' -Whole | ForEach-Object { $_.Dispose() }
    Owner 'unplug the cable; the headset starts by itself'
}

if ($Pretend) {
    Start-NeapPretend -Page firmware -Flags @()
    try { Invoke-Sequence } finally { Stop-NeapPretend }
}
else {
    & powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\prepare-headset-check.ps1" -PublishOnly
    if ($LASTEXITCODE -ne 0) { throw 'the test build did not publish; not starting' }
    Protect-RealRun {
        $lines = @(Get-RealLog).Count
        Start-NeapReal -Page firmware
        [void](Wait-Until { Get-RealLog $lines | Where-Object { $_ -match 'firmware edits:|no page with the tag firmware' } } 20)
        if (Get-RealLog $lines | Where-Object { $_ -match 'no page with the tag firmware' }) { throw 'the test build has no Firmware page; not starting' }
        Invoke-Sequence
        Get-Content (Join-Path $env:LOCALAPPDATA 'Neap\app.log') -Tail 80 | Where-Object { $_ -match 'firmware edits: (applying|restoring|section|Done|Failed|NotReadBack|Stopped)' } | ForEach-Object { Note "log: $_" }
    }
}
Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
