<#
.SYNOPSIS
Checks that the Parametric mode gives way to another app's ten-band
equaliser, on the published app in pretend mode.

.DESCRIPTION
Run through run.ps1. With a shape in the Parametric mode, the pretend headset
reports a new gain for band 1, as the phone app or Swarm II would. Neap goes
back to the ten bands, says why, plays the other app's gains on the stock
bands, and keeps the shape: Back to Parametric brings it back as it was.
Part B does the same after Parametric is chosen on the page, as a person
does, which the page once held on to while the headset played the ten bands.
Nothing here changes a real headset.
#>
param([string]$Parts = 'AB')

. "$PSScriptRoot\Ui.ps1"

# The value in a band's field: the text box inside the named control.
function Get-Field([string]$Name) {
    foreach ($element in Find-All $Name) {
        $edit = $element.FindFirst($Scope::Subtree, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Edit)))
        $pattern = $null
        if ($edit -and $edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) { return $pattern.Current.Value }
    }
}

$said = 'The equaliser was changed on the headset or in another app, so Neap switched to Bands. Your Parametric shape is kept.'

if ($Parts.Contains('A')) {
    Section 'another app changes the ten-band equaliser'
    Start-NeapPretend -Page equaliser -Flags @('--parametric', '--wired')
    try {
        Set-NeapSize 1400 1500 'Equaliser'
        Check (Wait-Until { $null -ne (Find-Named 'All bands') } 20) 'the parametric editor is open'
        Choose-Parametric 'Add bass'
        Start-Sleep -Seconds 2
        $shape = @(1..10 | ForEach-Object { Get-Field "Band $_ gain" }) -join ' '
        Note "the shape: $shape"

        Send-Pretend 'report eq_band_1 20' | Out-Null
        Check (Wait-Until { (Get-Texts) -contains $said } 6) 'it says another app changed the equaliser'
        Check ($null -eq (Find-Named 'All bands')) 'and shows the ten bands'
        # The headset plays the other app's gains on the stock bands, nothing of the shape beneath them.
        # The ten bands go a write at a time, paced, so they are waited for.
        $stock = { $playing = @(Send-Pretend 'bands'); ($playing | Where-Object { $_.type -ne 'Peaking' }).Count -eq 0 -and [double]$playing[0].gain -eq 2 }
        Check (Wait-Until $stock 8) "the headset plays the other app's gains on the stock bells"
        Note ('playing: ' + ((@(Send-Pretend 'bands') | ForEach-Object { '{0} {1} {2}' -f $_.type, $_.hertz, $_.gain }) -join ' / '))

        Press (Find-Named 'Back to Parametric')
        Check (Wait-Until { $null -ne (Find-Named 'All bands') } 6) 'Back to Parametric opens the editor'
        Start-Sleep -Seconds 1
        $after = @(1..10 | ForEach-Object { Get-Field "Band $_ gain" }) -join ' '
        Check ($after -eq $shape) "the shape is as it was: $after"
        Check (-not ((Get-Texts) -contains $said)) 'and the note has gone'
    }
    finally { Stop-NeapPretend }
}

if ($Parts.Contains('B')) {
    Section 'the headset leaves Parametric after it was chosen on the page'
    Start-NeapPretend -Page equaliser -Flags @('--parametric', '--wired')
    try {
        Set-NeapSize 1400 1500 'Equaliser'
        Check (Wait-Until { $null -ne (Find-Named 'All bands') } 20) 'the parametric editor is open'
        Press (Find-Named 'Bands')
        Check (Wait-Until { $null -eq (Find-Named 'All bands') } 6) 'Bands shows the ten bands'
        Press (Find-Named 'Parametric')
        Check (Wait-Until { $null -ne (Find-Named 'All bands') } 6) 'choosing Parametric on the page opens the editor'
        Choose-Parametric 'Add bass'
        Start-Sleep -Seconds 2

        # As the headset's Mode button stepping to the next preset reports it.
        Send-Pretend 'report eq_band_1 20' | Out-Null
        Check (Wait-Until { (Get-Texts) -contains $said } 6) 'it says the equaliser was changed'
        Check (Wait-Until { $null -eq (Find-Named 'All bands') } 6) 'and the page shows the ten bands the headset plays'
    }
    finally { Stop-NeapPretend }
}
