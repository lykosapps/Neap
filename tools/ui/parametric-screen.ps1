<#
.SYNOPSIS
Looks at the true parametric equaliser on the Equaliser page, in pretend mode:
that the curve, the dials and the table of ten are there with names, and
takes pictures in both themes.

.DESCRIPTION
Run through run.ps1. A practice run asked for Parametric opens the Stealth Pro II's
game equaliser in that mode with parametric turned on, and on its own cable.
Nothing here changes a real headset.

.PARAMETER Parts
Which parts to run: A dark, B light. Both by default.
#>
param([string]$Parts = 'AB')

. "$PSScriptRoot\Ui.ps1"

foreach ($part in @(@('A', 'dark'), @('B', 'light'))) {
    if (-not $Parts.Contains($part[0])) { continue }
    Section "the parametric equaliser in $($part[1])"
    Start-NeapPretend -Theme $part[1] -Page equaliser -Flags @('--parametric', '--wired')
    try {
        Set-NeapSize 1400 1500 'Equaliser'
        Wait-Until { $null -ne (Find-Named 'Equaliser curve') } 20 | Out-Null
        Check ($null -ne (Find-Named 'Equaliser curve')) 'the curve is there, with a name'
        Check ($null -ne (Find-Named 'Band 1 frequency')) 'the chosen band has a frequency to set'
        Check ($null -ne (Find-Named 'Band 10 gain')) 'the table reaches the tenth band'
        Check ($null -ne (Find-Named 'Hear without changes')) 'it offers to hear without the changes'
        Save-Shot "parametric-$($part[1])" -Whole | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }
}
