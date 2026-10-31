<#
.SYNOPSIS
Checks every page of the published app in pretend mode for the parts of
WCAG 2.2 level AA that can be measured from outside: names, target sizes,
keyboard order, a visible focus mark, focus left in view, text contrast, and
what happens in a narrow window.

.DESCRIPTION
Run through run.ps1. The app is launched once per theme, each page is opened
from the rail in turn, and the window is narrowed in place. Focus is walked
from the first control until it comes round again, by asking the practice
run over its pipe to move it as Tab does (focus next), so the window stays
behind everything and the keyboard is left alone: it can run while somebody
uses the machine. That Windows delivers a real Tab press to the app is left
to a person, once before a release.

Contrast is read from a picture of the window. Small text never shows its
true colour there, so a reading under 4.5:1 is put to the app, which says the
colours the text is drawn in and over, and that decides it.

What this cannot see: how a screen reader reads a page, high contrast, and
text enlarged in Windows. Those need a person, and are listed in the report.

.PARAMETER Parts
Which pages to run, as a comma-separated list. All of them by default.
"parametric" is the Equaliser page in its Parametric mode, which needs a
launch of its own with Parametric turned on.

.PARAMETER Headset
"atlas" walks the pages with the pretend headset being an Atlas Air, whose
pages show fewer controls; Parametric is left out, as it has none. The
Stealth Pro II by default.
#>
param([string]$Parts = 'home,audio,equaliser,controls,profiles,device,settings,parametric', [string]$Headset = '')

. "$PSScriptRoot\Ui.ps1"

# Smallest a control may be, in physical pixels, before it needs space around it.
$smallest = 24
$focusable = New-Condition IsKeyboardFocusableProperty $true

function Get-Stops {
    @(Search { $script:Window.FindAll($Scope::Descendants, $focusable) }) | Where-Object { -not $_.Current.IsOffscreen -and $_.Current.IsEnabled }
}
# WCAG leaves out controls that are switched off, so text inside one is not measured.
function Test-InDisabled($Element) {
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    for ($up = $Element; $null -ne $up -and -not [System.Windows.Automation.Automation]::Compare($up, $script:Window); $up = $walker.GetParent($up)) {
        if (-not $up.Current.IsEnabled) { return $true }
    }
    $false
}
function Get-Label($Element) {
    '{0} "{1}" ({2})' -f $Element.Current.ControlType.ProgrammaticName.Replace('ControlType.', ''), $Element.Current.Name, $Element.Current.AutomationId
}
# Whether a stop the walk reached is this control: the same label, and the
# same place to within a few pixels, as the app and UI Automation round apart.
function Test-Same($Seen, $Element) {
    $box = $Element.Current.BoundingRectangle
    $Seen.Label -eq (Get-Label $Element) -and [Math]::Abs($Seen.Box.X - $box.X) -le 8 -and [Math]::Abs($Seen.Box.Y - $box.Y) -le 8
}

# Asks the practice run something over its pipe and returns the answer. Pages
# are opened and focus moved this way, since doing either through UI
# Automation brings the window to the front.
function Connect-Pretend {
    $stream = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'Neap.Pretend', [System.IO.Pipes.PipeDirection]::InOut)
    $stream.Connect(10000)
    $script:Pipe = @{ Stream = $stream; Reader = New-Object System.IO.StreamReader($stream); Writer = New-Object System.IO.StreamWriter($stream) }
    $script:Pipe.Writer.AutoFlush = $true
}
function Invoke-Pretend([string]$Line) {
    $script:Pipe.Writer.WriteLine($Line)
    $reply = $script:Pipe.Reader.ReadLine() | ConvertFrom-Json
    if (-not $reply.ok) { throw "the practice run would not do '$Line': $($reply.error)" }
    $reply.result
}

# Opens a page and waits until what is on it has stopped moving.
function Open-PageBehind([string]$Page) {
    Invoke-Pretend "page $Page" | Out-Null
    Wait-Stable { (Get-Texts) -join ';' } | Out-Null
}

