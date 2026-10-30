<#
.SYNOPSIS
Measures alignment on every page, the mini panel, dialogs and open lists in
pretend mode, and lists every row whose controls differ in height or text
baseline, every box whose text is off centre, and every icon off its words.

.DESCRIPTION
Run through run.ps1. One practice run, dark, at one window size. For each
view it takes the controls' edges from UI Automation and finds the text and
icons inside each from the pixels: where the ink starts, its baseline (the
lowest row most of the letters reach), and a leading icon's centre. Findings
of more than 1 pixel go to TestResults\ui\align\report.txt, with a picture of
each view beside it. Measurements are in the screen's pixels.

.PARAMETER Parts
Which parts to run: P the pages, M the mini panel, D the dialogs, L the open
lists. All by default.

.PARAMETER Theme
dark, light or contrast (high contrast). Dark by default.

.PARAMETER Label
A word added to the folder the pictures and report go in, such as before.
#>
param([string]$Parts = 'PMDL', [string]$Theme = 'dark', [string]$Label = '')

. "$PSScriptRoot\Ui.ps1"

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

public sealed class InkGroup { public int Left, Right, Top, Bottom, BodyTop, Baseline; public int Width { get { return Right - Left + 1; } } }

public sealed class InkPicture
{
    private readonly int[] _px; public readonly int Width, Height;
    public InkPicture(Bitmap b)
    {
        Width = b.Width; Height = b.Height; _px = new int[Width * Height];
        var data = b.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < Height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, _px, y * Width, Width);
        b.UnlockBits(data);
    }

    // The ink inside a box, in groups of columns apart by a gap: an icon, then words.
    public List<InkGroup> Groups(int x, int y, int w, int h)
    {
        var groups = new List<InkGroup>();
        int x0 = Math.Max(0, x + 3), y0 = Math.Max(0, y + 2), x1 = Math.Min(Width - 1, x + w - 4), y1 = Math.Min(Height - 1, y + h - 3);
        if (x1 - x0 < 3 || y1 - y0 < 3) return groups;
        var counts = new Dictionary<int, int>();
        for (int yy = y0; yy <= y1; yy++) for (int xx = x0; xx <= x1; xx++) { int k = Quant(_px[yy * Width + xx]); int n; counts.TryGetValue(k, out n); counts[k] = n + 1; }
        int bg = counts.OrderByDescending(p => p.Value).First().Key;
        int W = x1 - x0 + 1, H = y1 - y0 + 1; var ink = new bool[W, H]; var cols = new bool[W];
        for (int yy = 0; yy < H; yy++) for (int xx = 0; xx < W; xx++)
        {
            int c = _px[(y0 + yy) * Width + x0 + xx];
            int d = Math.Abs(((c >> 16) & 255) - (((bg >> 10) & 31) << 3)) + Math.Abs(((c >> 8) & 255) - (((bg >> 5) & 31) << 3)) + Math.Abs((c & 255) - ((bg & 31) << 3));
            if (d > 110) ink[xx, yy] = true;
        }
        // A row or column of ink across nearly the whole box is an edge, such as a pill's or a border, not text.
        for (int yy = 0; yy < H; yy++) { int n = 0; for (int xx = 0; xx < W; xx++) if (ink[xx, yy]) n++; if (n > W * 0.6) for (int xx = 0; xx < W; xx++) ink[xx, yy] = false; }
        for (int xx = 0; xx < W; xx++) { int n = 0; for (int yy = 0; yy < H; yy++) if (ink[xx, yy]) n++; if (n > H * 0.8) for (int yy = 0; yy < H; yy++) ink[xx, yy] = false; }
        for (int xx = 0; xx < W; xx++) for (int yy = 0; yy < H; yy++) if (ink[xx, yy]) { cols[xx] = true; break; }
        int start = -1, gap = 0;
        for (int xx = 0; xx <= W; xx++)
        {
            bool on = xx < W && cols[xx];
            if (on) { if (start < 0) start = xx; gap = 0; }
            else if (start >= 0 && (++gap >= 8 || xx == W)) { groups.Add(Measure(ink, start, xx - gap, H, x0, y0)); start = -1; gap = 0; }
        }
        return groups.Where(g => g != null).ToList();
    }

    private static InkGroup Measure(bool[,] ink, int from, int to, int h, int x0, int y0)
    {
        var rows = new int[h]; int most = 0;
        for (int y = 0; y < h; y++) { for (int x = from; x <= to; x++) if (ink[x, y]) rows[y]++; most = Math.Max(most, rows[y]); }
        if (most == 0) return null;
        int top = Array.FindIndex(rows, r => r > 0), bottom = Array.FindLastIndex(rows, r => r > 0);
        int bodyTop = Array.FindIndex(rows, r => r >= most * 0.3), baseline = Array.FindLastIndex(rows, r => r >= most * 0.3);
        return new InkGroup { Left = x0 + from, Right = x0 + to, Top = y0 + top, Bottom = y0 + bottom, BodyTop = y0 + bodyTop, Baseline = y0 + baseline };
    }

    private static int Quant(int c) { return (((c >> 16) & 255) >> 3) << 10 | (((c >> 8) & 255) >> 3) << 5 | ((c & 255) >> 3); }
}

