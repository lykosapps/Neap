<#
.SYNOPSIS
Looks at the true parametric equaliser on the Equaliser page, in pretend mode:
that the curve, the dials and the table of ten are there with names, takes
pictures in both themes, imports filters from pasted text, and fits bands
with Auto-EQ.

.DESCRIPTION
Run through run.ps1. A practice run asked for Parametric opens the Stealth Pro II's
game equaliser in that mode with parametric turned on, and on its own cable.
Nothing here changes a real headset.

.PARAMETER Parts
Which parts to run: A dark, B light, C import filters in both themes, D Auto-EQ in
dark, E the test tone open and the curves' lists, in both themes. All
by default.
#>
param([string]$Parts = 'ABCDE')

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

# A frequency as shown, such as "105 Hz" or "1.2 kHz", in hertz.
function Get-Hertz($Shown) {
    $text = @($Shown)[-1]
    if ($text -match '([\d.,]+)\s*(k?)Hz') { [double]($Matches[1] -replace ',', '.') * $(if ($Matches[2]) { 1000 } else { 1 }) } else { 0 }
}

$importItem = "Import preset$([char]0x2026)"
$sample = @(
    'Preamp: -6.2 dB'
    'Filter 1: ON PK Fc 105 Hz Gain -2.5 dB Q 0.7'
    'Filter 2: ON HSC Fc 8000 Hz Gain 3 dB Q 0.7'
    'Filter 3: ON AP Fc 300 Hz Gain 0 dB Q 1'
) -join "`r`n"

