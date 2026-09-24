<#
.SYNOPSIS
Runs the published app against the pretend headset and drives every screen
through UI Automation, checking the commands it sends.

.DESCRIPTION
Launches the Release publish with --pretend, so nothing reaches a real
headset or Windows' audio, and talks to the pretend headset over its pipe.
Injected clicks do not reach WinUI content, so every control is operated
through UI Automation.

For each setting control on each page it moves the control, then checks
that exactly one setting was written, that it is the control's own, that
the value is the one chosen and inside the registry's limits, and that the
headset holds it. It then has the headset change that setting itself and
checks the control follows. It also covers the equaliser bands and
presets, the Windows-owned volume, mute and format controls (which must
write nothing to the headset), the chat wheel driving the mix, the
battery, and the headset switching off and on. At the end nothing may have
been refused: no unconfirmed key, no value out of range, no slot base
address, no firmware command.

A screenshot of every page goes in the output folder. Exits 1 if any
check fails.

The app opens with --behind, beneath every other window. Moving between
pages through UI Automation still brings it to the front, so the run stops
the moment it does rather than keep taking keys from whoever is typing:
run it while nobody is using the machine.

Publish first:

    dotnet publish dotnet/StealthPro.App -c Release

.PARAMETER Exe
The published app.

.PARAMETER Out
Where to put the screenshots.

.PARAMETER KeepOpen
Leave the app running afterwards.

.PARAMETER Unattended
Nobody is at the machine, so the app may come to the front without the run
stopping.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\pretend.ps1
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\dotnet\StealthPro.App\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\Neap.exe'),
    [string]$Out = (Join-Path $env:TEMP 'neap-pretend'),
    [switch]$KeepOpen,
    [switch]$Unattended
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class NeapWindow {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
}
'@
[NeapWindow]::SetProcessDPIAware() | Out-Null

$A = [System.Windows.Automation.AutomationElement]
$Scopes = [System.Windows.Automation.TreeScope]

# -- results -----------------------------------------------------------------

$script:failures = New-Object System.Collections.Generic.List[string]
$script:passes = 0

function Pass([string]$what) { $script:passes++; Write-Host "  ok    $what" }
function Fail([string]$what) { $script:failures.Add($what); Write-Host "  FAIL  $what" -ForegroundColor Red }
function Check([bool]$condition, [string]$what) { if ($condition) { Pass $what } else { Fail $what } }

# Samples a condition until it holds or the time runs out: one reading after
# the fact says where a value settled, not whether it moved.
function Until([scriptblock]$condition, [double]$seconds = 3) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt $seconds) {
        Watch
        if (& $condition) { return $true }
        Start-Sleep -Milliseconds 100
    }
    return [bool](& $condition)
}

# The run happens while somebody works: the app must never come to the
# front. Looked at every time the script waits.
function Watch {
    if ($Unattended) { return }
    $owner = [uint32]0
    [NeapWindow]::GetWindowThreadProcessId([NeapWindow]::GetForegroundWindow(), [ref]$owner) | Out-Null
    if ($owner -eq $process.Id) {
        $script:tookFocus++
        throw "the app came to the front during $($script:step); stopped so it does not keep interrupting"
    }
}

# -- the pretend headset's pipe ----------------------------------------------

function Ask([string]$line) {
    $script:writer.WriteLine($line)
    $reply = $script:reader.ReadLine() | ConvertFrom-Json
    if (-not $reply.ok) { throw "pretend headset: $line -> $($reply.error)" }
    return $reply.result
}

function Writes { @(Ask 'writes') }

function Number([string]$hex) { [Convert]::ToInt32($hex, 16) }

# -- the app's window --------------------------------------------------------

function Find($under, [string]$property, $value, [string]$scope = 'Descendants') {
    $condition = New-Object System.Windows.Automation.PropertyCondition($A::$property, $value)
    $under.FindFirst($Scopes::$scope, $condition)
}

function FindAll($under, [string]$property, $value) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($A::$property, $value)
    @($under.FindAll($Scopes::Descendants, $condition))
}