public static class ProcessWindows
{
    private delegate bool Visitor(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Visitor visitor, IntPtr state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);

    // The visible top-level windows of a process, popups included.
    public static IntPtr[] Of(int process)
    {
        var found = new List<IntPtr>();
        EnumWindows((window, state) => { uint id; GetWindowThreadProcessId(window, out id); if (id == process && IsWindowVisible(window)) found.Add(window); return true; }, IntPtr.Zero);
        return found.ToArray();
    }
}
'@

$out = Join-Path $script:Out ("align-$Theme" + $(if ($Label) { "-$Label" } else { '' }))
New-Item -ItemType Directory -Force -Path $out | Out-Null
$report = Join-Path $out 'report.txt'
Set-Content -Path $report -Value "Alignment, $Theme, in the screen's pixels. Findings over 1 px." -Encoding utf8
$CT = [System.Windows.Automation.ControlType]
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$boxes = @($CT::Button, $CT::ComboBox, $CT::Edit, $CT::SplitButton, $CT::ListItem, $CT::MenuItem)
$kinds = $boxes + @($CT::Text, $CT::CheckBox, $CT::RadioButton, $CT::Hyperlink)

function Say([string]$line) { Add-Content -Path $report -Value $line -Encoding utf8; Write-Host "  $line" }

# Text whose top is the height of a capital, so its centring can be judged.
function Test-Tall([string]$Name) { $Name -cmatch '[A-Z0-9bdfhklt]' }

