<#
.SYNOPSIS
Checks the Firmware page's parametric equaliser card on the published app in
pretend mode: that it asks before changing anything, says plainly when the
headset is not on its cable, and turns parametric on and back to stock when it is.

.DESCRIPTION
Run through run.ps1. The page is out of the menu until firmware edits are
released, and a practice run opens on it all the same. Part A is the Stealth
Pro II through its dock, which cannot be changed; part B is the headset on its
own cable, which turns parametric on and restores stock; part C is an Atlas
Air, which has no parametric equaliser; part D is a headset Swarm II has reset,
which turns parametric off until it is turned on again.

.PARAMETER Parts
Which parts to run: A through the dock, B on the cable, C an Atlas Air, D reset by Swarm II. All by default.
#>
param([string]$Parts = 'ABCD')

. "$PSScriptRoot\Ui.ps1"

# The dialog is a window of its own that UI Automation lists inside the main one.
function Find-Dialog([string]$Heading) {
    $title = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty $Heading), (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Text)))
    @($script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Window)))) |
        Where-Object { [IntPtr]$_.Current.NativeWindowHandle -ne $script:Handle -and $null -ne $_.FindFirst($Scope::Descendants, $title) } | Select-Object -First 1
}

# Presses the card's button, then the same-named button in the dialog it opens.
function Confirm-Change([string]$Button, [string]$Heading) {
    Press (Find-Named $Button)
    Check (Wait-Until { $null -ne (Find-Dialog $Heading) } 5) "pressing '$Button' asks first: '$Heading'"
    $dialog = Find-Dialog $Heading
    if (-not $dialog) { return }
    $both = New-Object System.Windows.Automation.AndCondition((New-Condition NameProperty $Button), (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Button)))
    $ok = $dialog.FindFirst($Scope::Descendants, $both)
    Check ($null -ne $ok) "the dialog offers '$Button'"
    if ($ok) { Press $ok }
}

if ($Parts.Contains('A')) {
    Section 'the Stealth Pro II through its dock: asks for the cable'
    Start-NeapPretend -Page firmware -Flags @()
    try {
        Wait-Until { $null -ne (Find-Named 'Turn on') } 15 | Out-Null
        Start-Sleep -Seconds 4
        Check ((Get-Texts) -contains 'Parametric is off.') 'it starts off'
        Confirm-Change 'Turn on' 'Turn on the parametric equaliser?'
        Check (Wait-Until { (Get-Texts) -contains 'Connect the headset with its USB-C cable, then try again.' } 10) 'it says to connect the cable'
        Check ((Get-Texts) -contains 'Parametric is off.') 'and is still off'
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('B')) {
    Section 'the headset on its cable: turns on and restores'
    Start-NeapPretend -Page firmware -Flags @('--wired')
    try {
        Wait-Until { $null -ne (Find-Named 'Turn on') } 15 | Out-Null
        Start-Sleep -Seconds 4   # the pretend headset connects over its cable a moment after the page is up
        Confirm-Change 'Turn on' 'Turn on the parametric equaliser?'
        Check (Wait-Until { (Get-Texts) -contains 'Parametric is on.' } 15) 'it says parametric is on'
        Confirm-Change 'Restore' 'Restore the stock equaliser?'
        Check (Wait-Until { (Get-Texts) -contains 'Back to the stock equaliser.' } 15) 'restoring says it is back to stock'
        Check (Wait-Until { (Get-Texts) -contains 'Parametric is off.' } 5) 'and shows parametric off'
        Save-Shot 'firmware-parametric' -Whole | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('C')) {
    Section 'an Atlas Air: no parametric equaliser'
    Start-NeapPretend -Page firmware -Flags @('--atlas')
    try {
        Check (Wait-Until { (Get-Texts) -contains 'The parametric equaliser is for the Stealth Pro II.' } 15) 'it says the equaliser is for the Stealth Pro II'
        Check ($null -eq (Find-Named 'Turn on')) 'and has no Turn on button'
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('D')) {
    Section 'a headset Swarm II has reset: parametric turns off until turned on again'
    $reset = "Swarm II reset the headset's equaliser, so Parametric is off. Turn it on again to use it."
    Start-NeapPretend -Page equaliser -Flags @('--parametric', '--wired', '--swarm-reset')
    try {
        Check (Wait-Until { (Get-Texts) -contains $reset } 15) 'the Equaliser page says Swarm II reset it'
        Check ($null -ne (Find-Named 'Open settings')) 'and offers the way to turn it on'
        Save-Shot 'equaliser-parametric-reset' | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }

    Start-NeapPretend -Page firmware -Flags @('--parametric', '--wired', '--swarm-reset')
    try {
        Check (Wait-Until { (Get-Texts) -contains "Swarm II reset the headset's equaliser, so parametric is off." } 15) 'the Firmware page says Swarm II reset it'
        Confirm-Change 'Turn on' 'Turn on the parametric equaliser?'
        Check (Wait-Until { (Get-Texts) -contains 'Parametric is on.' } 15) 'turning it on again says parametric is on'
    }
    finally { Stop-NeapPretend }
}