# The first control of a type with this name; its label often shares the name.
function Control([string]$name, $type) {
    FindAll $script:window 'NameProperty' $name | Where-Object { $_.Current.ControlType -eq $type } |
        Select-Object -First 1
}

function Pattern($element, $pattern) { $element.GetCurrentPattern($pattern::Pattern) }

# Opens a page and waits for the app to stop asking the headset things. A
# page with an equaliser reads the preset slots on arrival, an empty one
# taking a second, and a write made meanwhile waits behind them.
function Page([string]$name) {
    $script:step = "the $name page"
    $items = FindAll $script:window 'NameProperty' $name |
        Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem }
    (Pattern $items[0] ([System.Windows.Automation.SelectionItemPattern])).Select()
    Start-Sleep -Milliseconds 800
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $count = Ask 'sent'
    $still = [Diagnostics.Stopwatch]::StartNew()
    while ($still.Elapsed.TotalSeconds -lt 1.5 -and $clock.Elapsed.TotalSeconds -lt 20) {
        Watch
        Start-Sleep -Milliseconds 100
        $now = Ask 'sent'
        if ($now -ne $count) { $count = $now; $still.Restart() }
    }
}

function Screenshot([string]$name) {
    $rect = New-Object NeapWindow+Rect
    [NeapWindow]::GetWindowRect($script:handle, [ref]$rect) | Out-Null
    $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $dc = $graphics.GetHdc()
    # 2 = PW_RENDERFULLCONTENT, which captures composed WinUI content.
    [NeapWindow]::PrintWindow($script:handle, $dc, 2) | Out-Null
    $graphics.ReleaseHdc($dc)
    $graphics.Dispose()
    $path = Join-Path $Out "$name.png"
    $bitmap.Save($path)
    $bitmap.Dispose()
    Write-Host "  shot  $path"
}

function Header { (Find $script:window 'AutomationIdProperty' 'ConnectionText').Current.Name }

# -- reading and moving one control ------------------------------------------

function Kind($element) {
    $type = $element.Current.ControlType
    if ($type -eq [System.Windows.Automation.ControlType]::Slider) { return 'slider' }
    if ($type -eq [System.Windows.Automation.ControlType]::ComboBox) { return 'choice' }
    return 'toggle'
}

function Options($element) {
    $expand = Pattern $element ([System.Windows.Automation.ExpandCollapsePattern])
    $expand.Expand()
    Start-Sleep -Milliseconds 300
    $items = FindAll $element 'ControlTypeProperty' ([System.Windows.Automation.ControlType]::ListItem)
    $names = @($items | Where-Object { $_.Current.IsEnabled } | ForEach-Object { $_.Current.Name })
    $expand.Collapse()
    Start-Sleep -Milliseconds 200
    return $names
}

function Choose($element, [string]$label) {
    $expand = Pattern $element ([System.Windows.Automation.ExpandCollapsePattern])
    $expand.Expand()
    Start-Sleep -Milliseconds 300
    $item = FindAll $element 'ControlTypeProperty' ([System.Windows.Automation.ControlType]::ListItem) |
        Where-Object { $_.Current.Name -eq $label } | Select-Object -First 1
    if ($null -eq $item) { $expand.Collapse(); throw "no option '$label' in $($element.Current.Name)" }
    try { (Pattern $item ([System.Windows.Automation.SelectionItemPattern])).Select() }
    catch [System.Windows.Automation.ElementNotEnabledException] {
        # The format list disables itself while the change is applied, and
        # the call reports that even though the selection was made.
    }
    Start-Sleep -Milliseconds 200
    if ($expand.Current.ExpandCollapseState -ne 'Collapsed') { $expand.Collapse() }
}

function Shown($element) {
    switch (Kind $element) {
        'slider' { [int](Pattern $element ([System.Windows.Automation.RangeValuePattern])).Current.Value }
        'choice' {
            $chosen = (Pattern $element ([System.Windows.Automation.SelectionPattern])).Current.GetSelection()
            if ($chosen.Count -gt 0) { $chosen[0].Current.Name } else { '' }
        }
        default { [int]((Pattern $element ([System.Windows.Automation.TogglePattern])).Current.ToggleState -eq 'On') }
    }
}

