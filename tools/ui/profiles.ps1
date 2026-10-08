<#
.SYNOPSIS
Checks the Profiles page on the published app in pretend mode: that it has its
place in the rail, what it says with none saved, saving one from the page, what
a saved profile offers, deleting one, and how it fits a narrow window.

.DESCRIPTION
Run through run.ps1. The app is launched once for each theme on the Profiles
page, with the pretend headset answering, and the window resized in place.

.PARAMETER Parts
Which parts to run: A the rail, B the page with none saved and saving one,
C a saved profile, D a narrow window, E when an app switches its profile.
All by default.
#>
param([string]$Parts = 'ABCDE')

. "$PSScriptRoot\Ui.ps1"

$anchor = 'New profile'

function Resize([int]$Width, [int]$Height) { Set-NeapSize $Width $Height 'Profiles' }

function Get-Box($Element) { $Element.Current.BoundingRectangle }

# An item of a list that is open, which sits in a window of its own.
function Find-Item([string]$Name) {
    foreach ($window in $A::RootElement.FindAll($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id)))) {
        $item = $window.FindFirst($Scope::Descendants, (New-Condition NameProperty $Name))
        if ($item) { return $item }
    }
}

# The New profile button on the page, not the one in the profile menu.
function Find-NewButton {
    Find-All 'New profile' | Where-Object { $_.Current.ControlType.ProgrammaticName -match 'Button' } | Sort-Object { $_.Current.BoundingRectangle.Y } | Select-Object -Last 1
}

