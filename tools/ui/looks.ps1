<#
.SYNOPSIS
Takes a picture of every page on the published app in pretend mode, in the
dark, light and high contrast themes, for a person to look over.

.DESCRIPTION
Run through run.ps1. The app is launched once per page and theme, and the
whole window saved as looks-<theme>-<page>.png in TestResults\ui. It checks
only that each page opens; whether a page looks right is for the person
reading the pictures.

.PARAMETER Parts
Which themes to take: D dark, L light, H high contrast. All by default.
#>
param([string]$Parts = 'DLH')

. "$PSScriptRoot\Ui.ps1"

$pages = 'home', 'audio', 'mic', 'controls', 'profiles', 'device', 'settings'
$themes = @(
    @{ Part = 'D'; Name = 'dark'; Theme = 'dark'; Flags = @() }
    @{ Part = 'L'; Name = 'light'; Theme = 'light'; Flags = @() }
    @{ Part = 'H'; Name = 'contrast'; Theme = 'dark'; Flags = @('--highcontrast') }
)

foreach ($theme in $themes | Where-Object { $Parts.Contains($_.Part) }) {
    Section "the pages in $($theme.Name)"
    foreach ($page in $pages) {
        try {
            Start-NeapPretend -Theme $theme.Theme -Page $page -Flags $theme.Flags
            Start-Sleep -Milliseconds 800
            $bitmap = Save-Shot "looks-$($theme.Name)-$page" -Whole
            $bitmap.Dispose()
            Check $true "$page opens"
        }
        catch { Check $false "$page opens: $_" }
        finally { Stop-NeapPretend }
    }
}

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