# Measures every row of controls in a view: the window or list $Root, pictured as $Bitmap whose top left is $Origin on screen, at $Scale screen pixels to a picture pixel.
function Measure-View([string]$View, $Root, $Bitmap, $Origin, [double]$Scale = 1) {
    $picture = New-Object InkPicture $Bitmap
    $found = @()
    foreach ($el in @($Root.FindAll($Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition))) {
        $c = $el.Current
        if ($c.IsOffscreen -or $kinds -notcontains $c.ControlType) { continue }
        $r = $c.BoundingRectangle
        if ($r.Width -lt 6 -or $r.Height -lt 6) { continue }
        $x = ($r.X - $Origin.X) / $Scale; $y = ($r.Y - $Origin.Y) / $Scale; $w = $r.Width / $Scale; $h = $r.Height / $Scale
        if ($x -lt 0 -or $y -lt 0 -or ($x + $w) -gt $picture.Width + 1 -or ($y + $h) -gt $picture.Height + 1) { continue }
        $parent = $walker.GetParent($el)
        $found += [pscustomobject]@{ Name = $c.Name; Type = $c.ControlType.ProgrammaticName -replace 'ControlType\.', ''; Box = $boxes -contains $c.ControlType; Holds = $boxes + @($CT::RadioButton, $CT::CheckBox) -contains $c.ControlType; Parent = $(if ($parent) { ($parent.GetRuntimeId() -join '.') } else { '' }); X = $x; Y = $y; W = $w; H = $h }
    }
    # A label or control inside another control is that control's own; only the outer one is measured.
    $found = @($found | Where-Object { $e = $_; -not ($found | Where-Object { $_ -ne $e -and $_.Holds -and $_.Type -ne 'ListItem' -and $e.X -ge $_.X - 1 -and $e.Y -ge $_.Y - 1 -and ($e.X + $e.W) -le ($_.X + $_.W + 1) -and ($e.Y + $e.H) -le ($_.Y + $_.H + 1) }) })
    foreach ($e in $found) {
        $groups = @($picture.Groups([int]$e.X, [int]$e.Y, [int]$e.W, [int]$e.H))
        $icon = $null; $words = $groups
        $iconShaped = { param($g) $g.Width -le [Math]::Max(28, ($g.Bottom - $g.Top) * 1.4) }
        if ($groups.Count -ge 2 -and (& $iconShaped $groups[0])) { $icon = $groups[0]; $words = $groups[1..($groups.Count - 1)] }
        elseif ($groups.Count -eq 1 -and $e.Box -and (& $iconShaped $groups[0]) -and -not ((Test-Tall $e.Name) -and $e.W -gt $e.H * 2)) { $icon = $groups[0]; $words = @() }
        $e | Add-Member Icon $icon
        $e | Add-Member Words $(if ($words.Count) { [pscustomobject]@{ Top = ($words | Measure-Object Top -Minimum).Minimum; Baseline = ($words | Measure-Object Baseline -Maximum).Maximum; BodyTop = ($words | Measure-Object BodyTop -Minimum).Minimum } } else { $null })
    }
    $count = 0
    # A font leaves room below its baseline, so text in a centred box measures a little high; what is off is text that differs from the rest.
    $centred = @($found | Where-Object { $_.Box -and $_.Words -and (Test-Tall $_.Name) })
    foreach ($e in $centred) { $e | Add-Member Off ((($e.Words.Top - $e.Y) - (($e.Y + $e.H) - $e.Words.Baseline)) / 2) }
    $usual = if ($centred.Count) { ($centred.Off | Sort-Object)[[int][Math]::Floor($centred.Count / 2)] } else { 0 }
    if ($centred.Count) { Say "$View | text in boxes usually sits $([Math]::Round($usual, 1)) px from centre (the font's own room below the baseline)" }
    foreach ($e in $centred) {
        if ([Math]::Abs($e.Off - $usual) -gt 1) { Say "$View | $($e.Type) '$($e.Name)' | text $([Math]::Round($e.Off - $usual, 1)) px $(if ($e.Off -gt $usual) { 'lower' } else { 'higher' }) in its box than usual"; $count++ }
    }
    foreach ($e in $found | Where-Object { $_.Box -and $_.Icon -and -not $_.Words }) {
        $off = (($e.Icon.Top - $e.Y) - (($e.Y + $e.H) - $e.Icon.Bottom)) / 2
        if ([Math]::Abs($off) -gt 1) { Say "$View | $($e.Type) '$($e.Name)' | icon off centre in its box by $([Math]::Round($off, 1)) px ($(if ($off -gt 0) { 'low' } else { 'high' }))"; $count++ }
    }
    foreach ($e in $found | Where-Object { $_.Words }) {
        if ($e.Icon -and $e.Words) {
            $iconMid = ($e.Icon.Top + $e.Icon.Bottom) / 2; $wordsMid = ($e.Words.Top + $e.Words.Baseline) / 2
            if ([Math]::Abs($iconMid - $wordsMid) -gt 1) { Say "$View | $($e.Type) '$($e.Name)' | icon off its words by $([Math]::Round($iconMid - $wordsMid, 1)) px ($(if ($iconMid -gt $wordsMid) { 'icon low' } else { 'icon high' }))"; $count++ }
        }
    }
    # Rows: controls whose middles are close, left to right.
    $rows = @()
    foreach ($e in $found | Sort-Object { $_.Y + $_.H / 2 }) {
        $mid = $e.Y + $e.H / 2
        $row = $rows | Where-Object { $_.Parent -eq $e.Parent -and [Math]::Abs($_.Mid - $mid) -le [Math]::Max(5, [Math]::Min($_.H, $e.H) * 0.35) } | Select-Object -First 1
        if ($row) { $row.Items += $e } else { $rows += [pscustomobject]@{ Mid = $mid; H = $e.H; Parent = $e.Parent; Items = @($e) } }
    }
    foreach ($row in $rows | Where-Object { $_.Items.Count -ge 2 }) {
        $items = @($row.Items | Where-Object { $_.Type -ne 'ListItem' } | Sort-Object X)
        $sized = @($items | Where-Object { $_.Box })
        if ($sized.Count -ge 2) {
            $hs = $sized | ForEach-Object { [Math]::Round($_.H) }
            if ((($hs | Measure-Object -Maximum).Maximum - ($hs | Measure-Object -Minimum).Minimum) -gt 1) {
                Say "$View | row at $([Math]::Round($row.Mid)) | heights differ: $(($sized | ForEach-Object { "$($_.Type) '$($_.Name)' $([Math]::Round($_.H))" }) -join '; ')"; $count++
            }
        }
        $lined = @($items | Where-Object { $_.Words -and (Test-Tall $_.Name) })
        if ($lined.Count -ge 2) {
            $bs = $lined | ForEach-Object { $_.Words.Baseline }
            $spread = ($bs | Measure-Object -Maximum).Maximum - ($bs | Measure-Object -Minimum).Minimum
            if ($spread -gt 1) { Say "$View | row at $([Math]::Round($row.Mid)) | baselines differ by $spread px: $(($lined | ForEach-Object { "$($_.Type) '$($_.Name)' $($_.Words.Baseline)" }) -join '; ')"; $count++ }
        }
    }
    if ($count -eq 0) { Say "$View | no finding" }
}