foreach ($theme in 'dark', 'light') {
    Section "the $theme theme"
    Start-NeapPretend -Theme $theme -Page profiles -Flags @()
    try {
        Wait-Until { (Get-Texts) -contains 'Profiles' } 15 | Out-Null
        Resize 1536 820

        if ($Parts.Contains('A') -and $theme -eq 'dark') {
            Section 'the rail'
            $rail = 'Home', 'Audio', 'Microphone', 'Controls', 'Profiles', 'Device', 'Settings'
            $items = foreach ($name in $rail) {
                Find-All $name | Where-Object { $_.GetSupportedPatterns() -contains [System.Windows.Automation.SelectionItemPattern]::Pattern } | Select-Object -First 1
            }
            Check (@($items | Where-Object { $_ }).Count -eq $rail.Count) 'every place is in the rail'
            $ys = @($items | ForEach-Object { if ($_) { $_.Current.BoundingRectangle.Y } })
            Check (($ys -join ',') -eq (($ys | Sort-Object { [double]$_ }) -join ',')) "the rail runs in the order: $($rail -join ', ')"
            $profilesItem = $items[4]
            Check ($profilesItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected) 'Profiles is the place that is selected'
        }

        if ($Parts.Contains('B')) {
            Section "none saved ($theme)"
            $texts = @(Get-Texts)
            Check ($texts -contains 'Profiles') 'the page is titled Profiles'
            Check (@($texts -match 'No profiles yet').Count -gt 0) 'it says what to do when none are saved'
            Check (@($texts -match 'Switch profiles from the bar at the top of the window').Count -gt 0) 'it says where profiles are switched'
            Check ($texts -notcontains 'Default profile') 'there is no default to choose with none saved'
            $new = Find-NewButton
            Check ($null -ne $new) 'a New profile button is on the page'
            $bitmap = Save-Shot "profiles-empty-$theme" -Top 500
            try {
                if ($new) { Test-Contrast $bitmap (Find-Text 'No profiles yet. Set the headset up the way you want, then choose New profile.') 'the empty message' }
            }
            finally { $bitmap.Dispose() }

            if ($new) {
                Check (Wait-Until { (Find-NewButton).Current.IsEnabled } 15) 'New profile is on once the headset has answered'
                Press (Find-NewButton)
                Check (Wait-Until { $null -ne (Find-Named 'Profile name') } 5) 'it asks for a name'
                $field = Find-Named 'Profile name'
                $field.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Game night')
                Wait-Until { (Find-Named 'Save').Current.IsEnabled } 3 | Out-Null
                Press (Find-Named 'Save')
                Check (Wait-Until { (Get-Texts) -contains 'Game night' } 6) 'the new profile is in the list'
                Check (Wait-Until { @(Get-Texts | Where-Object { $_ -match 'No profiles yet' }).Count -eq 0 } 3) 'the empty message goes'
            }
        }

        if ($Parts.Contains('C') -and (Find-Named 'Game night')) {
            Section "a saved profile ($theme)"
            Check ($null -ne (Find-Named 'Default profile')) 'the default profile choice has appeared'
            foreach ($label in 'Rename Game night', 'Delete Game night') {
                $button = Find-Named $label
                Check ($null -ne $button) "$label is a named button"
                if ($button) { Check ($button.Current.IsKeyboardFocusable -and (Get-Box $button).Height -ge 24 -and (Get-Box $button).Width -ge 24) "$label is keyboard reachable and at least 24 by 24" }
            }
            $apps = @(Find-All 'Apps for Game night')
            Check ($apps.Count -ge 1) 'the apps button is named for the profile'
            $bitmap = Save-Shot "profiles-one-$theme" -Top 500
            $bitmap.Dispose()

            if ($theme -eq 'dark') {
                Press (Find-Named 'Delete Game night')
                Check (Wait-Until { $null -ne (Find-Named 'Keep Game night') } 4) 'deleting asks first, with Delete and Keep'
                Press (Find-Named 'Keep Game night')
                Check (Wait-Until { $null -ne (Find-Named 'Rename Game night') } 4) 'Keep puts the row back'
            }
        }

        if ($Parts.Contains('E') -and $theme -eq 'dark' -and (Find-Named 'Game night')) {
            Section 'when an app switches its profile'
            Press (@(Find-All 'Apps for Game night')[0])
            Check (Wait-Until { $null -ne (Find-Named 'Pretend Chat') } 5) 'the apps dialog lists Pretend Chat'
            $when = 'When Pretend Chat switches'
            Check ($null -eq (Find-Named $when)) 'there is no choice of when until the app is checked'
            Press (Find-Named 'Pretend Chat')
            Check (Wait-Until { $null -ne (Find-Named $when) } 4) 'checking Pretend Chat offers a choice of when it switches'
            $choice = Find-Named $when
            if ($choice) {
                $chosen = { @($choice.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection() | ForEach-Object { $_.Current.Name }) }
                Check ((& $chosen) -contains "While it's open") 'it switches while it is open until told otherwise'
                Check ($choice.Current.IsKeyboardFocusable -and (Get-Box $choice).Height -ge 24) 'the choice is keyboard reachable and at least 24 high'
                Open-Menu $choice
                Check (Wait-Until { $null -ne (Find-Item "While it's using the microphone") } 4) 'the choice offers the microphone'
                $item = Find-Item "While it's using the microphone"
                if ($item) { $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
                Close-Menu $choice
                Check (Wait-Until { (& $chosen) -contains "While it's using the microphone" } 3) 'the microphone can be chosen'
            }
            Press (Find-Named 'OK')
            # The row's list of apps is not reachable by UI Automation, so it is checked by eye.
            Check (Wait-Until { $null -eq (Find-Named $when) } 3) 'OK closes the apps dialog'
            Save-Shot 'profiles-row-microphone' -Top 500 | ForEach-Object { $_.Dispose() }
            Press (@(Find-All 'Apps for Game night')[0])
            Check (Wait-Until { $null -ne (Find-Named $when) } 5) 'opened again, the choice is there'
            if (Find-Named $when) {
                $kept = @((Find-Named $when).GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection() | ForEach-Object { $_.Current.Name })
                Check ($kept -contains "While it's using the microphone") 'opened again, it still says the microphone'
            }
            Press (Find-Named 'Pretend Chat')
            Check (Wait-Until { $null -eq (Find-Named $when) } 3) 'unchecking Pretend Chat takes the choice away'
            Press (Find-Named 'OK')
            Check (Wait-Until { $null -eq (Find-Named 'Pretend Chat') } 3) 'OK closes the apps dialog again'
        }

        if ($Parts.Contains('D') -and $theme -eq 'dark' -and (Find-Named 'Game night')) {
            Section 'a narrow window'
            Resize 625 800
            $rect = Get-NeapRect
            foreach ($label in 'Rename Game night', 'Delete Game night', 'Default profile') {
                $box = Get-Box (Find-Named $label)
                Check ($box.X -ge $rect.Left -and $box.Right -le $rect.Right) "$label is inside the window"
            }
            $newBox = Get-Box (Find-NewButton)
            Check ($newBox.Right -le $rect.Right) 'New profile is inside the window'
            Save-Shot 'profiles-narrow' -Top 600 | ForEach-Object { $_.Dispose() }
        }
    }
    finally { Stop-NeapPretend }
}

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
