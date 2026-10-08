<#
.SYNOPSIS
Checks What's new on the published app in pretend mode: every line of the
notes fits inside the dialog, a long one wraps, and the install steps are left
out; and with no update on offer, Settings still shows the running version's.

.DESCRIPTION
Run through run.ps1. The pretend release's notes are shaped as the release
workflow writes them, install steps included; the running version's come from
the changelog built into the app. Each dialog is opened in both themes, and a
picture of it saved.
#>

. "$PSScriptRoot\Ui.ps1"

$text = [System.Windows.Automation.ControlType]::Text

# The notes dialog whose heading matches, once it has opened. It is a window of
# its own, which the app's window holds; it is found by its heading, then the
# window around it.
function Find-Notes([string]$Heading) {
    if (-not (Wait-Until { @(Get-Texts) -match $Heading } 8)) { return $null }
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $dialog = Find-Text (@(Get-Texts) -match $Heading | Select-Object -First 1)
    while ($dialog -and $dialog.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window) { $dialog = $walker.GetParent($dialog) }
    if ($dialog -and $dialog.Current.NativeWindowHandle -ne $script:Window.Current.NativeWindowHandle) { $dialog }
}

# Checks that every line of the open dialog's notes fits inside it, then
# returns its lines.
function Test-Fits($Dialog) {
    $script:Window = $Dialog
    $script:Handle = [IntPtr]$Dialog.Current.NativeWindowHandle
    Wait-Stable { (Get-NeapRect).Right } | Out-Null
    $edge = $Dialog.Current.BoundingRectangle
    $lines = @($Dialog.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty $text)) | Where-Object { -not $_.Current.IsOffscreen -and $_.Current.Name })
    $over = @($lines | Where-Object { $_.Current.BoundingRectangle.Right -gt $edge.Right - 8 })
    foreach ($line in $over) { Note ("runs past the edge: '{0}'" -f $line.Current.Name) }
    Check ($lines.Count -gt 0 -and $over.Count -eq 0) "every line of the notes fits inside the dialog ($($lines.Count) read)"
    $lines
}

foreach ($theme in 'dark', 'light') {
    Section "a newer version's notes, $theme"
    Start-NeapPretend -Theme $theme
    try {
        # Wide enough for the banner to show What's new as a button of its own.
        Set-NeapSize 1536 820 'Version 9.9.9 is available.'
        Check (Wait-Until { Test-Present (Find-Named "What's new") } 15) "the banner shows What's new"
        Press (Find-Named "What's new")
        $dialog = Find-Notes "^What's new in Neap 9\.9\.9$"
        Check ($null -ne $dialog) 'the banner opens the notes'
        if ($dialog) {
            $app = $script:Window
            $lines = Test-Fits $dialog
            $long = $lines | Where-Object { $_.Current.Name -like '*long enough to wrap*' } | Select-Object -First 1
            $short = $lines | Where-Object { $_.Current.Name -eq 'A second item.' } | Select-Object -First 1
            Check ($long -and $short -and $long.Current.BoundingRectangle.Height -gt 1.5 * $short.Current.BoundingRectangle.Height) 'a long line wraps onto more than one line'

            $all = @(Get-Texts) -join ' '
            Check ($all -notmatch 'Download the (zip|AppImage)' -and $all -notmatch 'Install on') 'the install steps are left out'
            Check ($all -match 'First thing' -and $all -match 'A paragraph on its own') 'the sections after them are kept'

            Save-Shot "notes-$theme" -Top 900 -Whole | ForEach-Object { $_.Dispose() }
            Press (Find-Id 'CloseButton')
            $script:Window = $app
        }
    }
    finally { Stop-NeapPretend }

    Section "the running version's notes, $theme"
    Start-NeapPretend -Theme $theme -Page settings -Flags @()
    try {
        Check (Wait-Until { Test-Present (Find-Named "What's new") } 15) "Settings shows What's new with no update on offer"
        Press (Find-Named "What's new")
        $dialog = Find-Notes "^What's new in Neap \d+\.\d+\.\d+$"
        Check ($null -ne $dialog) "it opens the running version's notes"
        if ($dialog) {
            $app = $script:Window
            Test-Fits $dialog | Out-Null
            Check (-not (Test-Present (Find-Id 'PrimaryButton'))) 'it offers nothing to install, only Close'
            Save-Shot "notes-running-$theme" -Top 900 -Whole | ForEach-Object { $_.Dispose() }
            Press (Find-Id 'CloseButton')
            $script:Window = $app
        }
    }
    finally { Stop-NeapPretend }
}

Write-Host "$script:Fails failures"
exit ([int]($script:Fails -gt 0))