# The window: its picture and where it sits.
function Measure-Window([string]$View, $Element) {
    $main = $script:Handle; $mainWindow = $script:Window
    $script:Handle = [IntPtr]$Element.Current.NativeWindowHandle; $script:Window = $Element
    try {
        $bitmap = Get-WindowBitmap
        $bitmap.Save((Join-Path $out "$View.png"))
        $rect = Get-NeapRect
        Measure-View $View $Element $bitmap ([pscustomobject]@{ X = $rect.Left; Y = $rect.Top })
        $bitmap.Dispose()
    }
    finally { $script:Handle = $main; $script:Window = $mainWindow }
}

# An open list: the app draws it at its own scale, so its rows are scaled to the picture.
# Its window is found through Windows, as UI Automation lists its rows under
# the control it opens from and not as a window of their own.
function Measure-List([string]$View) {
    Start-Sleep -Milliseconds 400
    $file = Join-Path $out "$View.png"
    try { Send-Pretend "picture $file" | Out-Null } catch { Say "$View | could not picture the list: $($_.Exception.Message)"; return }
    $bitmap = [System.Drawing.Bitmap]::FromFile($file)
    try {
        # The window shaped like the picture, which leaves out the main window and the mini panel.
        $popup = [ProcessWindows]::Of([int]$script:Proc.Id) | Where-Object { $_ -ne $script:Handle } | ForEach-Object {
            $r = New-Object NeapUi+Rect; [NeapUi]::GetWindowRect($_, [ref]$r) | Out-Null; $r
        } | Sort-Object { [Math]::Abs(($_.Right - $_.Left) * $bitmap.Height - ($_.Bottom - $_.Top) * $bitmap.Width) } | Select-Object -First 1
        if (-not $popup) { Say "$View | no list window found"; return }
        $scale = ($popup.Right - $popup.Left) / $bitmap.Width
        Measure-View $View $script:Window $bitmap ([pscustomobject]@{ X = $popup.Left; Y = $popup.Top }) $scale
    }
    finally { $bitmap.Dispose() }
}

