<#
.SYNOPSIS
Looks at the true parametric equaliser on the Equaliser page, in pretend mode:
that the curve, the dials and the table of ten are there with names, takes
pictures in both themes, and imports filters from pasted text.

.DESCRIPTION
Run through run.ps1. A practice run asked for Parametric opens the Stealth Pro II's
game equaliser in that mode with parametric turned on, and on its own cable.
Nothing here changes a real headset.

.PARAMETER Parts
Which parts to run: A dark, B light, C import filters in both themes. All by default.
#>
param([string]$Parts = 'ABC')

. "$PSScriptRoot\Ui.ps1"

# The dialog is a window of its own that UI Automation lists inside the main one.
function Find-Dialog([string]$Heading) {
    $title = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty $Heading), (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Text)))
    @($script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Window)))) |
        Where-Object { [IntPtr]$_.Current.NativeWindowHandle -ne $script:Handle -and $null -ne $_.FindFirst($Scope::Descendants, $title) } | Select-Object -First 1
}

# A picture of the dialog alone, taken the way the main window's is.
function Save-DialogShot($Dialog, [string]$Name) {
    $main = $script:Handle
    $script:Handle = [IntPtr]$Dialog.Current.NativeWindowHandle
    try { Save-Shot $Name -Whole | ForEach-Object { $_.Dispose() } }
    finally { $script:Handle = $main }
}

# What the band's frequency field in the table shows.
function Get-FrequencyShown([int]$Band) {
    $value = $null
    @(Find-All "Band $Band frequency" | ForEach-Object {
        if ($_.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$value)) { $value.Current.Value }
    })
}

$importItem = "Import filters$([char]0x2026)"
$sample = @(
    'Preamp: -6.2 dB'
    'Filter 1: ON PK Fc 105 Hz Gain -2.5 dB Q 0.7'
    'Filter 2: ON HSC Fc 8000 Hz Gain 3 dB Q 0.7'
    'Filter 3: ON AP Fc 300 Hz Gain 0 dB Q 1'
) -join "`r`n"

foreach ($part in @(@('A', 'dark'), @('B', 'light'))) {
    if (-not $Parts.Contains($part[0]) -and -not $Parts.Contains('C')) { continue }
    $theme = $part[1]
    Start-NeapPretend -Theme $theme -Page equaliser -Flags @('--parametric', '--wired')
    try {
        Set-NeapSize 1400 1500 'Equaliser'
        Wait-Until { $null -ne (Find-Named 'Equaliser curve') } 20 | Out-Null

        if ($Parts.Contains($part[0])) {
            Section "the parametric equaliser in $theme"
            Check ($null -ne (Find-Named 'Equaliser curve')) 'the curve is there, with a name'
            Check ($null -ne (Find-Named 'Band 1 frequency')) 'the chosen band has a frequency to set'
            Check ($null -ne (Find-Named 'Band 10 gain')) 'the table reaches the tenth band'
            Check ($null -ne (Find-Named 'Compare')) 'it offers Compare, to hear it flat'
            Check ($null -ne (Find-Named 'Test tone')) 'the test tone is there, folded'
            Save-Shot "parametric-$theme" -Whole | ForEach-Object { $_.Dispose() }
        }

        if ($Parts.Contains('C')) {
            Section "importing filters in $theme"
            Press (Find-ParametricPicker)
            Check (Wait-Until { $null -ne (Find-ListRow $importItem) } 4) 'the preset picker offers Import filters'
            Press (Find-ListRow $importItem)
            Check (Wait-Until { $null -ne (Find-Dialog 'Import filters') } 5) 'it opens the import dialog'
            $dialog = Find-Dialog 'Import filters'
            if (-not $dialog) { continue }
            $import = $dialog.FindFirst($Scope::Descendants, (New-Condition AutomationIdProperty 'PrimaryButton'))
            Check (-not $import.Current.IsEnabled) 'Import is off until there are filters'
            $field = $dialog.FindFirst($Scope::Descendants, (New-Condition NameProperty 'Filters'))
            Check ($null -ne $field) 'the text box has a name'
            Check ($null -ne $dialog.FindFirst($Scope::Descendants, (New-Condition NameProperty "Open file$([char]0x2026)"))) 'it offers to open a file'
            $field.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($sample)
            $said = { @($dialog.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Text))) | ForEach-Object { $_.Current.Name }) }
            Check (Wait-Until { (& $said) -contains 'Sets 2 bands and turns the rest off.' } 4) 'it says how many bands it sets'
            Check ((& $said) -contains 'The preamp of -6.2 dB is not used. Set the level with Loudness boost.') 'it says the preamp is not used'
            Check ((& $said) -contains 'Filter 3: ON AP Fc 300 Hz Gain 0 dB Q 1: the headset has no AP filter.') 'it lists the line it does not use, and why'
            Check ($import.Current.IsEnabled) 'Import is on once a filter fits'
            Save-DialogShot $dialog "parametric-import-$theme"
            Press $import
            Check (Wait-Until { $null -eq (Find-Dialog 'Import filters') } 5) 'Import closes the dialog'
            Check (Wait-Until { (Get-FrequencyShown 1) -contains '105 Hz' } 4) 'the table shows band 1 at the imported 105 Hz'
            Check ((Get-FrequencyShown 2) -contains '8 kHz') 'and band 2 at the imported 8 kHz'
            $inUse = $null
            $third = Find-Named 'Band 3 in use'
            Check ($third -and $third.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$inUse) -and $inUse.Current.ToggleState -eq 'Off') 'the bands past the import are not in use'
            Check ((Find-Named 'Undo').Current.IsEnabled) 'the import can be undone'
            Save-Shot "parametric-imported-$theme" -Whole | ForEach-Object { $_.Dispose() }
        }
    }
    finally { Stop-NeapPretend }
}
