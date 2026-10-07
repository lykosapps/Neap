<#
.SYNOPSIS
Checks every page of the published app in pretend mode for the parts of
WCAG 2.2 level AA that can be measured from outside: names, target sizes,
keyboard order, a visible focus mark, focus left in view, text contrast, and
what happens in a narrow window.

.DESCRIPTION
Run through run.ps1. Each page is launched once per theme, and the window is
narrowed in place. The keyboard is walked with Tab, from the first control
until focus comes round again; this takes the keyboard focus, so run it when
nobody is using the machine.

What this cannot see: how a screen reader reads a page, high contrast, and
text enlarged in Windows. Those need a person, and are listed in the report.

.PARAMETER Parts
Which pages to run, as a comma-separated list. All of them by default.
#>
param([string]$Parts = 'home,audio,microphone,controls,profiles,device,settings')

. "$PSScriptRoot\Ui.ps1"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class NeapFocus {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr w);
}
'@

# Smallest a control may be, in physical pixels, before it needs space around it.
$smallest = 24
$focusable = New-Condition IsKeyboardFocusableProperty $true

function Get-Stops {
    @($script:Window.FindAll($Scope::Descendants, $focusable)) | Where-Object { -not $_.Current.IsOffscreen -and $_.Current.IsEnabled }
}
# WCAG leaves out controls that are switched off, so text inside one is not measured.
function Test-InDisabled($Element) {
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    for ($up = $Element; $null -ne $up -and -not [System.Windows.Automation.Automation]::Compare($up, $script:Window); $up = $walker.GetParent($up)) {
        if (-not $up.Current.IsEnabled) { return $true }
    }
    $false
}
function Get-Key($Element) { ($Element.GetRuntimeId() -join '.') }
function Get-Label($Element) {
    '{0} "{1}" ({2})' -f $Element.Current.ControlType.ProgrammaticName.Replace('ControlType.', ''), $Element.Current.Name, $Element.Current.AutomationId
}

# Whether a ring of pixels round a control differs between two pictures of the
# window, which is how a focus mark shows itself.
function Test-Changed($Before, $After, $Box, $Origin) {
    $left = [int]($Box.X - $Origin.Left); $top = [int]($Box.Y - $Origin.Top)
    $right = $left + [int]$Box.Width; $bottom = $top + [int]$Box.Height
    $changed = 0
    # Some controls draw the mark well outside their own box, as a toggle does.
    foreach ($grow in -2, 0, 2, 4, 8, 12, 16) {
        $x0 = $left - $grow; $x1 = $right + $grow; $y0 = $top - $grow; $y1 = $bottom + $grow
        $points = @()
        for ($x = $x0; $x -le $x1; $x += 2) { $points += , @($x, $y0); $points += , @($x, $y1) }
        for ($y = $y0; $y -le $y1; $y += 2) { $points += , @($x0, $y); $points += , @($x1, $y) }
        # A wide row can carry its focus mark on a control inside it, so the
        # inside is sampled too.
        if ($grow -eq -2) {
            for ($x = $x0; $x -le $x1; $x += 6) { for ($y = $y0; $y -le $y1; $y += 6) { $points += , @($x, $y) } }
        }
        foreach ($point in $points) {
            $x = $point[0]; $y = $point[1]
            if ($x -lt 0 -or $y -lt 0 -or $x -ge $Before.Width -or $y -ge $Before.Height) { continue }
            $a = $Before.GetPixel($x, $y); $b = $After.GetPixel($x, $y)
            if ([Math]::Abs($a.R - $b.R) + [Math]::Abs($a.G - $b.G) + [Math]::Abs($a.B - $b.B) -gt 40) { $changed++ }
        }
    }
    $changed -ge 6
}

# Presses Tab from the first control until focus comes round again, taking a
# picture at each stop. Returns what each stop was and whether it showed focus.
function Invoke-TabWalk($Stops) {
    [NeapFocus]::SetForegroundWindow($script:Handle) | Out-Null
    $first = $Stops | Select-Object -First 1
    $first.SetFocus()
    # Focus put there by a program carries no focus mark; one that arrives by a
    # key does, so go back one and forward one.
    Start-Sleep -Milliseconds 250
    [System.Windows.Forms.SendKeys]::SendWait('+{TAB}')
    Start-Sleep -Milliseconds 200
    [System.Windows.Forms.SendKeys]::SendWait('{TAB}')
    Start-Sleep -Milliseconds 250
    $seen = @(); $shots = @(); $origin = Get-NeapRect
    $limit = $Stops.Count + 15
    for ($i = 0; $i -lt $limit; $i++) {
        $now = $A::FocusedElement
        if ($null -eq $now -or $now.Current.ProcessId -ne $script:Proc.Id) { $seen += , @{ Left = $true }; break }
        $key = Get-Key $now
        if ($seen.Count -gt 0 -and $key -eq $seen[0].Key) { $seen += , @{ Wrapped = $true }; break }
        $seen += , @{ Key = $key; Label = Get-Label $now; Box = $now.Current.BoundingRectangle; Offscreen = $now.Current.IsOffscreen }
        $shots += , (Get-WindowBitmap)
        [System.Windows.Forms.SendKeys]::SendWait('{TAB}')
        Start-Sleep -Milliseconds 300
    }
    $after = Get-WindowBitmap
    $visited = @()
    for ($i = 0; $i -lt $shots.Count; $i++) {
        $next = if ($i + 1 -lt $shots.Count) { $shots[$i + 1] } else { $after }
        $visited += , @{ Seen = $seen[$i]; Marked = (Test-Changed $shots[$i] $next $seen[$i].Box $origin) }
    }
    $shots | ForEach-Object { $_.Dispose() }; $after.Dispose()
    @{ Stops = $visited; End = $seen[-1] }
}