function Operate($element, $value) {
    switch (Kind $element) {
        'slider' { (Pattern $element ([System.Windows.Automation.RangeValuePattern])).SetValue([double]$value) }
        'choice' { Choose $element $value }
        default { (Pattern $element ([System.Windows.Automation.TogglePattern])).Toggle() }
    }
}

# A value inside the setting's limits and different from the one shown.
function Another($setting, $shown, [string]$kind) {
    switch ($kind) {
        'slider' {
            $low = if ($null -ne $setting.min) { $setting.min } else { 0 }
            $high = if ($null -ne $setting.max) { $setting.max } else { 100 }
            $pick = [int]($low + ($high - $low) / 3)
            if ($pick -eq $shown) { $pick = [int]($low + 2 * ($high - $low) / 3) }
            return $pick
        }
        'choice' {
            $labels = @($setting.options.PSObject.Properties | ForEach-Object { $_.Value })
            return @($labels | Where-Object { $_ -ne $shown })[0]
        }
        default { return 1 - $shown }
    }
}

# The value a label or a toggle state goes out as.
function Wire($setting, $value, [string]$kind) {
    if ($kind -eq 'choice') {
        return ($setting.options.PSObject.Properties | Where-Object { $_.Value -eq $value }).Name
    }
    return [string]$value
}

# Everything written must be a confirmed setting, writable, within its
# limits. The pretend headset refuses anything else as well; this checks
# independently of it.
function Confirmed($write) {
    $key = Number $write.key
    $setting = $script:registry | Where-Object { (Number $_.key) -eq $key } | Select-Object -First 1
    $slot = $key - 0x400
    if (-not $setting -and $slot -ge 0 -and $slot -lt 0x80 -and ($slot % 0x20) -in 1, 2) {
        # A lighting write for another transmitter's slot; the limits are slot one's.
        $setting = $script:registry | Where-Object { (Number $_.key) -eq (0x400 + $slot % 0x20) }
    }
    if (-not $setting -or -not $setting.writable) { return $false }
    if ($setting.kind -eq 'Text') { return $true }
    $number = 0
    if (-not [int]::TryParse($write.value, [ref]$number)) { return $false }
    if ($setting.kind -eq 'Toggle') { return $number -in 0, 1 }
    if ($null -ne $setting.options) { return [bool]($setting.options.PSObject.Properties.Name -contains $write.value) }
    return ($null -eq $setting.min -or $number -ge $setting.min) -and ($null -eq $setting.max -or $number -le $setting.max)
}

function Collect {
    foreach ($write in Writes) {
        $script:everything.Add($write)
        if (-not (Confirmed $write)) { Fail "write $($write.key)=$($write.value) is not a confirmed write" }
    }
    Ask 'clear' | Out-Null
}

# -- the checks ----------------------------------------------------------------

# Moves one setting's control, checks what was sent, then has the headset
# change the setting and checks the control follows.
function Exercise($element, $setting) {
    $name = $setting.name
    $script:step = $name
    $kind = Kind $element
    if (-not $element.Current.IsEnabled) { Write-Host "  skip  $name is not enabled in this state"; return }

    Ask 'clear' | Out-Null
    $shown = Shown $element
    $want = Another $setting $shown $kind
    $wire = Wire $setting $want $kind
    Operate $element $want

    Until { @(Writes | Where-Object { $_.key -eq $setting.key }).Count -gt 0 } | Out-Null
    Start-Sleep -Milliseconds 300
    $sent = @(Writes)
    $own = @($sent | Where-Object { $_.key -eq $setting.key })
    Check ($own.Count -ge 1 -and $own[-1].value -eq $wire) "$name sends $($setting.key)=$wire"
    Check (@($sent | Where-Object { $_.key -ne $setting.key }).Count -eq 0) "$name writes nothing else"
    Check ((Ask "value $($setting.key)") -eq $wire) "the headset holds $name=$wire"
    Collect

    # The value the person just set is theirs for a moment; let it pass.
    Start-Sleep -Milliseconds 1800
    $back = if ($kind -eq 'choice') { $shown } else { Another $setting $want $kind }
    $backWire = Wire $setting $back $kind
    try { Ask "report $($setting.key) $backWire" | Out-Null }
    catch { Write-Host "  note  $name is set only from the app: $($_.Exception.Message)"; return }
    Check (Until { [string](Shown $element) -eq [string]$back }) "$name follows the headset to $backWire"
    Check (@(Writes).Count -eq 0) "following the headset sends nothing back"
    Collect
}

