<#
.SYNOPSIS
Checks the update banner on the published app in pretend mode: its layout at
each width, its menus, putting it away, a simulated update, and focus.

.DESCRIPTION
Run through run.ps1. The app is launched once for each theme and the window
resized in place; only putting the banner away needs the app restarted, since
it must come back up knowing what was chosen.

.PARAMETER Parts
Which parts to run: A layout and contrast at each width, B the menus,
C putting it away across a restart, D a simulated update, E focus when the
layout changes, F an update whose new version will not open. All by default.
#>
param([string]$Parts = 'ABCDEF')

. "$PSScriptRoot\Ui.ps1"

$message = 'Version 9.9.9 is available.'
$buttons = "What's new", 'Update and restart', 'Not now', 'More options'

# Name, width and height in physical pixels, and what the banner should show
# there: wide, with its three controls, or compact, with the menu.
$sizes = @(
    @('desktop', 1920, 1000, 'wide'),
    @('laptop', 1536, 820, 'wide'),
    @('edge', 920, 700, 'wide'),
    @('mid', 900, 700, 'compact'),
    @('narrow', 625, 700, 'compact')
)

function Resize([int]$Width, [int]$Height) { Set-NeapSize $Width $Height $message }

function Get-Shown { $buttons | Where-Object { Test-Present (Find-Named $_) } }

function Test-Bar([string]$Name, [string]$Expect) {
    $rect = Get-NeapRect
    $shown = @(Get-Shown)
    if ($Expect -eq 'wide') {
        Check ($shown -contains "What's new" -and $shown -contains 'Update and restart' -and $shown -contains 'Not now' -and $shown -notcontains 'More options') "${Name}: What's new, Update and restart and Not now are shown, and not the more menu"
    }
    else {
        Check ($shown -contains 'Update and restart' -and $shown -contains 'More options' -and $shown -notcontains 'Not now' -and $shown -notcontains "What's new") "${Name}: Update and restart and the more menu are shown, and nothing else"
    }

    $messageBox = (Find-Named $message).Current.BoundingRectangle
    $centres = @([int]($messageBox.Y + $messageBox.Height / 2))
    foreach ($label in $shown) {
        $box = (Find-Named $label).Current.BoundingRectangle
        $centres += [int]($box.Y + $box.Height / 2)
        Check ($box.Height -ge 24 -and $box.Width -ge 24) ("{0}: {1} is {2} by {3}, at least 24 by 24" -f $Name, $label, [int]$box.Width, [int]$box.Height)
        Check ($box.Right -le $rect.Right) "${Name}: $label is inside the window"
    }
    $spread = ($centres | Measure-Object -Maximum).Maximum - ($centres | Measure-Object -Minimum).Minimum
    Check ($spread -le [int]$messageBox.Height + 4) "${Name}: the message and the controls share one row"
    $heading = Find-Named 'Pretend Headset'
    if ($heading) {
        $down = [int]($heading.Current.BoundingRectangle.Y - $rect.Top)
        Note ("{0}: the page starts {1} px down, {2}% of the window" -f $Name, $down, [int](100 * $down / ($rect.Bottom - $rect.Top)))
    }
}

function Test-Names {
    $shown = @(Get-Shown)
    $order = $shown | Sort-Object { (Find-Named $_).Current.BoundingRectangle.X }
    Check (($order -join ',') -eq (($buttons | Where-Object { $shown -contains $_ }) -join ',')) 'left to right is the order the controls are tabbed'
    foreach ($label in $shown) { Check ((Find-Named $label).Current.IsKeyboardFocusable) "$label can be reached from the keyboard" }
}

function Test-BarContrast([string]$Name) {
    $bitmap = Save-Shot $Name
    try {
        Test-Contrast $bitmap (Find-Named $message) 'the message'
        foreach ($label in "What's new", 'Update and restart', 'Not now') { Test-Contrast $bitmap (Find-Named $label) "$label on its fill" }
    }
    finally { $bitmap.Dispose() }
}

