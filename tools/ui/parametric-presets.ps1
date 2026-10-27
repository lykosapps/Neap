<#
.SYNOPSIS
Checks the parametric equaliser's saved presets and a profile that holds a
parametric shape, on the published app in pretend mode.

.DESCRIPTION
Run through run.ps1. Part A saves a shape as a preset, edits it, saves over
it, chooses it again, renames it and deletes it. Part B saves a profile while
in Parametric, goes to the ten bands, and has the profile's Discard put the
Parametric shape back. Nothing here changes a real headset.

.PARAMETER Parts
Which parts to run: A presets, B a profile. Both by default.
#>
param([string]$Parts = 'AB')

. "$PSScriptRoot\Ui.ps1"

# A dialog or a popup is a window of its own, listed inside the main one or
# beside it, so one is searched before the main window's own controls.
function Find-Anywhere([string]$Name, $Type = [System.Windows.Automation.ControlType]::Button) {
    $both = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty $Name), (New-Condition ControlTypeProperty $Type))
    $windows = @($A::RootElement.FindAll($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id)))) +
        @($script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Window))))
    $others = $windows | Where-Object { [IntPtr]$_.Current.NativeWindowHandle -ne $script:Handle }
    foreach ($window in @($others) + @($script:Window)) {
        $found = $window.FindAll($Scope::Descendants, $both) | Where-Object { -not $_.Current.IsOffscreen } | Select-Object -First 1
        if ($found) { return $found }
    }
}

function Find-Item([string]$Name) { Get-Popups | Where-Object { $_.Current.Name -eq $Name } | Select-Object -First 1 }

function Choose-Shape([string]$Shape) {
    Open-Menu (Find-Named 'Start from')
    Wait-Until { $null -ne (Find-Item $Shape) } 4 | Out-Null
    Press (Find-Item $Shape)
}

function Test-Picker([string]$Shows) { Wait-Until { $null -ne (Find-Named "Parametric preset: $Shows") } 4 }

# Types a name into the name dialog that is open, and saves it.
function Save-Name([string]$Name) {
    Check (Wait-Until { $null -ne (Find-Anywhere 'Name' ([System.Windows.Automation.ControlType]::Edit)) } 5) 'it asks for a name'
    $field = Find-Anywhere 'Name' ([System.Windows.Automation.ControlType]::Edit)
    if (-not $field) { return }
    $field.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Name)
    Wait-Until { (Find-Anywhere 'Save').Current.IsEnabled } 3 | Out-Null
    Press (Find-Anywhere 'Save')
}

if ($Parts.Contains('A')) {
    Section 'saved parametric presets'
    Start-NeapPretend -Page equaliser -Flags @('--parametric', '--wired')
    try {
        Set-NeapSize 1400 1500 'Equaliser'
        Check (Wait-Until { $null -ne (Find-Named 'Equaliser curve') } 20) 'the editor is there'
        Check (Test-Picker 'No preset') 'it starts with no preset'
        Check (-not (Find-Named 'Rename').Current.IsEnabled) 'with nothing to rename'

        Choose-Shape 'Add bass'
        Press (Find-Named 'Save as new')
        Save-Name 'Bassy'
        Check (Test-Picker 'Bassy') 'saving names the preset'
        Check (-not (Find-Named 'Save over the preset').Current.IsEnabled) 'with nothing to save over yet'

        Choose-Shape 'Cut mud'
        Check (Test-Picker 'Bassy, edited') 'a change shows the preset as edited'
        Check ((Find-Named 'Save over the preset').Current.IsEnabled) 'and it can be saved over'

        Open-Menu (Find-Named 'Parametric preset: Bassy, edited')
        Check (Wait-Until { $null -ne (Find-Item 'Bassy') } 4) 'the picker lists it'
        Press (Find-Item 'Bassy')
        Check (Test-Picker 'Bassy') 'choosing it puts its shape back'
        Check ((Find-Named 'Undo').Current.IsEnabled) 'as a change Undo can take back'

        Press (Find-Named 'Rename')
        Save-Name 'Bass'
        Check (Test-Picker 'Bass') 'renaming shows the new name'

        Press (Find-Named 'Delete')
        Check (Wait-Until { $null -ne (Find-Anywhere 'Cancel') } 5) 'deleting asks first'
        Press (Find-Anywhere 'Delete')
        Check (Test-Picker 'No preset') 'deleting leaves no preset'
        Save-Shot 'parametric-presets' | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('B')) {
    Section 'a profile that holds a parametric shape'
    Start-NeapPretend -Page equaliser -Flags @('--parametric', '--wired')
    try {
        Set-NeapSize 1400 1500 'Equaliser'
        Wait-Until { $null -ne (Find-Named 'Equaliser curve') } 20 | Out-Null
        Choose-Shape 'Add bass'
        Press (Find-Named 'Save as new')
        Save-Name 'Bassy'

        Send-Pretend 'page profiles' | Out-Null
        Wait-Until { $null -ne (Find-Named 'New profile') } 10 | Out-Null
        Press (Find-All 'New profile' | Where-Object { $_.Current.ControlType.ProgrammaticName -match 'Button' } | Sort-Object { $_.Current.BoundingRectangle.Y } | Select-Object -Last 1)
        Check (Wait-Until { $null -ne (Find-Named 'Profile name') } 5) 'a new profile asks for a name'
        (Find-Named 'Profile name').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Bass night')
        Wait-Until { (Find-Named 'Save').Current.IsEnabled } 3 | Out-Null
        Press (Find-Named 'Save')
        Check (Wait-Until { (Get-Texts) -contains 'Bass night' } 5) 'the profile is saved'

        Send-Pretend 'page equaliser' | Out-Null
        Wait-Until { $null -ne (Find-Named 'Bands') } 10 | Out-Null
        Press (Find-Named 'Bands')
        Check (Wait-Until { $null -eq (Find-Named 'Parametric preset: Bassy') } 5) 'the ten bands are shown'

        Open-Menu (Find-Named 'Profile')
        Check (Wait-Until { $null -ne (Find-Anywhere 'Discard') } 5) 'the profile reads as edited and offers Discard'
        Press (Find-Anywhere 'Discard')
        Check (Wait-Until { $null -ne (Find-Named 'Parametric preset: Bassy') } 8) 'Discard puts Parametric back with the shape, named'
    }
    finally { Stop-NeapPretend }
}
