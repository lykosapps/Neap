<#
.SYNOPSIS
Checks the mini panel on the published app in pretend mode: it opens as the
icon by the clock opens it, each tile's list offers its choices and takes one
with the panel left open, the next tile's list opens straight after, and Open
Neap brings the window back. The Superhuman Hearing tile is ticked on in
Settings, shown, switched on and given a sound; unticking a control in
Settings takes its tile out of the next panel.

.DESCRIPTION
Run through run.ps1. The panel is opened over the practice run's pipe, since
the icon is the system's and out of reach; everything after is UI Automation
on the panel itself, so the panel's own closing when it loses focus is in
play. A picture of it is saved in each theme.
#>

. "$PSScriptRoot\Ui.ps1"

# The practice run's mini panel, once it has opened.
function Find-Panel {
    $A::RootElement.FindAll($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id))) |
        Where-Object { $_.Current.Name -eq 'Mini panel' } | Select-Object -First 1
}
function Get-Choices { @(Get-Popups | Where-Object { -not $_.Current.IsOffscreen } | ForEach-Object { $_.Current.Name }) }
# A tick box on the Settings page, by the control's name.
function Find-Tick([string]$Name) {
    Search {
        $both = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty $Name),
            (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::CheckBox)))
        $script:Window.FindFirst($Scope::Descendants, $both)
    }
}
function Set-Tick([string]$Name, [bool]$On) {
    $toggle = (Find-Tick $Name).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $want = if ($On) { 'On' } else { 'Off' }
    if ([string]$toggle.Current.ToggleState -ne $want) { $toggle.Toggle() }
    Wait-Until { [string]$toggle.Current.ToggleState -eq $want } 4 | Out-Null
}
# Opens the panel and makes it the window the checks look in; false if it never opens.
function Open-Panel {
    Send-Pretend 'mini' | Out-Null
    if (-not (Wait-Until { $null -ne (Find-Panel) } 10)) { return $false }
    $panel = Find-Panel
    $script:Window = $panel
    $script:Handle = [IntPtr]$panel.Current.NativeWindowHandle
    Wait-Stable { (Get-Texts) -join '|' } | Out-Null
    $true
}
function Choose([string]$Word) { Press (Get-Popups | Where-Object { $_.Current.Name -eq $Word } | Select-Object -First 1) }