function Get-MenuNames { @(Get-Popups | ForEach-Object { $_.Current.Name }) }

if ($Parts.Contains('A') -or $Parts.Contains('B') -or $Parts.Contains('D') -or $Parts.Contains('E')) {
    foreach ($theme in 'dark', 'light') {
        Section "the $theme theme"
        Start-NeapPretend -Theme $theme
        try {
            Wait-Until { Test-Present (Find-Named $message) } 15 | Out-Null
            if ($Parts.Contains('A')) {
                foreach ($size in $sizes) {
                    Resize $size[1] $size[2]
                    Test-Bar "$($size[0]) $theme" $size[3]
                    if ($size[0] -eq 'laptop') { Test-Names; Test-BarContrast "laptop-$theme" }
                    else { Save-Shot "$($size[0])-$theme" | ForEach-Object { $_.Dispose() } }
                }
            }
            if ($theme -ne 'dark') { continue }

            if ($Parts.Contains('B')) {
                Section 'the menus'
                Resize 625 700
                $more = Find-Named 'More options'
                Open-Menu $more
                Wait-Until { @(Get-Popups).Count -ge 3 } 4 | Out-Null
                $names = Get-MenuNames
                Note ('narrow menu: ' + ($names -join ' | '))
                Check ($names -contains "What's new" -and $names -contains 'Remind me tomorrow' -and $names -contains 'Skip this version') 'the more menu holds What''s new, Remind me tomorrow and Skip this version'
                foreach ($item in Get-Popups) { Check ($item.Current.BoundingRectangle.Height -ge 24) ("menu item '{0}' is {1} high" -f $item.Current.Name, [int]$item.Current.BoundingRectangle.Height) }
                Save-Shot 'narrow-menu' | ForEach-Object { $_.Dispose() }
                Press (Get-Popups | Where-Object { $_.Current.Name -eq "What's new" } | Select-Object -First 1)
                Check (Wait-Until { (Get-Texts) -contains "What's new in Neap 9.9.9" } 5) 'the more menu opens the notes'
                Press (Find-Id 'CloseButton')
                Wait-Until { (Get-Texts) -notcontains "What's new in Neap 9.9.9" } 4 | Out-Null

                Resize 1536 820
                $notNow = Find-Named 'Not now'
                Open-Menu $notNow
                Wait-Until { @(Get-Popups).Count -ge 2 } 4 | Out-Null
                $names = Get-MenuNames
                Note ('wide menu: ' + ($names -join ' | '))
                Check ($names.Count -eq 2 -and $names -contains 'Remind me tomorrow' -and $names -contains 'Skip this version') 'Not now holds exactly Remind me tomorrow and Skip this version'
                Save-Shot 'wide-menu' | ForEach-Object { $_.Dispose() }
                Close-Menu $notNow
            }

            if ($Parts.Contains('E')) {
                Section 'focus when the layout changes'
                Resize 1536 820
                $notes = Find-Named "What's new"
                $notes.SetFocus()
                Check (Wait-Until { $notes.Current.HasKeyboardFocus } 2) "What's new has focus at a wide size"
                Resize 625 700
                $focused = $A::FocusedElement
                Note ("after narrowing, focus is on '{0}' in Neap: {1}" -f $focused.Current.Name, ($focused.Current.ProcessId -eq $script:Proc.Id))
                Check ($focused.Current.ProcessId -eq $script:Proc.Id -and $focused.Current.Name -ne '') 'focus stays inside the app, on something named'
            }

            if ($Parts.Contains('D')) {
                Section 'a simulated update'
                foreach ($size in @(@(1536, 820), @(625, 700))) {
                    Resize $size[0] $size[1]
                    Write-Host ("-- width {0}" -f $size[0])
                    $act = Find-Named 'Update and restart'
                    $act.SetFocus()
                    Press $act
                    $clock = [Diagnostics.Stopwatch]::StartNew(); $last = ''; $sawProgress = $false; $sawInstalling = $false; $offeredDuring = @()
                    while ($clock.Elapsed.TotalSeconds -lt 20) {
                        # Read the sentence, the controls, then the sentence again: the update can end
                        # between the readings, and only a reading with the update running on both
                        # sides says anything about the controls.
                        $before = @(Get-Texts) -match 'Downloading|Installing' | Select-Object -First 1
                        $now = Find-Named 'Update and restart'
                        $enabled = $now.Current.IsEnabled
                        $others = @(Get-Shown | Where-Object { $_ -ne 'Update and restart' })
                        $bar = $script:Window.FindFirst($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::ProgressBar)))
                        $say = @(Get-Texts) -match 'Downloading|Installing' | Select-Object -First 1
                        if ($before -match 'Installing' -or $say -match 'Installing') { $sawInstalling = $true }
                        if ($before -and $say) {
                            if ($bar) { $sawProgress = $true }
                            $offeredDuring += $others
                            $line = "act enabled {0}; text '{1}'" -f $enabled, ($say -replace '\d+%', 'N%')
                            if ($line -ne $last) { Note $line; $last = $line }
                            if ($enabled) { Check $false 'Update and restart is off while an update runs' }
                        }
                        if ($sawInstalling -and -not $say -and $enabled) { break }
                        Start-Sleep -Milliseconds 250
                    }
                    Check $sawProgress 'a progress bar is shown while an update runs'
                    Check ($offeredDuring.Count -eq 0) 'nothing else is offered while an update runs'
                    Check ((Find-Named 'Update and restart').Current.IsEnabled) 'afterwards Update and restart is on again'
                }
            }
        }
        finally { Stop-NeapPretend }
    }
}