# Every part of a page, a window's height at a time from its top.
function Measure-Page([string]$View) {
    Send-Pretend 'scroll top' | Out-Null
    Wait-Stable { (Get-Texts) -join '|' } 2 | Out-Null
    $part = 1
    do {
        Start-Sleep -Milliseconds 400
        Measure-Window "$View-$part" $script:Window
        $part++
    } while ($part -le 6 -and (Send-Pretend 'scroll down') -eq 'moved')
    Send-Pretend 'scroll top' | Out-Null
}

function Find-Dialog { @($A::RootElement.FindAll($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id)))) + @($script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty $CT::Window))) | Where-Object { [IntPtr]$_.Current.NativeWindowHandle -ne $script:Handle -and $_.Current.NativeWindowHandle -ne 0 } | Select-Object -First 1 }

$extra = if ($Theme -eq 'contrast') { @('--highcontrast') } else { @() }
Start-NeapPretend -Theme $(if ($Theme -eq 'light') { 'light' } else { 'dark' }) -Page home -Flags (@('--parametric', '--wired') + $extra)
try {
    Set-NeapSize 1400 1100 'Profile'
    Wait-Until { $null -ne (Find-Named 'Equaliser preset') } 20 | Out-Null

    if ($Parts.Contains('P')) {
        Section 'the pages'
        foreach ($page in 'home', 'audio', 'controls', 'profiles', 'device', 'settings') {
            Send-Pretend "page $page" | Out-Null
            Measure-Page $page
        }
        Send-Pretend 'page equaliser' | Out-Null
        Wait-Until { $null -ne (Find-Named 'Equaliser curve') } 10 | Out-Null
        foreach ($n in 1..2) { Press (Find-Named 'Add band'); Start-Sleep -Milliseconds 400 }
        Measure-Page 'equaliser-parametric'
        Press (Find-Named 'Bands')
        Start-Sleep -Milliseconds 800
        Measure-Page 'equaliser-bands'
        Press (Find-Named 'Microphone')
        Start-Sleep -Milliseconds 800
        Measure-Page 'equaliser-microphone'
    }

    if ($Parts.Contains('L')) {
        Section 'the open lists'
        Send-Pretend 'page home' | Out-Null
        Wait-Stable { (Get-Texts) -join '|' } 2 | Out-Null
        # A list opened before the headset answers shows only that it is reading.
        Wait-Until { (Get-Texts) -contains 'Headset connected' } 15 | Out-Null
        # Each list on its own, so one that cannot be opened leaves the rest measured.
        $lists = [ordered]@{
            'list-profile' = {
                # The list says it is reading until the profile settings are read, a moment after connecting.
                Open-Menu (Find-Named 'Profile')
                Wait-Until { $box = Find-Named 'New profile'; $box -and $box.Current.IsEnabled } 15 | Out-Null
                Measure-List 'list-profile'; Send-Pretend 'press Profile' | Out-Null }
            'list-chat' = {
                $chat = @(Search { $script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty $CT::Button)) }) | Where-Object { $_.Current.Name -like 'Chat comes from*' } | Select-Object -First 1
                Press $chat; Measure-List 'list-chat'; Send-Pretend "press $($chat.Current.Name)" | Out-Null
            }
            'list-home-preset' = { Open-Menu (Find-Box 'Equaliser preset*'); Measure-List 'list-home-preset'; Close-Menu (Find-Box 'Equaliser preset*') }
            'list-device-shutoff' = { Send-Pretend 'page device' | Out-Null; Start-Sleep 1; Open-Menu (Find-Box 'Auto shut-off*'); Measure-List 'list-device-shutoff'; Close-Menu (Find-Box 'Auto shut-off*') }
            'list-parametric-presets' = {
                Send-Pretend 'page equaliser' | Out-Null; Start-Sleep 1
                Press (Find-Named 'Game'); Start-Sleep -Milliseconds 600
                # The modes show once the presets are read from the headset.
                Wait-Until { $null -ne (Find-Named 'Parametric') } 15 | Out-Null
                Press (Find-Named 'Parametric'); Wait-Until { $null -ne (Find-ParametricPicker) } 6 | Out-Null
                Open-Menu (Find-ParametricPicker); Measure-List 'list-parametric-presets'; Close-Menu (Find-ParametricPicker)
            }
            'list-measured' = { Open-Menu (Find-Box 'Measured'); Measure-List 'list-measured'; Close-Menu (Find-Box 'Measured') }
            'list-band-filter' = {
                if (-not (Find-Box 'Band 1 filter')) { Press (Find-Named 'Add band'); Start-Sleep -Milliseconds 400 }
                Open-Menu (Find-Box 'Band 1 filter'); Measure-List 'list-band-filter'; Close-Menu (Find-Box 'Band 1 filter') }
        }
        foreach ($name in $lists.Keys) {
            try { & $lists[$name] } catch { Say "$name | could not be measured: $($_.Exception.Message)" }
        }
    }

    if ($Parts.Contains('D')) {
        Section 'the dialogs'
        Send-Pretend 'page equaliser' | Out-Null
        Wait-Stable { (Get-Texts) -join '|' } 2 | Out-Null
        if (Find-Named 'Game') { Press (Find-Named 'Game'); Start-Sleep -Milliseconds 600 }
        if (-not (Find-Named 'Save as new')) { Press (Find-Named 'Parametric'); Wait-Until { $null -ne (Find-Named 'Save as new') } 6 | Out-Null }
        Press (Find-Named 'Save as new')
        if (Wait-Until { $null -ne (Find-Dialog) } 5) { Measure-Window 'dialog-save-preset' (Find-Dialog); Press (Find-Anywhere 'Cancel') }
        Wait-Until { $null -eq (Find-Dialog) } 4 | Out-Null
        Open-Menu (Find-Named 'More preset actions')
        Start-Sleep -Milliseconds 400
        try { Send-Pretend "press Import preset$([char]0x2026)" | Out-Null } catch { Say 'dialog-import | could not open it' }
        if (Wait-Until { $null -ne (Find-Dialog) } 5) { Measure-Window 'dialog-import' (Find-Dialog); Press (Find-Anywhere 'Cancel') }
        Wait-Until { $null -eq (Find-Dialog) } 4 | Out-Null
        Send-Pretend 'page profiles' | Out-Null
        Wait-Until { $null -ne (Find-Named 'New profile') -and (Find-Named 'New profile').Current.IsEnabled } 15 | Out-Null
        Press (Find-Named 'New profile')
        if (Wait-Until { $null -ne (Find-Dialog) } 5) { Measure-Window 'dialog-new-profile' (Find-Dialog); Press (Find-Anywhere 'Cancel') }
    }

    if ($Parts.Contains('M')) {
        Section 'the mini panel'
        Send-Pretend 'mini' | Out-Null
        $findPanel = { @($A::RootElement.FindAll($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id)))) | Where-Object { $_.Current.Name -eq 'Mini panel' } | Select-Object -First 1 }
        if (Wait-Until { $null -ne (& $findPanel) } 10) {
            Start-Sleep -Milliseconds 800
            Measure-Window 'mini' (& $findPanel)
        }
        else { Say 'mini | the panel did not open' }
    }
}
finally { Stop-NeapPretend }