function Test-Page([string]$Page, [string]$Theme) {
    Section "$Page, $Theme"
    Start-NeapPretend -Theme $Theme -Page $Page
    try {
        Set-NeapSize 1536 1000 'Home' | Out-Null
        Wait-Stable { (Get-Stops | ForEach-Object { '{0}' -f $_.Current.BoundingRectangle }) -join ';' } | Out-Null
        $stops = @(Get-Stops)
        Check ($stops.Count -gt 0) "$($stops.Count) controls can take the keyboard"
        $title = $script:Window.Current.Name
        Check (-not [string]::IsNullOrWhiteSpace($title)) 'the window has a name'

        # Text against what is behind it, measured before anything scrolls, so
        # the picture and the positions agree.
        $bitmap = Get-WindowBitmap
        try {
            $origin = Get-NeapRect
            $texts = @($script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Text)))) |
                Where-Object { -not $_.Current.IsOffscreen -and $_.Current.Name.Trim().Length -gt 1 -and $_.Current.BoundingRectangle.Width -gt 4 -and -not (Test-InDisabled $_) }
            $low = 0
            foreach ($text in $texts) {
                $colours = Get-BoxColours $bitmap $text.Current.BoundingRectangle $origin
                $ratio = Get-ContrastRatio $colours.Text $colours.Ground
                if ($ratio -lt 4.5) { $low++; Note ("contrast {0}:1 on '{1}'" -f $ratio, $text.Current.Name) }
            }
            Check ($low -eq 0) "all $($texts.Count) pieces of text are 4.5:1 or better"
        }
        finally { $bitmap.Dispose() }

        foreach ($stop in $stops) {
            $name = $stop.Current.Name
            $box = $stop.Current.BoundingRectangle
            if ([string]::IsNullOrWhiteSpace($name)) { Check $false "has no name: $(Get-Label $stop) at $([int]$box.X),$([int]$box.Y)" }
            if ($box.Width -lt $smallest -or $box.Height -lt $smallest) {
                # A control scrolled half out of view reads as short; measure it whole.
                try { $stop.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern).ScrollIntoView() } catch { Write-Verbose 'it cannot be scrolled' }
                Start-Sleep -Milliseconds 300
                $box = $stop.Current.BoundingRectangle
            }
            if ($box.Width -lt $smallest -or $box.Height -lt $smallest) {
                Check $false ("{0} is {1} by {2}, under {3} by {3}" -f (Get-Label $stop), [int]$box.Width, [int]$box.Height, $smallest)
            }
        }

        $walk = Invoke-TabWalk $stops
        $reached = @($walk.Stops | ForEach-Object { $_.Seen.Key })
        Check ($walk.End.Wrapped -eq $true) 'Tab goes round the page and comes back, with no trap and no way out'
        if ($walk.End.Left) { Note 'Tab left the app before it came round' }
        # A list, such as the page rail, is one Tab stop and the arrows move
        # within it, so only a list never entered is worth saying.
        $listEntered = @($walk.Stops | Where-Object { $_.Seen.Label -like 'ListItem*' }).Count -gt 0
        foreach ($stop in $stops) {
            if ((Get-Key $stop) -in $reached) { continue }
            if ($stop.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem -and $listEntered) { continue }
            Note "not reached by Tab, so arrows or nothing: $(Get-Label $stop)"
        }
        $unmarked = @($walk.Stops | Where-Object { -not $_.Marked })
        Check ($unmarked.Count -eq 0) "every Tab stop shows where focus is ($($walk.Stops.Count) stops)"
        foreach ($stop in $unmarked) { Note "no focus mark seen on $($stop.Seen.Label)" }

        # The order, for a person to read against the page: columns and the
        # page rail make a top-to-bottom rule wrong, so it is not judged here.
        $window = Get-NeapRect
        Note ('Tab order: ' + (($walk.Stops | ForEach-Object { '{0} @{1},{2}' -f $_.Seen.Label.Split('(')[0].Trim(), [int]($_.Seen.Box.X - $window.Left), [int]($_.Seen.Box.Y - $window.Top) }) -join ' > '))
        $hidden = @($walk.Stops | Where-Object { $_.Seen.Box.Bottom -gt $window.Bottom + 1 -or $_.Seen.Box.Right -gt $window.Right + 1 -or $_.Seen.Box.Top -lt $window.Top })
        Check ($hidden.Count -eq 0) 'focus is never left outside the window'
        foreach ($stop in $hidden) { Note "focus left out of view on $($stop.Seen.Label)" }

        # A narrow window: nothing may be cut off at the side.
        $rect = Get-NeapRect
        [NeapUi]::MoveWindow($script:Handle, $rect.Left, $rect.Top, 640, 900, $true) | Out-Null
        Wait-Stable { (Get-Stops | ForEach-Object { '{0}' -f $_.Current.BoundingRectangle }) -join ';' } | Out-Null
        $narrow = Get-NeapRect
        $cut = @(Get-Stops | Where-Object { $_.Current.BoundingRectangle.Right -gt $narrow.Right + 1 -or $_.Current.BoundingRectangle.Left -lt $narrow.Left - 1 })
        Check ($cut.Count -eq 0) 'in a narrow window nothing is cut off at the side'
        foreach ($stop in $cut) { Note "cut off: $(Get-Label $stop)" }
        Save-Shot "access-$Page-$Theme-narrow" -Top 300 | ForEach-Object { $_.Dispose() }
    }
    finally { Stop-NeapPretend }
}

foreach ($page in $Parts.Split(',')) {
    foreach ($theme in 'dark', 'light') { Test-Page $page.Trim() $theme }
}
Write-Host "$script:Fails failures"
exit $script:Fails