foreach ($theme in 'dark', 'light') {
    Section "the mini panel, $theme"
    Start-NeapPretend -Theme $theme -Page settings -Flags @()
    try {
        $app = $script:Window
        $appHandle = $script:Handle

        # Superhuman Hearing is left out until it is chosen, so nobody finds it added.
        Wait-Until { $null -ne (Find-Tick 'Superhuman Hearing') } 10 | Out-Null
        Check ($null -ne (Find-Tick 'Superhuman Hearing')) 'Settings offers a tick box for each control, Superhuman Hearing among them'
        Check ([string](Find-Tick 'Superhuman Hearing').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq 'Off') 'Superhuman Hearing starts unticked'
        Check ([string](Find-Tick 'Spatial sound').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq 'On') 'Spatial sound starts ticked'
        Set-Tick 'Superhuman Hearing' $true
        # The profile is shown before a choice only once one is saved, which a fresh practice run has not.
        Check ([string](Find-Tick 'Profile').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq 'Off') 'the profile starts unticked with none saved'
        Set-Tick 'Profile' $true

        Check (Open-Panel) 'the mini panel opens as the icon opens it'
        $panel = Find-Panel
        if (-not $panel) { continue }
        Check ((Get-Texts) -contains 'Microphone' -and (Get-Texts) -contains 'Noise' -and (Get-Texts) -contains 'Spatial') 'it shows the microphone, noise and spatial tiles'
        Check (Test-Present (Find-Named 'Superhuman Hearing')) 'the Superhuman Hearing tile is there once ticked'
        Check (Test-Present (Find-Named 'Profile')) 'the profile bar is there once ticked'
        Save-Shot "mini-$theme" -Whole | ForEach-Object { $_.Dispose() }

        # Its list offers Off and its sounds, as noise control's offers Off and its modes.
        Press (Find-Named 'Superhuman Hearing')
        Check (Wait-Until { (Find-Named 'Superhuman Hearing').Current.HelpText -ne 'Off' } 4) 'pressing the Superhuman Hearing tile turns it on'
        Press (Find-Named 'Superhuman Hearing sounds')
        $sounds = 'Legacy', 'Footsteps', 'Gunshots'
        Check (Wait-Until { (Compare-Object (Get-Choices) (@('Off') + $sounds) -SyncWindow 0).Count -eq 0 } 4) "its list offers Off, $($sounds -join ', ')"
        $pick = $sounds | Where-Object { $_ -ne (Find-Named 'Superhuman Hearing').Current.HelpText } | Select-Object -First 1
        Choose $pick
        Check (Wait-Until { (Find-Named 'Superhuman Hearing').Current.HelpText -eq $pick } 4) "choosing $pick shows it on the tile"
        Check ($null -ne (Find-Panel)) 'the panel stays open after choosing a sound'
        Save-Shot "mini-hearing-$theme" -Whole | ForEach-Object { $_.Dispose() }

        foreach ($tile in @(
                @{ List = 'Noise cancellation choices'; Words = 'Off', 'Cancelling', 'Transparency'; Pick = 'Transparency'; State = 'Noise cancellation' },
                @{ List = 'Spatial sound choices'; Words = 'Off', 'Windows Sonic', 'Dolby Atmos'; Pick = 'Windows Sonic'; State = 'Spatial sound' })) {
            Press (Find-Named $tile.List)
            Check (Wait-Until { (Compare-Object (Get-Choices) $tile.Words -SyncWindow 0).Count -eq 0 } 4) "$($tile.List) lists $($tile.Words -join ', ')"
            Choose $tile.Pick
            Check (Wait-Until { (Find-Named $tile.State).Current.HelpText -eq $tile.Pick } 4) "choosing $($tile.Pick) shows it on the tile"
            Check ($null -ne (Find-Panel)) "the panel stays open after choosing from $($tile.List)"
        }

        # A choice once left its list taking itself for open, and the next list's arrow did nothing.
        Press (Find-Named 'Noise cancellation choices')
        Choose 'Cancelling'
        Press (Find-Named 'Spatial sound choices')
        Check (Wait-Until { (Get-Choices).Count -eq 3 } 4) "straight after a choice, the next tile's list opens"
        Choose 'Off'

        Press (Find-Named 'Open Neap')
        Check (Wait-Until { $null -eq (Find-Panel) } 4) 'Open Neap closes the panel'
        $script:Window = $app; $script:Handle = $appHandle

        # The panel is drawn from the choice each time it opens. Each run starts from fresh
        # settings, so putting Superhuman Hearing back is unticking it, which the next panel shows.
        Open-Page 'Settings'
        Set-Tick 'Spatial sound' $false
        Set-Tick 'Superhuman Hearing' $false
        Check (Open-Panel) 'the panel opens again after the choice changes'
        Check ($null -eq (Find-Named 'Spatial sound') -or -not (Test-Present (Find-Named 'Spatial sound'))) 'unticking Spatial sound takes its tile out'
        Check (-not (Test-Present (Find-Named 'Superhuman Hearing'))) 'unticking Superhuman Hearing takes its tile out'
        Check ((Get-Texts) -contains 'Microphone' -and (Get-Texts) -contains 'Noise') 'the microphone and noise tiles stay'
        Save-Shot "mini-fewer-$theme" -Whole | ForEach-Object { $_.Dispose() }
        $script:Window = $app; $script:Handle = $appHandle
    }
    finally { Stop-NeapPretend }
}

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