# Asks the practice run to move focus as Tab does, and returns what took it,
# or null when nothing did.
function Invoke-Focus([string]$Way) {
    $r = Invoke-Pretend "focus $Way"
    if ($null -eq $r) { return $null }
    @{
        From = $r.from
        Key = '{0}|{1}|{2}|{3},{4}' -f $r.type, $r.name, $r.id, $r.left, $r.top
        Label = '{0} "{1}" ({2})' -f $r.type, $r.name, $r.id
        Box = New-Object System.Windows.Rect($r.left, $r.top, ($r.right - $r.left), ($r.bottom - $r.top))
    }
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

# A picture of the window once it has stopped changing, so a focus mark still
# being drawn, or a page still scrolling to the control, is not caught halfway.
function Get-StillBitmap {
    $last = $null; $previous = $null
    for ($try = 0; $try -lt 12; $try++) {
        $bitmap = Get-WindowBitmap
        $sample = New-Object System.Text.StringBuilder
        for ($x = 4; $x -lt $bitmap.Width; $x += 24) { for ($y = 4; $y -lt $bitmap.Height; $y += 24) { [void]$sample.Append($bitmap.GetPixel($x, $y).ToArgb()) } }
        $now = $sample.ToString()
        if ($null -ne $previous) { $previous.Dispose() }
        if ($now -eq $last) { return $bitmap }
        $last = $now; $previous = $bitmap
        Start-Sleep -Milliseconds 50
    }
    $previous
}

# Moves focus from the first control until it comes round again, taking a
# picture at each stop. Returns what each stop was and whether it showed focus.
function Invoke-TabWalk($Stops) {
    $seen = @(); $shots = @(); $origin = Get-NeapRect
        $limit = $Stops.Count + 15
        # Every walk starts on the window's first control, wherever the last
        # page left focus, so coming round to it again is plain to see.
    $now = Invoke-Focus 'first'
    for ($i = 0; $i -lt $limit; $i++) {
        if ($null -eq $now) { $seen += , @{ Left = $true }; break }
        if ($seen.Count -gt 0 -and $now.Key -eq $seen[0].Key) { $seen += , @{ Wrapped = $true }; break }
        $seen += , $now
        $shots += , (Get-StillBitmap)
        $now = Invoke-Focus 'next'
        if ($null -ne $now -and $now.From -ne $seen[-1].Label) { $script:Wandered += , "from $($seen[-1].Label) to $($now.From)" }
    }
    $after = Get-StillBitmap
    $visited = @()
    for ($i = 0; $i -lt $shots.Count; $i++) {
        $next = if ($i + 1 -lt $shots.Count) { $shots[$i + 1] } else { $after }
        $visited += , @{ Seen = $seen[$i]; Marked = (Test-Changed $shots[$i] $next $seen[$i].Box $origin) }
    }
    $shots | ForEach-Object { $_.Dispose() }; $after.Dispose()
    @{ Stops = $visited; End = $seen[-1] }
}

# Checks the page open in the window, then narrows the window; the caller
# opens the page and puts the window's size back.
function Test-Page([string]$Page, [string]$Theme) {
    Section "$Page, $Theme"
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
        $all = @($script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Text)))) |
            Where-Object { -not $_.Current.IsOffscreen -and $_.Current.Name.Trim().Length -gt 1 -and $_.Current.BoundingRectangle.Width -gt 4 -and -not (Test-InDisabled $_) }
        # Text scrolled below the window is not in the picture, and would measure
        # as one flat colour; UI Automation does not call it off screen.
        $texts = @($all | Where-Object { $b = $_.Current.BoundingRectangle; $b.Top -ge $origin.Top -and $b.Bottom -le $origin.Bottom -and $b.Left -ge $origin.Left -and $b.Right -le $origin.Right })
        if ($texts.Count -lt $all.Count) { Note "$($all.Count - $texts.Count) pieces of text lie outside the window and are not measured" }
        $low = 0
        foreach ($text in $texts) {
            $box = $text.Current.BoundingRectangle
            $colours = Get-BoxColours $bitmap $box $origin
            $ratio = Get-ContrastRatio $colours.Text $colours.Ground
            if ($ratio -ge 4.5) { continue }
            # Small text never shows its true colour in a picture, as its
            # edges blend into what is behind it, so a short reading is put to
            # the app, which says what the text is drawn in and over.
            $drawn = Invoke-Pretend ('colours {0} {1}' -f [int]($box.X + $box.Width / 2), [int]($box.Y + $box.Height / 2))
            if ($drawn) {
                $declared = Get-ContrastRatio ([System.Drawing.ColorTranslator]::FromHtml($drawn.fore)) ([System.Drawing.ColorTranslator]::FromHtml($drawn.back))
                if ($declared -ge 4.5) { Note ("'{0}' reads {1}:1 in the picture; it is drawn {2} on {3}, {4}:1" -f $text.Current.Name, $ratio, $drawn.fore, $drawn.back, $declared); continue }
                $ratio = $declared
            }
            $low++
            Note ("contrast {0}:1 on '{1}'" -f $ratio, $text.Current.Name)
        }
        Check ($low -eq 0) "all $($texts.Count) pieces of text are 4.5:1 or better"
    }
    finally { $bitmap.Dispose() }

    foreach ($stop in $stops) {
        $name = $stop.Current.Name
        $box = $stop.Current.BoundingRectangle
        if ([string]::IsNullOrWhiteSpace($name)) { Check $false "has no name: $(Get-Label $stop) at $([int]$box.X),$([int]$box.Y)" }
        # A control named from content with no text of its own says its type.
        if ($name -like 'Neap.*' -or $name -like 'Avalonia.*') { Check $false "is named after its code type: $(Get-Label $stop) at $([int]$box.X),$([int]$box.Y)" }
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

    $script:Wandered = @()
    $walk = Invoke-TabWalk $stops
    Check ($script:Wandered.Count -eq 0) 'focus stays where Tab puts it until the next Tab'
    foreach ($move in $script:Wandered) { Note "focus moved by itself $move" }
    Check ($walk.End.Wrapped -eq $true) 'Tab goes round the page and comes back, with no trap and no way out'
    if ($walk.End.Left) { Note 'Tab left the app before it came round' }
    # A list, such as the page rail, is one Tab stop and the arrows move
    # within it, so only a list never entered is worth saying.
    $listEntered = @($walk.Stops | Where-Object { $_.Seen.Label -like 'ListItem*' }).Count -gt 0
    foreach ($stop in $stops) {
        if (@($walk.Stops | Where-Object { Test-Same $_.Seen $stop }).Count -gt 0) { continue }
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
    # Only what lies across the window's height: a box far below it is placed
    # by whichever monitor holds that point, at that monitor's scale.
    $across = @(Get-Stops | Where-Object { $_.Current.BoundingRectangle.Top -lt $narrow.Bottom -and $_.Current.BoundingRectangle.Bottom -gt $narrow.Top })
    $cut = @($across | Where-Object { $_.Current.BoundingRectangle.Right -gt $narrow.Right + 1 -or $_.Current.BoundingRectangle.Left -lt $narrow.Left - 1 })
    Check ($cut.Count -eq 0) 'in a narrow window nothing is cut off at the side'
    foreach ($stop in $cut) { Note "cut off: $(Get-Label $stop)" }
    Save-Shot "access-$Page-$Theme-narrow" -Top 300 | ForEach-Object { $_.Dispose() }
    # The check runs while somebody works, so nothing in it may bring the window forward.
    $front = [NeapUi]::GetForegroundWindow()
    Check ($front -eq [IntPtr]::Zero -or $A::FromHandle($front).Current.ProcessId -ne $script:Proc.Id) 'the window stayed behind other windows'
}

$wanted = @($Parts.Split(',') | ForEach-Object { $_.Trim() })
$headsetFlags = @(if ($Headset -eq 'atlas') { '--atlas' })
$launches = @(@{ Pages = @($wanted | Where-Object { $_ -ne 'parametric' }); Flags = @('--update') + $headsetFlags })
if ($wanted -contains 'parametric' -and -not $headsetFlags) { $launches += @{ Pages = @('parametric'); Flags = @('--update', '--parametric', '--wired') } }
foreach ($theme in 'dark', 'light') {
    foreach ($launch in $launches | Where-Object { $_.Pages.Count -gt 0 }) {
        $pages = $launch.Pages
        $first = if ($pages[0] -eq 'parametric') { 'equaliser' } else { $pages[0] }
        Start-NeapPretend -Theme $theme -Page $first -Flags $launch.Flags
        Connect-Pretend
        try {
            # The simulated update's banner arrives a moment after launch and moves
            # everything below it, so no page is walked until it is there.
            Wait-Until { Test-Present (Find-Named 'Update and restart') } 15 | Out-Null
            foreach ($page in $pages) {
                # Wide again after the last page's narrow window, so the rail is there to open the next.
                Set-NeapSize 1536 1000 'Home' | Out-Null
                if ($page -ne $pages[0]) { Open-PageBehind $page }
                # The editor shows only once the headset's presets are read, a moment after the page opens.
                if ($page -eq 'parametric' -and -not (Wait-Until { Test-Present (Find-Named 'Equaliser curve') } 30)) { Check $false 'the parametric editor opened' }
                Test-Page $page $theme
            }
        }
        finally { $script:Pipe.Stream.Dispose(); Stop-NeapPretend }
    }
}
Write-Host "$script:Fails failures"
exit $script:Fails