# Opens every expander on the page, so the settings folded inside one can
# be found. Lists are left alone: opening one is choosing from it.
function ExpandAll {
    $all = $script:window.FindAll($Scopes::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $all) {
        if ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::ComboBox) { continue }
        $expand = $null
        if ($element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$expand) -and
            $expand.Current.ExpandCollapseState -eq 'Collapsed') { $expand.Expand() }
    }
    Start-Sleep -Milliseconds 400
}

function Settings([string]$page) {
    Page $page
    ExpandAll
    $found = 0
    foreach ($setting in $script:registry | Where-Object { $_.writable -and $_.kind -ne 'Text' }) {
        $element = Find $script:window 'AutomationIdProperty' $setting.name
        if ($null -eq $element) { continue }
        $found++
        $script:covered[$setting.name] = $true
        Exercise $element $setting
    }
    Check ($found -gt 0) "$page shows setting controls"
}

function Band([string]$page, [string]$label, [string]$key) {
    Page $page
    $band = Control $label ([System.Windows.Automation.ControlType]::Spinner)
    if ($null -eq $band) { Fail "$page has an equaliser band $label"; return }

    Ask 'clear' | Out-Null
    (Pattern $band ([System.Windows.Automation.RangeValuePattern])).SetValue(3)
    Check (Until { @(Writes | Where-Object { $_.key -eq $key -and $_.value -eq '30' }).Count -gt 0 }) "$page $label band sends $key=30"
    Collect
}

function Preset([string]$page, [string]$name, [string]$key, [string]$id) {
    Page $page
    $button = Control $name ([System.Windows.Automation.ControlType]::Button)
    if ($null -eq $button) { Fail "$page lists the preset $name"; return }

    Ask 'clear' | Out-Null
    (Pattern $button ([System.Windows.Automation.InvokePattern])).Invoke()
    Check (Until { @(Writes | Where-Object { $_.key -eq $key -and $_.value -eq $id }).Count -gt 0 }) "$page preset $name sends $key=$id"
    Check (Until { (Find $script:window 'AutomationIdProperty' 'PresetName').Current.Name -eq $name }) "$page names the preset $name"
    Collect
}

function WindowsOwned {
    Page 'Audio'
    Ask 'clear' | Out-Null
    $volume = Control 'Master volume' ([System.Windows.Automation.ControlType]::Slider)
    (Pattern $volume ([System.Windows.Automation.RangeValuePattern])).SetValue(30)
    Check (Until { (Ask 'volume output').percent -eq 30 }) 'master volume sets Windows'' volume'
    $mute = Control 'Mute' ([System.Windows.Automation.ControlType]::Button)
    (Pattern $mute ([System.Windows.Automation.TogglePattern])).Toggle()
    Check (Until { (Ask 'volume output').muted }) 'mute mutes Windows'' output'
    (Pattern $mute ([System.Windows.Automation.TogglePattern])).Toggle()
    Check (Until { -not (Ask 'volume output').muted }) 'mute unmutes it again'
    Start-Sleep -Milliseconds 500
    Check (@(Writes).Count -eq 0) 'the Windows volume controls write nothing to the headset'
    Collect
}

# Last, because it has crashed the app: an item chosen through UI
# Automation while the list is open, which then disables itself.
function Format {
    Page 'Device'
    $format = Control 'Headset output' ([System.Windows.Automation.ControlType]::ComboBox)
    $before = Ask 'format output'
    $other = @(Options $format | Where-Object { $_ -ne (Shown $format) })[0]
    Choose $format $other
    Start-Sleep -Seconds 3
    if ($process.HasExited) { Fail 'the app crashed choosing an output format'; return }
    Check (Until { (Ask 'format output') -ne $before } 5) "the output format moves off $before"
    Check (@(Writes).Count -eq 0) 'the format writes nothing to the headset'
    Collect
}

