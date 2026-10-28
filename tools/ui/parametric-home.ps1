<#
.SYNOPSIS
Checks that Home's equaliser preset picker and the mini panel's follow the
Parametric mode: they name and list the parametric presets, not the ten-band
ones, on the published app in pretend mode.

.DESCRIPTION
Run through run.ps1. In each theme it opens Home in Parametric with no preset
saved, checks the picker says so and that its link opens the Equaliser page,
saves two parametric presets there, and goes back to Home to check the picker
names the one in use, lists both and takes a choice. The mini panel's picker
is then checked the same way. Nothing here changes a real headset.
#>

. "$PSScriptRoot\Ui.ps1"

# A popup is a window of its own or a part of the one it opened from, so this
# searches every window of the practice run.
function Find-InPopup([string]$Name, $Type, [string]$Property = 'NameProperty') {
    $both = New-Object System.Windows.Automation.AndCondition((New-Condition $Property $Name), (New-Condition ControlTypeProperty $Type))
    foreach ($window in $A::RootElement.FindAll($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id)))) {
        $found = $window.FindAll($Scope::Descendants, $both) | Where-Object { -not $_.Current.IsOffscreen } | Select-Object -First 1
        if ($found) { return $found }
    }
}
function Find-Choice([string]$Name) { Find-InPopup $Name ([System.Windows.Automation.ControlType]::ListItem) }

# What the picker names, as it tells a screen reader.
function Get-Shown { (Find-Named 'Equaliser preset').Current.ItemStatus }
function Test-Shown([string]$Status) { Wait-Until { (Get-Shown) -eq $Status } 6 }

function Choose-Shape([string]$Shape) { Choose-Parametric $Shape }

# Saves the shape on screen as a new parametric preset.
function Save-As([string]$Name) {
    Press (Find-Named 'Save as new')
    $edit = [System.Windows.Automation.ControlType]::Edit
    Wait-Until { $null -ne (Find-InPopup 'Name' $edit) -or $null -ne (Find-Named 'Name') } 5 | Out-Null
    $field = Find-InPopup 'Name' $edit
    if (-not $field) { $field = Find-Named 'Name' }
    $field.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Name)
    $save = { $found = Find-InPopup 'Save' ([System.Windows.Automation.ControlType]::Button); if (-not $found) { $found = Find-Named 'Save' }; $found }
    Wait-Until { (& $save).Current.IsEnabled } 3 | Out-Null
    Press (& $save)
    Wait-Until { $null -ne (Find-Named "Parametric preset: $Name") } 5 | Out-Null
}

function Find-Panel {
    $A::RootElement.FindAll($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id))) |
        Where-Object { $_.Current.Name -eq 'Mini panel' } | Select-Object -First 1
}

foreach ($theme in 'dark', 'light') {
    Section "Home's equaliser preset in Parametric, $theme"
    Start-NeapPretend -Theme $theme -Page home -Flags @('--parametric', '--wired')
    try {
        $app = $script:Window
        $appHandle = $script:Handle
        Set-NeapSize 1400 1100 'Equaliser preset'
        Check (Wait-Until { $null -ne (Find-Named 'Equaliser preset') } 20) 'Home shows the equaliser preset picker'
        Check (Test-Shown 'No preset, parametric') 'with no parametric preset saved, it names none, as parametric'
        Check ((Find-Named 'Equaliser preset').Current.IsEnabled) 'and it can still be opened'
        Save-Shot "parametric-home-none-$theme" -Whole | ForEach-Object { $_.Dispose() }

        Open-Menu (Find-Named 'Equaliser preset')
        Check (Wait-Until { $null -ne (Find-InPopup 'No saved presets yet' ([System.Windows.Automation.ControlType]::Text)) } 4) 'its list says no preset is saved yet'
        Check ($null -eq (Find-Choice 'Bass Boost')) 'and offers no ten-band preset'
        # Home has a link of the same name under the picker, so the list's own is found by its id.
        $link = Find-InPopup 'PresetChoiceEqualiser' ([System.Windows.Automation.ControlType]::Button) 'AutomationIdProperty'
        Check ($null -eq $link -or $link.Current.Name -eq 'Equaliser settings') 'named as the link under the picker'
        Check ($null -ne $link) 'it offers the way to the equaliser'
        if ($link) { Press $link }
        Check (Wait-Until { $null -ne (Find-Named 'Equaliser curve') } 10) 'which opens the parametric equaliser'

        Choose-Shape 'Add bass'
        Save-As 'Bassy'
        Choose-Shape 'Cut mud'
        Save-As 'Clear'

        Send-Pretend 'page home' | Out-Null
        Check (Wait-Until { $null -ne (Find-Named 'Equaliser preset') } 10) 'Home opens again'
        Check (Test-Shown 'Clear, parametric') 'the picker names the parametric preset in use'
        # The page comes in over the one before it, so the picture waits for it to settle.
        Wait-Until { -not (Test-Present (Find-Named 'Equaliser curve')) } 3 | Out-Null
        Wait-Stable { (Get-Texts) -join ';' } | Out-Null
        Save-Shot "parametric-home-$theme" -Whole | ForEach-Object { $_.Dispose() }

        Open-Menu (Find-Named 'Equaliser preset')
        Check (Wait-Until { $null -ne (Find-Choice 'Bassy') -and $null -ne (Find-Choice 'Clear') } 4) 'its list offers the parametric presets'
        Check ($null -eq (Find-Choice 'Bass Boost')) 'and not the ten-band ones'
        Press (Find-Choice 'Bassy')
        Check (Test-Shown 'Bassy, parametric') 'choosing one names it'

        Section "the mini panel's equaliser preset in Parametric, $theme"
        Send-Pretend 'mini' | Out-Null
        Check (Wait-Until { $null -ne (Find-Panel) } 10) 'the mini panel opens'
        $panel = Find-Panel
        if ($panel) {
            $script:Window = $panel
            $script:Handle = [IntPtr]$panel.Current.NativeWindowHandle
            Wait-Stable { (Get-Texts) -join '|' } | Out-Null
            Check (Test-Shown 'Bassy, parametric') 'its picker names the parametric preset in use'
            Save-Shot "parametric-mini-$theme" -Whole | ForEach-Object { $_.Dispose() }
            Open-Menu (Find-Named 'Equaliser preset')
            Check (Wait-Until { $null -ne (Find-Choice 'Clear') } 4) 'its list offers the parametric presets'
            Press (Find-Choice 'Clear')
            Check (Test-Shown 'Clear, parametric') 'choosing one names it'
            Check ($null -ne (Find-Panel)) 'and the panel stays open'
            Press (Find-Named 'Open Neap')
            Wait-Until { $null -eq (Find-Panel) } 4 | Out-Null
        }

        $script:Window = $app; $script:Handle = $appHandle
        Check (Test-Shown 'Clear, parametric') "Home's picker follows the choice made in the mini panel"
    }
    finally { Stop-NeapPretend }
}

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
