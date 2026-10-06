<#
.SYNOPSIS
Checks every page of the new app, in both themes, for what a screen reader and
a keyboard need.

.DESCRIPTION
Launches the Windows build of Neap.Desktop with --pretend on each page, in a
data folder of its own so it can run beside other practice runs, and lists
every interactive element that has no name, cannot be reached from the
keyboard, or is smaller than 24 by 24. The toolkit's own parts (a slider's
track, a scroll bar's arrows) are skipped.

Run it outside an app package, as run.ps1 does for the other checks, and with
the Windows build made: dotnet build src\Neap.Desktop -c Release.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\ui\desktop.ps1
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\..\src\Neap.Desktop\bin\Release\net10.0-windows10.0.19041.0\Neap.Desktop.exe'),
    [string[]]$Pages = @('home', 'audio', 'mic', 'controls', 'profiles', 'device', 'settings'),
    [string[]]$Themes = @('--dark', '--light'),
    [string]$Extra = ''
)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$env:NEAP_PRETEND_FOLDER = 'PretendAudit'
$types = 'Button', 'CheckBox', 'RadioButton', 'Slider', 'ComboBox', 'Edit', 'ListItem', 'Hyperlink', 'MenuItem', 'TabItem', 'SplitButton'
$failures = 0

function Launch($page, $theme) {
    $args = @('--pretend', '--page', $page, $theme)
    if ($Extra) { $args += $Extra.Split(' ') }
    $p = Start-Process -FilePath $Exe -ArgumentList $args -PassThru
    for ($i = 0; $i -lt 40 -and $p.MainWindowHandle -eq 0; $i++) { Start-Sleep -Milliseconds 250; $p.Refresh() }
    Start-Sleep -Seconds 12
    $p
}

# Every interactive element on the page, scrolling to the foot so what is below the fold is seen too.
function Collect($root) {
    $all = @{}
    $scroller = $null
    foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
        if ($e.GetSupportedPatterns().ProgrammaticName -contains 'ScrollPatternIdentifiers.Pattern') {
            $sp = $e.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
            if ($sp.Current.VerticallyScrollable -and $null -eq $scroller) { $scroller = $sp }
        }
    }
    $round = 0
    do {
        foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            $c = $e.Current
            if ($c.IsOffscreen) { continue }
            $kind = $c.ControlType.ProgrammaticName -replace 'ControlType\.', ''
            if ($types -notcontains $kind) { continue }
            $key = ($e.GetRuntimeId() -join '.')
            if (-not $all.ContainsKey($key)) { $all[$key] = $e }
        }
        $more = $false
        if ($scroller -and $scroller.Current.VerticalScrollPercent -lt 99.5) {
            $scroller.ScrollVertical([System.Windows.Automation.ScrollAmount]::LargeIncrement); Start-Sleep -Milliseconds 500; $more = $true
        }
        $round++
    } while ($more -and $round -lt 12)
    $all.Values
}

foreach ($theme in $Themes) {
    foreach ($page in $Pages) {
        $p = Launch $page $theme
        try {
            $win = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
            $items = @(Collect $win)
            $bad = 0
            foreach ($e in $items) {
                $c = $e.Current
                $kind = $c.ControlType.ProgrammaticName -replace 'ControlType\.', ''
                $r = $c.BoundingRectangle
                # The toolkit's own parts are not the app's to name.
                if ($c.AutomationId -like 'PART_*' -or [double]::IsInfinity($r.X)) { continue }
                $where = "{0} at {1:0},{2:0} id='{3}'" -f $kind, $r.X, $r.Y, $c.AutomationId
                if ([string]::IsNullOrWhiteSpace($c.Name)) { Write-Host "  FAIL [$page $theme] unnamed $where"; $bad++ }
                if ($c.IsEnabled -and -not $c.IsKeyboardFocusable) { Write-Host "  FAIL [$page $theme] not keyboard focusable: '$($c.Name)' $where"; $bad++ }
                if ($r.Width -lt 24 -or $r.Height -lt 24) { Write-Host "  FAIL [$page $theme] under 24x24 ($([int]$r.Width)x$([int]$r.Height)): '$($c.Name)' $where"; $bad++ }
            }
            Write-Host ("[{0} {1}] {2} interactive elements, {3} problems" -f $page, $theme, $items.Count, $bad)
            $failures += $bad
        }
        finally { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 800 }
    }
}
Write-Host "== finished, $failures problems"
exit $(if ($failures -eq 0) { 0 } else { 1 })