function Wheel {
    Page 'Home'
    $picker = Find $script:window 'AutomationIdProperty' 'ChatPicker'
    if ($null -eq $picker) { Fail 'Home offers a chat app picker'; return }
    Choose $picker 'Pretend Chat'
    Check (Until { $null -ne (Ask 'mix') } 5) 'choosing a chat app starts the mix'
    $start = Ask 'mix'

    # The first count is a starting point; the turns after it move the mix.
    foreach ($count in 50, 55, 60, 65, 70, 75, 80) {
        Ask "report game_chat_mix $count" | Out-Null
        Start-Sleep -Milliseconds 150
    }
    Check (Until { (Ask 'mix') -gt $start }) "turning the wheel toward chat moves the mix from $start to $(Ask 'mix')"
    Check (@(Writes | Where-Object { $_.key -eq '0x510' }).Count -eq 0) 'the wheel is read, never written'
    Collect
    Screenshot 'home-mix'
}

# A switch on Home, which writes one key and follows the headset. $inverted
# is for the microphone, whose switch is on while it is live and so writes
# the mute the other way round.
function QuickSwitch([string]$name, [string]$setting, [string]$key, [bool]$inverted) {
    $script:step = "the $name switch on Home"
    $switch = Control $name ([System.Windows.Automation.ControlType]::Button)
    if ($null -eq $switch) { Fail "Home has a $name switch"; return }
    $toggle = Pattern $switch ([System.Windows.Automation.TogglePattern])
    $on = $toggle.Current.ToggleState -eq 'On'
    $want = if ($on -eq $inverted) { '1' } else { '0' }

    Ask 'clear' | Out-Null
    $toggle.Toggle()
    Check (Until { @(Writes | Where-Object { $_.key -eq $key -and $_.value -eq $want }).Count -gt 0 }) "the $name switch sends $key=$want"
    Check (@(Writes | Where-Object { $_.key -ne $key }).Count -eq 0) "the $name switch writes nothing else"
    Collect

    # The value just set is the person's for a moment; let it pass.
    Start-Sleep -Milliseconds 1800
    $back = if ($want -eq '1') { '0' } else { '1' }
    Ask "report $setting $back" | Out-Null
    Check (Until { ($toggle.Current.ToggleState -eq 'On') -eq $on }) "the $name switch follows the headset back"
    Check (@(Writes).Count -eq 0) 'following the headset sends nothing back'
    Collect
}

function Quick {
    Page 'Home'
    QuickSwitch 'Microphone' 'mic_muted' '0x600' $true
    QuickSwitch 'Noise cancellation' 'anc' '0x750' $false

    $script:step = 'the preset list on Home'
    $picker = Find $script:window 'AutomationIdProperty' 'PresetPicker'
    if ($null -eq $picker) { Fail 'Home has a preset list'; return }
    Ask 'clear' | Out-Null
    Choose $picker 'Bass Boost'
    Check (Until { @(Writes | Where-Object { $_.key -eq '0x1210' -and $_.value -eq '2' }).Count -gt 0 }) 'the preset list on Home sends 0x1210=2'
    Collect
}

function Battery {
    Page 'Home'
    Ask 'report battery 42' | Out-Null
    Check (Until { $null -ne (Find $script:window 'NameProperty' '42%') }) 'Home shows the battery the headset reports'
}

function PowerCycle {
    Ask 'off' | Out-Null
    Check (Until { (Header) -ne 'Headset connected' } 40) "switched off, the header says $(Header)"
    Screenshot 'home-off'
    Ask 'on' | Out-Null
    Check (Until { (Header) -eq 'Headset connected' } 40) 'switched back on, the header says Headset connected'
}

# -- the run -------------------------------------------------------------------

