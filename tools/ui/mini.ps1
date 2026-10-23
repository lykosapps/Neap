<#
.SYNOPSIS
Checks the mini panel on the published app in pretend mode: it opens as the
icon by the clock opens it, each tile's list offers its choices and takes one
with the panel left open, the next tile's list opens straight after, and Open
Neap brings the window back.

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
function Choose([string]$Word) { Press (Get-Popups | Where-Object { $_.Current.Name -eq $Word } | Select-Object -First 1) }

foreach ($theme in 'dark', 'light') {
    Section "the mini panel, $theme"
    Start-NeapPretend -Theme $theme -Flags @()
    try {
        $app = $script:Window
        Send-Pretend 'mini' | Out-Null
        Check (Wait-Until { $null -ne (Find-Panel) } 10) 'the mini panel opens as the icon opens it'
        $panel = Find-Panel
        if (-not $panel) { continue }
        $script:Window = $panel
        $script:Handle = [IntPtr]$panel.Current.NativeWindowHandle
        Wait-Stable { (Get-Texts) -join '|' } | Out-Null
        Check ((Get-Texts) -contains 'Microphone' -and (Get-Texts) -contains 'Noise' -and (Get-Texts) -contains 'Spatial') 'it shows the microphone, noise and spatial tiles'
        Save-Shot "mini-$theme" -Whole | ForEach-Object { $_.Dispose() }

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
        $script:Window = $app
    }
    finally { Stop-NeapPretend }
}

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
