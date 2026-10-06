<#
.SYNOPSIS
Checks the Settings page on the published app in pretend mode: what is on it
and in what order, where the update banner sits above the profile selector,
how Neap's card lays out at a wide and a narrow window, and contrast in both
themes.

.DESCRIPTION
Run through run.ps1. The app is launched once for each theme, with a made-up
newer version on offer, and the window resized in place. A further launch
with nothing on offer checks the page when Neap is up to date.

.PARAMETER Parts
Which parts to run: A the page with an update on offer, B the page when Neap
is up to date. Both by default.
#>
param([string]$Parts = 'AB')

. "$PSScriptRoot\Ui.ps1"

$offer = 'Version 9.9.9 is available.'
$anchor = 'Check for updates automatically'

function Resize([int]$Width, [int]$Height) { Set-NeapSize $Width $Height $anchor }

function Get-Box($Element) { $Element.Current.BoundingRectangle }

# The text of this name if there is one, otherwise whatever has the name.
function Find-Anywhere([string]$Name) {
    $element = Find-Text $Name
    if (-not $element) { $element = Find-Named $Name }
    $element
}

if ($Parts.Contains('A')) {
    foreach ($theme in 'dark', 'light') {
        Section "the $theme theme, with an update on offer"
        Start-NeapPretend -Theme $theme -Page settings
        try {
            Wait-Until { (Find-All $offer).Count -ge 2 } 15 | Out-Null
            # Tall enough for the whole page to be on screen, so every line can be measured.
            Resize 1536 1000

            $banner = Find-All $offer | Sort-Object { $_.Current.BoundingRectangle.Y } | Select-Object -First 1
            $profile = Find-Named 'No profile'
            Check ($null -ne $banner -and $null -ne $profile) 'the banner and the profile selector are both there'
            if ($banner -and $profile) { Check ((Get-Box $banner).Y -lt (Get-Box $profile).Y) 'the update banner is above the profile selector' }

            $texts = @(Get-Texts)
            Check ($texts -contains 'Settings') 'the page is titled Settings'
            Check ($texts -contains 'Neap') "Neap's own name is on the page"
            Check (@($texts -match '^Version \d').Count -gt 0) 'its version is on the page'
            Check ($texts -contains 'Problems and other headsets') 'the troubleshooting section is named plainly'
            Check ($texts -notcontains 'Default profile') 'the profile list has left this page'
            Check ($texts -notcontains 'Diagnostics') 'the old name for the troubleshooting section is gone'
            Check (@($texts -match 'only been tested with the Stealth Pro II').Count -gt 0) 'it says what has been tested, beside the invitation to help'
            Check (@($texts -match 'Free software under the GNU GPL').Count -gt 0) 'the licence line is on the page'
            foreach ($name in 'Start when you sign in', $anchor, 'Help support another headset') { Check ($null -ne (Find-Named $name)) "$name is on the page" }

            # Order, top to bottom: Neap's card, the two switches, the troubleshooting section, the licence line.
            $order = 'Neap', $anchor, 'Start when you sign in', 'Problems and other headsets', 'Help support another headset'
            $ys = $order | ForEach-Object { (Get-Box (Find-Anywhere $_)).Y }
            Check (($ys -join ',') -eq (($ys | Sort-Object { [double]$_ }) -join ',')) "the page runs in the order: $($order -join ', ')"

            # Neap's card: the buttons beside the name when wide, under it when narrow.
            $name = Find-Text 'Neap'
            $act = Find-Lowest 'Update and restart'
            $notes = Find-Lowest "What's new"
            Check ((Get-Box $act).X -gt (Get-Box $name).X + 300 -and [Math]::Abs((Get-Box $act).Y - (Get-Box $name).Y) -lt 80) "wide: Neap's buttons sit beside its name"
            $bitmap = Save-Shot "settings-wide-$theme" -Top 700
            try {
                $status = Find-Text $offer
                Test-Contrast $bitmap $status 'the status line'
                foreach ($label in 'Start when you sign in', 'Problems and other headsets') { Test-Contrast $bitmap (Find-Text $label) $label }
                Test-Contrast $bitmap (Find-Text 'Free software under the GNU GPL, version 3 or later, with no warranty. Not affiliated with or endorsed by Turtle Beach.') 'the licence line'
            }
            finally { $bitmap.Dispose() }

            Resize 625 800
            $name = Find-Text 'Neap'; $act = Find-Lowest 'Update and restart'; $rect = Get-NeapRect
            Check ((Get-Box $act).Y -gt (Get-Box $name).Y + 40) "narrow: Neap's buttons sit under its name"
            foreach ($label in 'Update and restart', "What's new", $anchor, 'Start when you sign in') {
                $box = Get-Box (Find-Lowest $label)
                Check ($box.X -ge $rect.Left -and $box.Right -le $rect.Right) "narrow: $label is inside the window"
                Check ($box.Height -ge 24 -and $box.Width -ge 24) ("narrow: {0} is {1} by {2}, at least 24 by 24" -f $label, [int]$box.Width, [int]$box.Height)
            }
            Save-Shot "settings-narrow-$theme" -Top 700 | ForEach-Object { $_.Dispose() }
        }
        finally { Stop-NeapPretend }
    }
}

if ($Parts.Contains('B')) {
    Section 'up to date'
    Start-NeapPretend -Theme dark -Page settings -Flags @()
    try {
        Wait-Until { (Get-Texts) -contains 'You have the latest version.' } 15 | Out-Null
        $texts = @(Get-Texts)
        Check ($texts -contains 'You have the latest version.') 'it says Neap is up to date'
        Check ($null -eq (Find-Named 'Update and restart')) 'there is no Update and restart to press'
        Check ($null -eq (Find-Named "What's new")) 'there is no What''s new to read'
        Check ($null -ne (Find-Named 'Check now')) 'Check now is offered'
        Check ($null -eq (Find-All $offer | Select-Object -First 1)) 'no banner is showing'
        Save-Shot 'settings-uptodate' -Top 700 | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }
}

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