if (-not (Test-Path $Exe)) { throw "no published app at $Exe; run dotnet publish dotnet/StealthPro.App -c Release" }
$Exe = (Resolve-Path $Exe).Path
$running = Get-CimInstance Win32_Process -Filter "Name = 'Neap.exe'" | Where-Object { $_.CommandLine -like '*--pretend*' }
if ($running) { throw 'a pretend run is already open; quit it first' }

New-Item -ItemType Directory -Force -Path $Out | Out-Null
# A clean start: the pretend run's own folder, never the real app's.
Remove-Item (Join-Path $env:LOCALAPPDATA 'Neap\Pretend') -Recurse -Force -ErrorAction SilentlyContinue

$script:tookFocus = 0
$script:step = 'the launch'
# Behind every other window and never activated, so the run does not take
# the machine from whoever is using it.
$process = Start-Process $Exe -ArgumentList '--pretend', '--behind' -PassThru
try {
    $root = $A::RootElement
    if (-not (Until { $null -ne (Find $root 'ProcessIdProperty' $process.Id 'Children') } 20)) { throw 'the app opened no window' }
    $script:window = Find $root 'ProcessIdProperty' $process.Id 'Children'
    $script:handle = [IntPtr]$script:window.Current.NativeWindowHandle

    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'Neap.Pretend', [System.IO.Pipes.PipeDirection]::InOut)
    $pipe.Connect(10000)
    $script:reader = New-Object System.IO.StreamReader($pipe)
    $script:writer = New-Object System.IO.StreamWriter($pipe)
    $script:writer.AutoFlush = $true
    $script:registry = @(Ask 'registry')
    $script:everything = New-Object System.Collections.Generic.List[object]
    $script:covered = @{}

    Write-Host 'Connecting'
    Check (Until { (Header) -eq 'Headset connected' } 20) 'the app reads the pretend headset as connected'
    Check ($script:window.Current.Name -like '*pretend*') 'the window says it is a pretend run'

    Write-Host 'Pages'
    foreach ($page in 'Home', 'Audio', 'Microphone', 'Controls', 'Device', 'Settings') {
        Page $page
        Start-Sleep -Seconds 1
        Screenshot $page.ToLowerInvariant()
    }

    Write-Host 'Settings'
    foreach ($page in 'Audio', 'Microphone', 'Controls', 'Device') { Settings $page }
    $missed = @($script:registry | Where-Object { $_.writable -and $_.kind -ne 'Text' -and -not $script:covered[$_.name] } | ForEach-Object { $_.name })
    Write-Host "  note  writable settings with no control on these pages: $($missed -join ', ')"

    Write-Host 'Equaliser'
    Band 'Audio' '1 kHz' '0x1270'
    Preset 'Audio' 'Vocal Boost' '0x1210' '4'
    Band 'Microphone' '1 kHz' '0x1370'
    Preset 'Microphone' 'Clarity' '0x1310' '3'

    Write-Host 'Windows'
    WindowsOwned

    Write-Host 'Chat wheel'
    Wheel

    Write-Host 'Home'
    Quick

    Write-Host 'Headset'
    Battery
    PowerCycle

    Write-Host 'Everything sent'
    Collect
    $refusals = @(Ask 'refusals')
    Check ($refusals.Count -eq 0) "the pretend headset refused nothing$(if ($refusals.Count) { ': ' + ($refusals | ForEach-Object { $_.reason }) -join '; ' })"
    Check (@($script:everything | Where-Object { (Number $_.key) -in 0x400, 0x420, 0x440, 0x460 }).Count -eq 0) 'no transmitter slot base address was written'
    Write-Host "  note  $($script:everything.Count) writes in all"

    Write-Host 'Audio format'
    Format
    if (-not $Unattended) { Check ($script:tookFocus -eq 0) 'the app never came to the front' }
}
finally {
    if ($pipe) { $pipe.Dispose() }
    if (-not $KeepOpen) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
}

Write-Host ''
Write-Host "$($script:passes) passed, $($script:failures.Count) failed. Screenshots in $Out"
if ($script:failures.Count -gt 0) { exit 1 }
