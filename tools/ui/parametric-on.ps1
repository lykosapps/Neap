<#
.SYNOPSIS
Checks turning the parametric equaliser on and off from the Equaliser page, on
the published app in pretend mode.

.DESCRIPTION
Run through run.ps1. Part A chooses Parametric through the Charging Dock, which
asks first and then opens the editor; part B declines, which goes back to the
ten bands; part C turns it off from the editor; part D is a headset Swarm II
has reset, turned on again from the page; part E is a battery too low to risk
the write; part F is an Atlas Air, which has no Parametric choice; part G
makes a shape, goes to the ten bands and back, and finds the shape as it was.

.PARAMETER Parts
Which parts to run. All by default.
#>
param([string]$Parts = 'ABCDEFG')

. "$PSScriptRoot\Ui.ps1"

# The dialog is a window of its own that UI Automation lists inside the main one.
function Find-Dialog([string]$Heading) {
    $title = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty $Heading), (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Text)))
    @($script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Window)))) |
        Where-Object { [IntPtr]$_.Current.NativeWindowHandle -ne $script:Handle -and $null -ne $_.FindFirst($Scope::Descendants, $title) } | Select-Object -First 1
}

# Answers the dialog with this heading by pressing one of its buttons.
function Answer-Dialog([string]$Heading, [string]$Button) {
    Check (Wait-Until { $null -ne (Find-Dialog $Heading) } 5) "it asks first: '$Heading'"
    $dialog = Find-Dialog $Heading
    if (-not $dialog) { return }
    $both = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty $Button), (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Button)))
    $press = $dialog.FindFirst($Scope::Descendants, $both)
    Check ($null -ne $press) "the dialog offers '$Button'"
    if ($press) { Press $press }
}

$on = 'Turn on the parametric equaliser?'
$off = 'Turn off the parametric equaliser?'

function Open-Equaliser([string[]]$Flags) {
    Start-NeapPretend -Page equaliser -Flags $Flags
    Set-NeapSize 1400 1500 'Equaliser'
    Wait-Until { $null -ne (Find-Named 'Parametric') } 20 | Out-Null
}

function Test-Editor { Wait-Until { $null -ne (Find-Named 'Start from') } 10 }

if ($Parts.Contains('A')) {
    Section 'through the Charging Dock: choosing Parametric asks, then turns it on'
    Open-Equaliser @()
    try {
        Check ($null -eq (Find-Named 'Start from')) 'it starts on the ten bands'
        Press (Find-Named 'Parametric')
        Answer-Dialog $on 'Turn on'
        Check (Test-Editor) 'the parametric editor opens'
        Save-Shot 'parametric-turned-on' | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('B')) {
    Section 'declining goes back to the ten bands'
    Open-Equaliser @()
    try {
        Press (Find-Named 'Parametric')
        Answer-Dialog $on 'Cancel'
        Check (Wait-Until { $null -eq (Find-Dialog $on) } 4) 'the dialog closes'
        Check ($null -eq (Find-Named 'Start from') -and $null -eq (Find-Named 'Parametric is off.')) 'the ten bands are shown, with nothing about Parametric'
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('C')) {
    Section 'turning it off from the editor'
    Open-Equaliser @('--parametric')
    try {
        Check (Test-Editor) 'the editor is open'
        Press (Find-Named 'Turn off')
        Answer-Dialog $off 'Turn off'
        Check (Wait-Until { $null -eq (Find-Named 'Start from') } 8) 'it goes back to the ten bands'
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('D')) {
    Section 'a headset Swarm II has reset: off, and turned on again from the page'
    Open-Equaliser @('--parametric', '--swarm-reset')
    try {
        Check (Wait-Until { (Get-Texts) -contains "Swarm II reset the headset's equaliser, so Parametric is off." } 15) 'the page says Swarm II reset it'
        Save-Shot 'equaliser-parametric-reset' | ForEach-Object { $_.Dispose() }
        Press (Find-Named 'Turn on')
        Answer-Dialog $on 'Turn on'
        Check (Test-Editor) 'turning it on again opens the editor'
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('E')) {
    Section 'a battery too low to risk the write'
    Open-Equaliser @()
    try {
        Send-Pretend 'report battery 5' | Out-Null
        Start-Sleep -Seconds 1
        Press (Find-Named 'Parametric')
        Answer-Dialog $on 'Turn on'
        Check (Wait-Until { (Get-Texts) -contains "The headset's battery is low. Charge it, then try again." } 6) 'it says the battery is low'
        Check ($null -eq (Find-Named 'Start from')) 'and the editor does not open'
        Check ($null -ne (Find-Named 'Turn on')) 'with a button to try again'
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('F')) {
    Section 'an Atlas Air: no Parametric choice'
    Start-NeapPretend -Page equaliser -Flags @('--atlas')
    try {
        Wait-Until { $null -ne (Find-Named 'Equaliser') } 15 | Out-Null
        Start-Sleep -Seconds 4
        Check ($null -eq (Find-Named 'Parametric')) 'there is no Parametric choice'
    }
    finally { Stop-NeapPretend }
}

# The value in a band's field: the text box inside the named control.
function Get-Field([string]$Name) {
    foreach ($element in Find-All $Name) {
        $edit = $element.FindFirst($Scope::Subtree, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Edit)))
        $pattern = $null
        if ($edit -and $edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) { return $pattern.Current.Value }
    }
}

if ($Parts.Contains('G')) {
    Section 'a shape survives a trip to the ten bands'
    Open-Equaliser @('--parametric')
    try {
        Check (Test-Editor) 'the editor is open'
        Open-Menu (Find-Named 'Start from')
        $item = Wait-Until { $null -ne (Get-Popups | Where-Object { $_.Current.Name -eq 'Add bass' }) } 4
        Press (Get-Popups | Where-Object { $_.Current.Name -eq 'Add bass' } | Select-Object -First 1)
        Start-Sleep -Seconds 2
        $before = @(1..10 | ForEach-Object { Get-Field "Band $_ gain" }) -join ' '
        Note "the shape: $before"
        Press (Find-Named 'Bands')
        Check (Wait-Until { $null -eq (Find-Named 'Start from') } 5) 'the ten bands are shown'
        Start-Sleep -Seconds 2
        Press (Find-Named 'Parametric')
        Check (Test-Editor) 'Parametric opens again'
        Start-Sleep -Seconds 1
        $after = @(1..10 | ForEach-Object { Get-Field "Band $_ gain" }) -join ' '
        Check ($after -eq $before) "the shape on screen is as it was: $after"
        # What the headset plays, not only what the screen shows: in use, a band plays its gain; off, it is a bypass.
        $playing = @(Send-Pretend 'bands' | ForEach-Object { if ($_.type -eq 'Bypass') { '0.0' } else { '{0:+0.0;-0.0;0.0}' -f [double]$_.gain } }) -join ' '
        Check ($playing -eq $before) "and the headset plays it: $playing"
    }
    finally { Stop-NeapPretend }
}