if ($Parts.Contains('C')) {
    Section 'putting it away, from the menu, across a restart'
    foreach ($case in @(@('Remind me tomorrow', 1536, 820, 'Not now'), @('Skip this version', 625, 700, 'More options'))) {
        Start-NeapPretend
        try {
            Wait-Until { Test-Present (Find-Named $message) } 15 | Out-Null
            Resize $case[1] $case[2]
            Open-Menu (Find-Named $case[3])
            Wait-Until { @(Get-Popups).Count -ge 2 } 4 | Out-Null
            Press (Get-Popups | Where-Object { $_.Current.Name -eq $case[0] } | Select-Object -First 1)
            Check (Wait-Until { (Get-Texts) -notcontains $message } 5) "$($case[0]) takes the banner away"
        }
        finally { Stop-NeapPretend }
        Start-NeapPretend -KeepSettings
        try {
            Start-Sleep -Milliseconds 1500
            Check ((Get-Texts) -notcontains $message) "$($case[0]): the banner is still away after a restart"
        }
        finally { Stop-NeapPretend }
    }
}

if ($Parts.Contains('F')) {
    Section 'an update whose new version will not open'
    Start-NeapPretend -Flags @('--update', '--update-fails')
    try {
        Wait-Until { Test-Present (Find-Named $message) } 15 | Out-Null
        Press (Find-Named 'Update and restart')

        # The simulated update runs about eight seconds, the new version
        # exits at once, and the old one then says so.
        $title = "Neap didn't open after updating"
        Check (Wait-Until { (Get-Texts) -contains $title } 30) 'the old version says the new one did not open'
        $texts = @(Get-Texts)
        Check (@($texts -match "^Version 9\.9\.9 was installed but wouldn't open").Count -gt 0) 'it says which version, and what to do'
        Check ($null -ne (Find-Named 'Download')) 'it offers the download'
        Check ($null -ne (Find-Id 'CloseButton')) 'it offers Close'
        Check (-not (Test-Present (Find-Named $message))) 'the banner is not left showing behind it'
        $bitmap = Save-Shot 'did-not-open' -Top 500
        try { Test-Contrast $bitmap (Find-Text $title) 'the dialog title' }
        finally { $bitmap.Dispose() }

        Press (Find-Id 'CloseButton')
        Check ($script:Proc.WaitForExit(8000)) 'Close ends the old version'
    }
    finally { Stop-NeapPretend }
}

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