foreach ($part in @(@('A', 'dark'), @('B', 'light'))) {
    if (-not $Parts.Contains($part[0]) -and -not $Parts.Contains('C') -and -not $Parts.Contains('E') -and -not ($Parts.Contains('D') -and $part[0] -eq 'A')) { continue }
    $theme = $part[1]
    Start-NeapPretend -Theme $theme -Page equaliser -Flags @('--parametric', '--wired')
    try {
        Set-NeapSize 1400 1500 'Equaliser'
        Wait-Until { $null -ne (Find-Named 'Equaliser curve') } 20 | Out-Null

        if ($Parts.Contains($part[0])) {
            Section "the parametric equaliser in $theme"
            Check ($null -ne (Find-Named 'Equaliser curve')) 'the curve is there, with a name'
            Check ($null -ne (Find-Named 'Compare')) 'it offers Compare, to hear it flat'
            Check ($null -ne (Find-Named 'Test tone')) 'the test tone is there, folded'
            Check ((Get-Texts) -contains 'No bands yet. Double-click the curve or press Add band.') 'it starts with no bands'
            Check ($null -eq (Find-Named 'Band 1 frequency')) 'and no band to set'
            Save-Shot "parametric-empty-$theme" -Whole | ForEach-Object { $_.Dispose() }

            # Ten bands added one at a time, each in the widest gap: the numbers run with the frequency.
            foreach ($n in 1..10) { Press (Find-Named 'Add band'); Start-Sleep -Milliseconds 300 }
            Check (Wait-Until { $null -ne (Find-Named 'Band 10 gain') } 4) 'Add band adds bands, up to the tenth'
            Check (-not (Find-Named 'Add band').Current.IsEnabled) 'and no more than ten'
            $hertz = @(1..10 | ForEach-Object { Get-Hertz (Get-FrequencyShown $_) })
            Check ((($hertz | Sort-Object) -join ',') -eq ($hertz -join ',')) "the bands run from low to high: $($hertz -join ', ')"
            Press (Find-Named 'Remove band 4')
            Check (Wait-Until { $null -eq (Find-Named 'Band 10 gain') } 4) 'removing a band takes its row away'
            Check ((Find-Named 'Add band').Current.IsEnabled) 'and Add band is back'
            Save-Shot "parametric-$theme" -Whole | ForEach-Object { $_.Dispose() }
        }

        if ($Parts.Contains('C')) {
            Section "importing filters in $theme"
            Open-Menu (Find-Named 'More preset actions')
            Start-Sleep -Milliseconds 400
            Check ($(try { Send-Pretend "press $importItem" | Out-Null; $true } catch { $false })) 'the preset menu offers Import preset'
            Check (Wait-Until { $null -ne (Find-Dialog 'Import preset') } 5) 'it opens the import dialog'
            $dialog = Find-Dialog 'Import preset'
            if (-not $dialog) { continue }
            $import = $dialog.FindFirst($Scope::Descendants, (New-Condition AutomationIdProperty 'PrimaryButton'))
            Check (-not $import.Current.IsEnabled) 'Import is off until there are filters'
            $field = $dialog.FindFirst($Scope::Descendants, (New-Condition NameProperty 'Filters'))
            Check ($null -ne $field) 'the text box has a name'
            Check ($null -ne $dialog.FindFirst($Scope::Descendants, (New-Condition NameProperty "Open file$([char]0x2026)"))) 'it offers to open a file'
            $field.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($sample)
            $said = { @($dialog.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Text))) | ForEach-Object { $_.Current.Name }) }
            Check (Wait-Until { (& $said) -contains 'Sets 2 bands and turns the rest off.' } 4) 'it says how many bands it sets'
            Check ((& $said) -contains 'The preamp of -6.2 dB is not used. Set it with Pre-gain.') 'it says the preamp is not used'
            Check ((& $said) -contains 'Filter 3: ON AP Fc 300 Hz Gain 0 dB Q 1: the headset has no AP filter.') 'it lists the line it does not use, and why'
            Check ($import.Current.IsEnabled) 'Import is on once a filter fits'
            Save-DialogShot $dialog "parametric-import-$theme"
            Press $import
            Check (Wait-Until { $null -eq (Find-Dialog 'Import preset') } 5) 'Import closes the dialog'
            Check (Wait-Until { (Get-FrequencyShown 1) -contains '105 Hz' } 4) 'the table shows band 1 at the imported 105 Hz'
            Check ((Get-FrequencyShown 2) -contains '8 kHz') 'and band 2 at the imported 8 kHz'
            Check ($null -eq (Find-Named 'Band 3 gain')) 'the bands past the import are not shown'
            Check ((Find-Named 'Undo').Current.IsEnabled) 'the import can be undone'
            Save-Shot "parametric-imported-$theme" -Whole | ForEach-Object { $_.Dispose() }
        }

        if ($Parts.Contains('D') -and $theme -eq 'dark') {
            Section 'Auto-EQ'
            Check ((Get-BoxChoice (Find-Box 'Measured')) -like 'Stealth Pro II, ANC o*, B&K 5128') 'the key chooses the built-in measurement'
            Check ((Get-BoxChoice (Find-Box 'Target')) -eq '5128 DF + Tilt') 'and the built-in target, by its name'
            Open-Menu (Find-Box 'Target')
            Check (Wait-Until { $null -ne (Find-ListRow 'None') } 4) 'the target list offers None'
            Press (Find-ListRow 'None')
            Check (Wait-Until { (Get-BoxChoice (Find-Box 'Target')) -eq 'None' } 3) 'choosing None says so on its line in the key'
            $autoEq = Find-Named 'Auto-EQ'
            Check ($autoEq -and -not $autoEq.Current.IsEnabled) 'Auto-EQ is off with the target hidden'
            Open-Menu (Find-Box 'Target')
            Wait-Until { $null -ne (Find-ListRow '5128 DF + Tilt') } 4 | Out-Null
            Press (Find-ListRow '5128 DF + Tilt')
            Check (Wait-Until { (Find-Named 'Auto-EQ').Current.IsEnabled } 3) 'and on again with both shown'
            Press (Find-Named 'Auto-EQ')
            Check (Wait-Until { $null -ne (Find-Named 'Band 4 gain') } 10) 'Auto-EQ sets bands'
            Check ((Get-Texts) -contains 'With your changes') 'and the measurement with the bands added'
            $hertz = @(1..10 | Where-Object { Find-Named "Band $_ gain" } | ForEach-Object { Get-Hertz (Get-FrequencyShown $_) })
            Check ((($hertz | Sort-Object) -join ',') -eq ($hertz -join ',')) "in order of frequency: $($hertz -join ', ')"
            Check (@($hertz | Where-Object { $_ -gt 10000 }).Count -eq 0) 'none above 10 kHz'
            Check ((Find-Named 'Undo').Current.IsEnabled) 'Auto-EQ can be undone'
            foreach ($row in 'None', 'Stealth Pro II, ANC on, B&K 5128', 'Stealth Pro II, ANC off, B&K 5128') {
                Open-Menu (Find-Box 'Measured')
                Wait-Until { $null -ne (Find-ListRow $row) } 4 | Out-Null
                Press (Find-ListRow $row)
                Check (Wait-Until { (Get-BoxChoice (Find-Box 'Measured')) -eq $row } 3) "the measured list chooses $row"
            }
            Check ($null -ne (Find-Named 'Pre-gain')) 'the page shows the pre-gain'
            Check (@(Find-All 'Pre-gain' | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Slider }).Count -eq 1) 'as a slider'
            $warning = 'Boosts this large are squashed by the headset at this pre-gain.'
            if ((Get-Texts) -contains $warning) {
                $before = @(Get-Texts | Where-Object { $_ -match '^[+-]\d+ dB$' })[0]
                Press (Find-Named 'Lower pre-gain')
                Check (Wait-Until { (Get-Texts) -notcontains $warning } 6) 'Lower pre-gain clears the warning'
                $after = @(Get-Texts | Where-Object { $_ -match '^[+-]\d+ dB$' })[0]
                Check ($after -ne $before) "and the pre-gain shows the new value: $before to $after"
            }
            else { Note 'the boost warning: not shown at this pre-gain' }
            Save-Shot 'parametric-autoeq-dark' -Whole | ForEach-Object { $_.Dispose() }
        }

        if ($Parts.Contains('E')) {
            Section "the open states in $theme, for a person to look over"
            $tone = Find-Named 'Test tone'
            Open-Menu $tone
            Check (Wait-Until { $null -ne (Find-Named 'Test tone frequency') } 4) 'the test tone opens'
            Save-Shot "parametric-tone-$theme" -Whole | ForEach-Object { $_.Dispose() }
            Close-Menu $tone
            foreach ($curve in @(@('Measured', 'measurement'), @('Target', 'target'))) {
                $face = Find-Box $curve[0]
                $builtIn = Get-BoxChoice $face
                Open-Menu $face
                Start-Sleep -Milliseconds 400
                Save-ListShot "parametric-list-$($curve[1])-$theme"
                Save-Shot "parametric-open-$($curve[1])-$theme" -Whole | ForEach-Object { $_.Dispose() }
                Check (Wait-Until { $null -ne (Find-ListRow $builtIn) } 4) "the $($curve[1]) list has the built-in one, $builtIn"
                $row = Find-ListRow $builtIn
                Check ($row -and $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected) 'selected, as it is in use'
                Check ($null -ne (Find-ListRow "Load $($curve[1])$([char]0x2026)")) "with Load $($curve[1]) as its last row"
                Check ($null -ne (Find-ListRow 'None')) 'and None, to hide it'
                Close-Menu $face
                Wait-Until { $null -eq (Find-ListRow $builtIn) } 3 | Out-Null
            }
        }
    }
    finally { Stop-NeapPretend }
}
