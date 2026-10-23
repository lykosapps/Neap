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
checks the control follows. Each control is found by its automation id, the
setting's registry name: a switch, a slider, a drop-down, or a segmented
control or list of choices, which is a group of radio buttons. It also covers
the options a switch opens while it is on, the equaliser page's Game and
Microphone tabs, bands and presets, starting and deleting a
preset, Home's tiles and its way to the equaliser, the Windows-owned
volume, mute and format controls (which must write nothing to the
headset), the chat wheel driving the mix, the battery, the Charging Dock's
spare battery, profiles and switching them as apps start and close, and
the headset switching off and on. At the end nothing
may have been refused: no unconfirmed key, no value out of range, no slot
base address, no firmware command.

A screenshot of every page goes in the output folder. Exits 1 if any
check fails.

The app opens with --behind, beneath every other window. Moving between
pages through UI Automation still brings it to the front, so the run stops
the moment it does rather than keep taking keys from whoever is typing:
run it while nobody is using the machine.

Publish first:

    dotnet publish src/Neap.Desktop -c Release -r win-x64 -f net10.0-windows10.0.19041.0 --self-contained

.PARAMETER Exe
The published app.

.PARAMETER Out
Where to put the screenshots.

.PARAMETER KeepOpen
Leave the app running afterwards.

.PARAMETER Unattended
Nobody is at the machine, so the app may come to the front without the run
stopping.

.PARAMETER Theme
Show the app in Light or Dark, whatever Windows is set to. Left out, it
follows Windows.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\pretend.ps1
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\Neap.Desktop\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\Neap.Desktop.exe'),
    [string]$Out = (Join-Path $env:TEMP 'neap-pretend'),
    [switch]$KeepOpen,
    [switch]$Unattended,
    [ValidateSet('', 'Light', 'Dark')]
    [string]$Theme = ''
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

# A section's heading, with how long the one before it took.
$script:sectionClock = $null
function Section([string]$name) {
    if ($script:sectionClock) { Write-Host ("  time  {0:0.0}s" -f $script:sectionClock.Elapsed.TotalSeconds) }
    $script:sectionClock = [Diagnostics.Stopwatch]::StartNew()
    Write-Host $name
}

function Pass([string]$what) { $script:passes++; Write-Host "  ok    $what" }
function Fail([string]$what) { $script:failures.Add($what); Write-Host "  FAIL  $what" -ForegroundColor Red }
function Check([bool]$condition, [string]$what) { if ($condition) { Pass $what } else { Fail $what } }

# Samples a condition until it holds or the time runs out: one reading after
# the fact says where a value settled, not whether it moved.
# A condition that meets a control not there yet, such as one on a page still
# being drawn, is "not yet" rather than an error that ends the walk.
function Until([scriptblock]$condition, [double]$seconds = 3) {
    $met = { try { [bool](& $condition) } catch { $false } }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt $seconds) {
        Watch
        if (& $met) { return $true }
        Start-Sleep -Milliseconds 100
    }
    return (& $met)
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

# A search made while a page is being replaced can meet elements already gone,
# which UI Automation reports as a failure; that is "not there yet", and the
# callers look again.
function FindAll($under, [string]$property, $value) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($A::$property, $value)
    try { @($under.FindAll($Scopes::Descendants, $condition)) }
    catch [System.Runtime.InteropServices.COMException] { @() }
}

# The app's windows other than the main one that are on screen. A menu, a
# drop-down's list and a flyout are each a window of their own, outside the
# main window's tree.
function Popups {
    $own = New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $process.Id)
    @($A::RootElement.FindAll($Scopes::Children, $own)) |
        Where-Object { $_.Current.NativeWindowHandle -ne $script:handle -and -not $_.Current.IsOffscreen }
}

# What has this property in the main window, or else in a popup.
function Everywhere([string]$property, $value) {
    $hits = @(FindAll $script:window $property $value)
    if ($hits.Count -gt 0) { return $hits }
    foreach ($popup in Popups) { $hits += @(FindAll $popup $property $value) }
    return $hits
}

# The first control of a type with this name; its label often shares the name.
function Control([string]$name, $type) {
    Everywhere 'NameProperty' $name | Where-Object { $_.Current.ControlType -eq $type } |
        Select-Object -First 1
}

function Pattern($element, $pattern) { $element.GetCurrentPattern($pattern::Pattern) }

# The groups the app reads all the time while it is connected: the state it
# checks every beat, the groups it watches, and the transmitter slots it
# looks over now and then. A page never waits on these, which never stop.
$script:background = @('GSI', 'Inf', 'SAF', 'Mic', 'TX', 'TX1', 'TX2', 'TX3', 'TX4')

# What the app has asked the headset to do beyond its background reading.
function Busy {
    $reads = Ask 'reads'
    $count = @(Writes).Count
    foreach ($group in $reads.PSObject.Properties) { if ($group.Name -notin $script:background) { $count += $group.Value } }
    $count
}

# Opens a page and waits for the app to stop asking the headset things. A
# page with an equaliser reads the preset slots on arrival, an empty one
# taking a second, and a write made meanwhile waits behind them. Only what
# the page asks counts: the app's background reading goes on all the time.
function Page([string]$name) {
    $script:step = "the $name page"
    $items = FindAll $script:window 'NameProperty' $name |
        Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem }
    (Pattern $items[0] ([System.Windows.Automation.SelectionItemPattern])).Select()
    Until { @(FindAll $script:window 'NameProperty' $name | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Text }).Count -gt 0 } | Out-Null
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $count = Busy
    $still = [Diagnostics.Stopwatch]::StartNew()
    while ($still.Elapsed.TotalSeconds -lt 1 -and $clock.Elapsed.TotalSeconds -lt 10) {
        Watch
        Start-Sleep -Milliseconds 100
        $now = Busy
        if ($now -ne $count) { $count = $now; $still.Restart() }
    }
}

# Opens the Equaliser page and puts one bank in front: Game or Microphone.
function Bank([string]$name) {
    Page 'Equaliser'
    $script:step = "the $name equaliser"
    $tab = Control $name ([System.Windows.Automation.ControlType]::RadioButton)
    if ($null -eq $tab) { Fail "the Equaliser page has a $name tab"; return }
    (Pattern $tab ([System.Windows.Automation.SelectionItemPattern])).Select()
    Start-Sleep -Milliseconds 500
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

# The radio buttons of a setting shown with every option in view.
function Segments($element) {
    FindAll $element 'ControlTypeProperty' ([System.Windows.Automation.ControlType]::RadioButton)
}

function Kind($element) {
    $type = $element.Current.ControlType
    if ($type -eq [System.Windows.Automation.ControlType]::Slider) { return 'slider' }
    if ($type -eq [System.Windows.Automation.ControlType]::ComboBox) { return 'choice' }
    if (@(Segments $element).Count -gt 0) { return 'segments' }
    return 'toggle'
}

# The items of a list, which are in the list's own tree or, for an opened
# drop-down, in the window it opens.
function Items($element) {
    $type = [System.Windows.Automation.ControlType]::ListItem
    $own = @(FindAll $element 'ControlTypeProperty' $type)
    if ($own.Count -gt 0) { return $own }
    foreach ($popup in Popups) { FindAll $popup 'ControlTypeProperty' $type }
}

# $label may end in * for an option whose name goes on, such as an app
# listed as playing.
function Item($element, [string]$label) {
    Items $element | Where-Object { $_.Current.Name -like $label } | Select-Object -First 1
}

# An opened list fills in its items a moment later, so both of these wait
# for them rather than look once.
function Options($element) {
    $expand = Pattern $element ([System.Windows.Automation.ExpandCollapsePattern])
    $expand.Expand()
    Until { @(Items $element).Count -gt 0 } | Out-Null
    $names = @(Items $element | Where-Object { $_.Current.IsEnabled } | ForEach-Object { $_.Current.Name })
    $expand.Collapse()
    Start-Sleep -Milliseconds 200
    return $names
}

function Choose($element, [string]$label) {
    $expand = Pattern $element ([System.Windows.Automation.ExpandCollapsePattern])
    $expand.Expand()
    # A list opened just after it closed can miss the opening; it is opened
    # once more before the option is called missing.
    if (-not (Until { $null -ne (Item $element $label) })) {
        $expand.Collapse()
        Start-Sleep -Milliseconds 300
        $expand.Expand()
    }
    if (-not (Until { $null -ne (Item $element $label) })) { $expand.Collapse(); throw "no option '$label' in $($element.Current.Name)" }
    $item = Item $element $label
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
        'segments' {
            $chosen = Segments $element | Where-Object { (Pattern $_ ([System.Windows.Automation.SelectionItemPattern])).Current.IsSelected }
            if ($chosen) { @($chosen)[0].Current.Name } else { '' }
        }
        default { [int]((Pattern $element ([System.Windows.Automation.TogglePattern])).Current.ToggleState -eq 'On') }
    }
}

function Operate($element, $value) {
    switch (Kind $element) {
        'slider' { (Pattern $element ([System.Windows.Automation.RangeValuePattern])).SetValue([double]$value) }
        'choice' { Choose $element $value }
        'segments' {
            $item = Segments $element | Where-Object { $_.Current.Name -eq $value } | Select-Object -First 1
            (Pattern $item ([System.Windows.Automation.SelectionItemPattern])).Select()
        }
        default { (Pattern $element ([System.Windows.Automation.TogglePattern])).Toggle() }
    }
}

# The registry names each option in English for the probe and the log, and
# the screen says a few of them in other words.
$script:spoken = @{ 'Mic monitoring volume' = 'Microphone monitoring volume' }

function Spoken([string]$label) {
    if ($script:spoken.ContainsKey($label)) { return $script:spoken[$label] }
    return $label
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
        { $_ -in 'choice', 'segments' } {
            $labels = @($setting.options.PSObject.Properties | ForEach-Object { Spoken $_.Value })
            return @($labels | Where-Object { $_ -ne $shown })[0]
        }
        default { return 1 - $shown }
    }
}

# The value a label or a toggle state goes out as.
function Wire($setting, $value, [string]$kind) {
    if ($kind -in 'choice', 'segments') {
        return ($setting.options.PSObject.Properties | Where-Object { (Spoken $_.Value) -eq $value }).Name
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
    $back = if ($kind -in 'choice', 'segments') { $shown } else { Another $setting $want $kind }
    $backWire = Wire $setting $back $kind
    try { Ask "report $($setting.key) $backWire" | Out-Null }
    catch { Write-Host "  note  $name is set only from the app: $($_.Exception.Message)"; return }
    Check (Until { [string](Shown $element) -eq [string]$back }) "$name follows the headset to $backWire"
    Check (@(Writes).Count -eq 0) "following the headset, $name sends nothing back"
    Collect
}

# Opens every expander on the page, so the settings folded inside one can
# be found. Drop-downs are left alone: opening one is choosing from it.
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

# Each band is a text field named for its frequency, in decibels. What is
# typed is taken when the field is left, so focus goes into the field and
# then to the button that starts a new preset, which does nothing until pressed.
function Band([string]$page, [string]$label, [string]$key) {
    Bank $page
    $band = Control $label ([System.Windows.Automation.ControlType]::Edit)
    if ($null -eq $band) { Fail "$page has an equaliser band $label"; return }
    $away = Control 'Start a new preset' ([System.Windows.Automation.ControlType]::Button)
    if ($null -eq $away) { Fail "the $page equaliser has a button to leave the band for"; return }

    Ask 'clear' | Out-Null
    $band.SetFocus()
    (Pattern $band ([System.Windows.Automation.ValuePattern])).SetValue('3')
    $away.SetFocus()
    Check (Until { @(Writes | Where-Object { $_.key -eq $key -and $_.value -eq '30' }).Count -gt 0 }) "$page $label band sends $key=30"
    Collect
}

# The presets are a list beside the curve, each a button named for its preset.
function Preset([string]$page, [string]$name, [string]$key, [string]$id) {
    Bank $page
    $find = { Control $name ([System.Windows.Automation.ControlType]::Button) }
    if (-not (Until { $null -ne (& $find) })) { Fail "$page lists the preset $name"; return }
    $button = & $find

    Ask 'clear' | Out-Null
    (Pattern $button ([System.Windows.Automation.InvokePattern])).Invoke()
    Check (Until { @(Writes | Where-Object { $_.key -eq $key -and $_.value -eq $id }).Count -gt 0 }) "$page preset $name sends $key=$id"
    Check (Until { (& $find).Current.ItemStatus -eq 'Selected' }) "$page marks the preset $name as selected"
    Collect
}

# A switch's options open in a drawer only while it is on, and the walk over
# each page leaves every switch where it found it, so it never sees them.
# The headset turns the switch on by itself, each option is exercised, and
# the headset turns it off again.
function Drawer([string]$page, [string]$switch, [string[]]$options) {
    Page $page
    $script:step = "the options under $switch"
    Ask "report $switch 1" | Out-Null
    foreach ($name in $options) {
        $setting = $script:registry | Where-Object { $_.name -eq $name }
        if (-not (Until { $null -ne ($script:found = Find $script:window 'AutomationIdProperty' $name) })) {
            Fail "$page shows $name while $switch is on"
            continue
        }
        $script:covered[$name] = $true
        Exercise $script:found $setting
    }
    Ask "report $switch 0" | Out-Null
    Check (Until { $null -eq (Find $script:window 'AutomationIdProperty' $options[0]) }) "$page hides the options under $switch while it is off"
    Collect
}

# Starting a preset from flat, and deleting one, from the list beside the
# curve. Deleting asks first, in place.
function PresetTools {
    Bank 'Game'
    $script:step = 'the preset tools on the Equaliser page'

    Ask 'clear' | Out-Null
    $new = { Control 'Start a new preset' ([System.Windows.Automation.ControlType]::Button) }
    if (-not (Until { $null -ne (& $new) })) { Fail 'the preset list offers a new preset'; return }
    (Pattern (& $new) ([System.Windows.Automation.InvokePattern])).Invoke()
    Check (Until { (Find $script:window 'AutomationIdProperty' 'PresetName').Current.Name -eq 'New preset' }) 'New preset starts a preset from flat'
    Collect

    $delete = { Control 'Delete Night Raid' ([System.Windows.Automation.ControlType]::Button) }
    if (-not (Until { $null -ne (& $delete) })) { Fail 'the preset list offers to delete Night Raid'; return }
    (Pattern (& $delete) ([System.Windows.Automation.InvokePattern])).Invoke()
    Check (Until { $null -ne (Control 'Keep Night Raid' ([System.Windows.Automation.ControlType]::Button)) }) 'deleting Night Raid asks first'
    Check (@(Writes | Where-Object { $_.key -eq '0x1610' }).Count -eq 0) 'asking deletes nothing'
    (Pattern (& $delete) ([System.Windows.Automation.InvokePattern])).Invoke()
    Check (Until { @(Writes | Where-Object { $_.key -eq '0x1610' }).Count -gt 0 }) 'confirming deletes Night Raid on the headset'
    # Deleting reads the bank back, a second for each empty slot.
    Check (Until { $null -eq (Control 'Night Raid' ([System.Windows.Automation.ControlType]::Button)) } 10) 'Night Raid leaves the list'
    Collect
}

# Superhuman Hearing's tile on Home, and the way from Home's equaliser tile
# to the equaliser itself, from its Equaliser settings link.
function HomeExtras {
    Page 'Home'
    $script:step = 'Superhuman Hearing on Home'
    $tile = Find $script:window 'AutomationIdProperty' 'superhuman_hearing'
    if ($null -eq $tile) { Fail 'Home has a Superhuman Hearing tile' }
    else { Exercise $tile ($script:registry | Where-Object { $_.name -eq 'superhuman_hearing' }) }

    $script:step = 'Equaliser settings on Home'
    $open = { Control 'Equaliser settings' ([System.Windows.Automation.ControlType]::Button) }
    if ($null -eq (& $open)) { Fail 'the equaliser tile has an Equaliser settings link'; return }
    (Pattern (& $open) ([System.Windows.Automation.InvokePattern])).Invoke()
    $equaliser = {
        FindAll $script:window 'NameProperty' 'Equaliser' |
            Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem } | Select-Object -First 1
    }
    Check (Until { (Pattern (& $equaliser) ([System.Windows.Automation.SelectionItemPattern])).Current.IsSelected }) 'Equaliser settings goes to the Equaliser page'
    $game = { Control 'Game' ([System.Windows.Automation.ControlType]::RadioButton) }
    Check (Until { $tab = & $game; $null -ne $tab -and (Pattern $tab ([System.Windows.Automation.SelectionItemPattern])).Current.IsSelected }) 'Equaliser settings lands on the Game tab'
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

function Format {
    Page 'Device'
    $format = Control 'Headset output' ([System.Windows.Automation.ControlType]::ComboBox)
    $before = Ask 'format output'
    $other = @(Options $format | Where-Object { $_ -ne (Shown $format) })[0]
    Choose $format $other
    $moved = Until { $process.HasExited -or (Ask 'format output') -ne $before } 8
    if ($process.HasExited) { throw 'the app crashed choosing an output format' }
    Check $moved "the output format moves off $before"
    Check (@(Writes).Count -eq 0) 'the format writes nothing to the headset'
    Collect
}

# Home's "Chat comes from" field: a button naming the chosen apps, whose list
# opens over the page, so choosing never moves the card.
function Wheel {
    Page 'Home'
    $chooser = Everywhere 'ControlTypeProperty' ([System.Windows.Automation.ControlType]::Button) |
        Where-Object { $_.Current.Name -like 'Chat comes from*' } | Select-Object -First 1
    if ($null -eq $chooser) { Fail 'Home offers a choice of chat apps'; return }
    Check ($chooser.Current.Name -eq 'Chat comes from No app chosen yet') "with no chat app chosen, the field says so: $($chooser.Current.Name)"
    $top = $chooser.Current.BoundingRectangle.Top
    (Pattern $chooser ([System.Windows.Automation.InvokePattern])).Invoke()
    $box = { param($name) Everywhere 'ControlTypeProperty' ([System.Windows.Automation.ControlType]::CheckBox) | Where-Object { $_.Current.Name -like $name } | Select-Object -First 1 }
    Until { $null -ne (& $box 'Pretend Chat*') } | Out-Null
    $chat = & $box 'Pretend Chat*'
    $music = & $box 'Pretend Music'
    if ($null -eq $chat -or $null -eq $music) { Fail 'the list offers the pretend apps'; return }
    Screenshot 'home-mix-choose'
    (Pattern $chat ([System.Windows.Automation.TogglePattern])).Toggle()
    Check (Until { $null -ne (Ask 'mix') } 5) 'choosing a chat app starts the mix'
    (Pattern $music ([System.Windows.Automation.TogglePattern])).Toggle()
    Check (Until { $chooser.Current.Name -eq 'Chat comes from Pretend Chat and Pretend Music' }) "a second app joins the first: $($chooser.Current.Name)"
    Check ($chooser.Current.BoundingRectangle.Top -eq $top) 'choosing apps does not move the field'
    Screenshot 'home-mix-two'
    (Pattern $chat ([System.Windows.Automation.TogglePattern])).Toggle()
    (Pattern $music ([System.Windows.Automation.TogglePattern])).Toggle()
    Check (Until { $null -eq (Ask 'mix') } 5) 'clearing the last chat app stops the mix'
    (Pattern $chat ([System.Windows.Automation.TogglePattern])).Toggle()
    Check (Until { $null -ne (Ask 'mix') } 5) 'choosing it again starts the mix again'
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

# The microphone card on Home, whose switch mutes the microphone in Windows and
# writes nothing to the headset, and is disabled while the boom arm is up.
function MicTile {
    $script:step = 'the microphone tile on Home'
    $tile = Control 'Microphone' ([System.Windows.Automation.ControlType]::Button)
    if ($null -eq $tile) { Fail 'Home has a microphone tile'; return }
    $toggle = Pattern $tile ([System.Windows.Automation.TogglePattern])
    Check (Until { $tile.Current.IsEnabled -and $toggle.Current.ToggleState -eq 'On' }) 'with the arm down and Windows unmuted, the microphone tile is live'

    Ask 'clear' | Out-Null
    $toggle.Toggle()
    Check (Until { (Ask 'volume input').muted }) 'the microphone tile mutes the microphone in Windows'
    Check (Until { $toggle.Current.ToggleState -eq 'Off' }) 'the microphone tile shows it muted'
    $toggle.Toggle()
    Check (Until { -not (Ask 'volume input').muted }) 'the microphone tile unmutes it again'
    Check (@(Writes).Count -eq 0) 'the microphone tile writes nothing to the headset'
    Collect

    Ask 'report mic_muted 1' | Out-Null
    Check (Until { -not $tile.Current.IsEnabled -and $toggle.Current.ToggleState -eq 'Off' }) 'with the arm up, the microphone tile is out and disabled'
    Check ($null -ne (Find $script:window 'NameProperty' 'Muted by the boom arm')) 'the microphone card says the boom arm muted it'
    Ask 'report mic_muted 0' | Out-Null
    Check (Until { $tile.Current.IsEnabled -and $toggle.Current.ToggleState -eq 'On' }) 'with the arm down again, the microphone tile is live'
    Check (@(Writes).Count -eq 0) 'following the arm, the microphone tile sends nothing'
    Collect
}

function Quick {
    Page 'Home'
    MicTile

    # The equaliser tile opens a list of presets in a flyout, which is a
    # window of its own, so the preset is looked for in the popups too.
    $script:step = 'the preset list on Home'
    $tile = Find $script:window 'AutomationIdProperty' 'PresetPicker'
    if ($null -eq $tile) { Fail 'Home has an equaliser tile'; return }
    Ask 'clear' | Out-Null
    (Pattern $tile ([System.Windows.Automation.InvokePattern])).Invoke()
    $preset = {
        Everywhere 'NameProperty' 'Signature Sound' |
            Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem } | Select-Object -First 1
    }
    if (-not (Until { $null -ne (& $preset) })) { Fail 'the equaliser tile lists Signature Sound'; return }
    $item = & $preset
    (Pattern $item ([System.Windows.Automation.SelectionItemPattern])).Select()
    Check (Until { @(Writes | Where-Object { $_.key -eq '0x1210' -and $_.value -eq '1' }).Count -gt 0 }) 'the preset list on Home sends 0x1210=1'
    Collect
}

# One step of noise control: what it does, then where the headset must end
# up, touching nothing but noise cancellation and its level.
function NoiseStep([scriptblock]$do, [string]$anc, [string]$level, [string]$what) {
    $script:step = $what
    Ask 'clear' | Out-Null
    & $do
    Check (Until { (Ask 'value 0x750') -eq $anc -and (Ask 'value 0x760') -eq $level }) "$what leaves the headset at 0x750=$anc, 0x760=$level"
    Check (@(Writes | Where-Object { $_.key -notin '0x750', '0x760' }).Count -eq 0) "$what writes nothing else"
    Collect
}

# Noise cancellation, transparency and off: chosen on Audio, stepped through
# on Home, and cycled by the Mode button, whose presses the pretend headset
# reports as noise cancellation turning on and off by itself.
function NoiseControl {
    Page 'Audio'
    $modes = Find $script:window 'AutomationIdProperty' 'noise_control'
    if ($null -eq $modes) { Fail 'Audio offers noise control'; return }
    if ((Ask 'value 0x750') -ne '1') { Fail 'the pretend headset starts with noise cancellation on'; return }
    $level = Ask 'value 0x760'
    NoiseStep { Operate $modes 'Transparency' } '1' '0' 'choosing Transparency on Audio'
    Check ($null -eq (Find $script:window 'AutomationIdProperty' 'anc_level')) 'Strength is hidden in transparency'
    NoiseStep { Operate $modes 'Off' } '0' $level 'choosing Off on Audio'
    NoiseStep { Operate $modes 'Cancelling' } '1' $level 'choosing Cancelling on Audio'
    Check (Until { $null -ne (Find $script:window 'AutomationIdProperty' 'anc_level') }) 'Strength is shown while cancelling'

    Page 'Home'
    $tile = Find $script:window 'AutomationIdProperty' 'home_noise_control'
    if ($null -eq $tile) { Fail 'Home has a noise cancellation tile'; return }
    NoiseStep { Operate $tile 'Transparency' } '1' '0' 'choosing Transparency on Home'
    NoiseStep { Operate $tile 'Off' } '0' $level 'choosing Off on Home'
    NoiseStep { Operate $tile 'Cancelling' } '1' $level 'choosing Cancelling on Home'

    Page 'Controls'
    $mode = Find $script:window 'AutomationIdProperty' 'mode_button_function'
    NoiseStep { Operate $mode 'Noise cancellation: cancelling, transparency and off' } '1' $level 'choosing the Mode button cycle'
    Check ($null -ne (Find $script:window 'NameProperty' 'Works while Neap is running. Otherwise the button turns noise cancellation on and off.')) 'the cycle says it needs Neap running'
    NoiseStep { Ask 'report anc 0' | Out-Null } '1' '0' 'a Mode press from noise cancellation'
    NoiseStep { Ask 'report anc 0' | Out-Null } '0' $level 'a Mode press from transparency'
    NoiseStep { Ask 'report anc 1' | Out-Null } '1' $level 'a Mode press from off'
    NoiseStep { Operate $mode 'Noise cancellation on/off' } '1' $level 'choosing noise cancellation on/off again'
    NoiseStep { Ask 'report anc 0' | Out-Null } '0' $level 'a Mode press without the cycle'
    Ask 'report anc 1' | Out-Null
    Screenshot 'controls-mode'
}

# A battery reading that rises is shown as it is; one that falls fast is a new
# battery settling, and Home says so rather than show a number (BatteryTrend).
function Battery {
    Page 'Home'
    Ask 'report battery 90' | Out-Null
    Check (Until { $null -ne (Find $script:window 'NameProperty' '90%') }) 'Home shows the battery the headset reports'
    Ask 'report battery 40' | Out-Null
    Check (Until { $null -ne (Find $script:window 'NameProperty' "Reading$([char]0x2026)") }) 'a battery falling fast shows as still reading, not as a number'
}

function Spare {
    Page 'Device'
    $shown = { (Find $script:window 'AutomationIdProperty' 'SpareBattery').Current.Name }
    # A spare short of full is charging, and its figure runs high meanwhile,
    # so the word stands in for the number until it is full.
    Check (Until { (& $shown) -eq 'Charging' }) 'the Charging Dock shows its spare battery charging'
    Screenshot 'device-spare'
    Ask 'spare empty' | Out-Null
    Check (Until { (& $shown) -eq 'Empty' }) 'the spare taken out shows at once as an empty slot'
    Ask 'spare 30' | Out-Null
    Check (Until { (& $shown) -eq 'Charging' }) 'a spare put back shows it charging'
    Ask 'spare 100' | Out-Null
    Check (Until { (& $shown) -eq '100%' }) 'a full spare shows its charge'
}

# The profile bar (no profile, an active one, edited), and the Profiles
# page: saving, editing after, renaming, assigning an app and
# deleting. The bar sits outside every page's frame, so it is looked for in
# the window rather than a particular page.
function Profiles {
    $script:step = 'the Profiles page with none saved'
    Page 'Profiles'
    Screenshot 'profiles-empty'

    $script:step = 'the profile bar with no profile'
    Page 'Home'
    $bar = Control 'Profile' ([System.Windows.Automation.ControlType]::Button)
    if ($null -eq $bar) { Fail 'the window has a profile bar'; return }
    Screenshot 'profile-none'

    $script:step = 'saving the current settings as a new profile'
    Ask 'clear' | Out-Null
    (Pattern $bar ([System.Windows.Automation.InvokePattern])).Invoke()
    $newButton = { Control 'New profile' ([System.Windows.Automation.ControlType]::Button) }
    if (-not (Until { $null -ne (& $newButton) -and (& $newButton).Current.IsEnabled })) { Fail 'the profile menu offers to save the current settings as a new profile'; return }
    Screenshot 'profile-flyout'
    (Pattern (& $newButton) ([System.Windows.Automation.InvokePattern])).Invoke()

    $field = { Control 'Profile name' ([System.Windows.Automation.ControlType]::Edit) }
    if (-not (Until { $null -ne (& $field) })) { Fail 'saving a new profile opens a name dialog'; return }
    # The flyout's own close animation can still be mid-flight the instant the
    # field exists, which shows both it and the dialog half-composited.
    Start-Sleep -Milliseconds 500
    Screenshot 'profile-new-dialog'
    (Pattern (& $field) ([System.Windows.Automation.ValuePattern])).SetValue('Gaming')
    $save = Control 'Save' ([System.Windows.Automation.ControlType]::Button)
    if ($null -eq $save) { Fail 'the name dialog has a Save button'; return }
    # Save is on only once there is a name to save.
    Until { $save.Current.IsEnabled } | Out-Null
    (Pattern $save ([System.Windows.Automation.InvokePattern])).Invoke()
    Check (Until { $null -ne (Control 'Gaming' ([System.Windows.Automation.ControlType]::TextBlock)) -or $null -ne (Find $script:window 'NameProperty' 'Gaming') }) 'saving names the profile Gaming'
    Start-Sleep -Milliseconds 300
    Screenshot 'profile-active'

    $script:step = 'the profile bar once something no longer matches it'
    Page 'Audio'
    $ai = Find $script:window 'AutomationIdProperty' 'ai_noise_reduction'
    if ($null -eq $ai) { Fail 'Audio has a noise reduction control'; return }
    Operate $ai $null
    Page 'Home'
    Check (Until { $null -ne (Find $script:window 'NameProperty' 'Edited') }) 'changing a setting the profile holds marks it edited'
    Screenshot 'profile-edited'

    # Save and Discard live in the profile menu, and Discard is there only
    # while something is edited, so the menu is opened to find it.
    (Pattern $bar ([System.Windows.Automation.InvokePattern])).Invoke()
    Until { $null -ne (Control 'Discard' ([System.Windows.Automation.ControlType]::Button)) } | Out-Null
    $discard = Control 'Discard' ([System.Windows.Automation.ControlType]::Button)
    if ($null -ne $discard) {
        (Pattern $discard ([System.Windows.Automation.InvokePattern])).Invoke()
        Check (Until { $null -eq (Find $script:window 'NameProperty' 'Edited') }) 'discarding clears the edited mark'
    }
    else { Fail 'the bar offers Discard while edited' }

    $script:step = 'the Profiles page'
    Page 'Profiles'
    Screenshot 'profiles'

    $rename = Control 'Rename Gaming' ([System.Windows.Automation.ControlType]::Button)
    if ($null -ne $rename) {
        (Pattern $rename ([System.Windows.Automation.InvokePattern])).Invoke()
        if (Until { $null -ne (Control 'Profile name' ([System.Windows.Automation.ControlType]::Edit)) }) {
            Screenshot 'profile-rename-dialog'
            $cancelRename = Control 'Cancel' ([System.Windows.Automation.ControlType]::Button)
            if ($null -ne $cancelRename) { (Pattern $cancelRename ([System.Windows.Automation.InvokePattern])).Invoke() }
        }
        else { Fail 'renaming opens a name dialog' }
    }
    else { Fail 'the Gaming row offers to rename it' }

    $apps = Control 'Apps for Gaming' ([System.Windows.Automation.ControlType]::Button)
    if ($null -ne $apps) {
        (Pattern $apps ([System.Windows.Automation.InvokePattern])).Invoke()
        Until { @(FindAll $script:window 'ControlTypeProperty' ([System.Windows.Automation.ControlType]::CheckBox)).Count -gt 0 } | Out-Null
        Start-Sleep -Milliseconds 300
        Check ($null -ne (Control "Browse for a program$([char]0x2026)" ([System.Windows.Automation.ControlType]::Button))) 'the apps picker offers to browse for a program'
        Screenshot 'profile-apps-dialog'
        $box = @(FindAll $script:window 'ControlTypeProperty' ([System.Windows.Automation.ControlType]::CheckBox)) | Select-Object -First 1
        if ($null -ne $box) { (Pattern $box ([System.Windows.Automation.TogglePattern])).Toggle() }
        # The check mark has its own brief animation; screenshotting the
        # instant the toggle call returns catches it still fading in.
        Start-Sleep -Milliseconds 400
        Screenshot 'profile-apps-checked'
        $ok = Control 'OK' ([System.Windows.Automation.ControlType]::Button)
        if ($null -ne $ok) { (Pattern $ok ([System.Windows.Automation.InvokePattern])).Invoke() }
        Check (Until { $null -eq (Find $script:window 'NameProperty' 'Not assigned to any app') }) 'checking an app in the picker assigns it'
        Start-Sleep -Milliseconds 300
        Screenshot 'profiles-assigned'
    }
    else { Fail 'the Gaming row offers to choose its apps' }

    $delete = Control 'Delete Gaming' ([System.Windows.Automation.ControlType]::Button)
    if ($null -ne $delete) {
        (Pattern $delete ([System.Windows.Automation.InvokePattern])).Invoke()
        if (Until { $null -ne (Control 'Keep Gaming' ([System.Windows.Automation.ControlType]::Button)) }) {
            Screenshot 'profile-delete-confirm'
            (Pattern (Control 'Keep Gaming' ([System.Windows.Automation.ControlType]::Button)) ([System.Windows.Automation.InvokePattern])).Invoke()
        }
        else { Fail 'deleting Gaming asks first' }
    }
    else { Fail 'the Gaming row offers to delete it' }
    Collect
}

# Switching as apps start and close. Profiles leaves Gaming on, with Pretend
# Chat assigned and running; this adds Desk as the default, then closes and
# starts Pretend Chat through the pretend Windows, then sets it to switch
# only while it is using the microphone and starts and stops it recording.
function AutoSwitching {
    $script:step = 'switching profiles as apps start and close'
    Page 'Profiles'
    $bar = Control 'Profile' ([System.Windows.Automation.ControlType]::Button)
    $on = { $bar.Current.ItemStatus -replace ', Edited$', '' }
    (Pattern $bar ([System.Windows.Automation.InvokePattern])).Invoke()
    $newButton = { Control 'New profile' ([System.Windows.Automation.ControlType]::Button) }
    if (-not (Until { $null -ne (& $newButton) -and (& $newButton).Current.IsEnabled })) { Fail 'the profile bar offers to save Desk'; return }
    (Pattern (& $newButton) ([System.Windows.Automation.InvokePattern])).Invoke()
    if (-not (Until { $null -ne (Control 'Profile name' ([System.Windows.Automation.ControlType]::Edit)) })) { Fail 'saving Desk opens a name dialog'; return }
    Start-Sleep -Milliseconds 500
    (Pattern (Control 'Profile name' ([System.Windows.Automation.ControlType]::Edit)) ([System.Windows.Automation.ValuePattern])).SetValue('Desk')
    $save = Control 'Save' ([System.Windows.Automation.ControlType]::Button)
    Until { $save.Current.IsEnabled } | Out-Null
    (Pattern $save ([System.Windows.Automation.InvokePattern])).Invoke()
    Check (Until { (& $on) -eq 'Desk' }) 'saving Desk puts it on'

    $default = Control 'Default profile' ([System.Windows.Automation.ControlType]::ComboBox)
    if ($null -eq $default) { Fail 'the Profiles page offers a default profile'; return }
    Choose $default 'Desk'
    Check (Until { (Shown $default) -eq 'Desk' }) 'Desk is the default profile'
    Screenshot 'profiles-default'

    Ask 'stop PretendChat' | Out-Null
    Start-Sleep -Seconds 4
    Check ((& $on) -eq 'Desk') 'Desk, chosen by hand while Pretend Chat ran, stays on when it closes'
    Ask 'start PretendChat' | Out-Null
    Check (Until { (& $on) -eq 'Gaming' } 8) 'Pretend Chat starting puts its profile, Gaming, on'
    # The row of the profile in use says so on its apps button, and the mark moves with the switch.
    $inUse = { param($name) $b = Control "Apps for $name" ([System.Windows.Automation.ControlType]::Button); $null -ne $b -and $b.Current.ItemStatus -eq 'In use' }
    Check (Until { (& $inUse 'Gaming') -and -not (& $inUse 'Desk') } 8) 'the Gaming row says In use, and the Desk row does not'
    Ask 'stop PretendChat' | Out-Null
    Check (Until { (& $on) -eq 'Desk' } 8) 'Pretend Chat closing puts the default, Desk, back on'
    Ask 'start PretendChat' | Out-Null
    Check (Until { (& $on) -eq 'Gaming' } 8) 'Pretend Chat starting again puts Gaming back on'

    $script:step = 'switching profiles as an app uses the microphone'
    $apps = Control 'Apps for Gaming' ([System.Windows.Automation.ControlType]::Button)
    if ($null -eq $apps) { Fail 'the Gaming row offers to choose its apps'; return }
    (Pattern $apps ([System.Windows.Automation.InvokePattern])).Invoke()
    $when = { Control 'When Pretend Chat switches' ([System.Windows.Automation.ControlType]::ComboBox) }
    if (-not (Until { $null -ne (& $when) })) { Fail 'the apps picker asks when Pretend Chat switches'; return }
    Check ((Shown (& $when)) -eq "While it's open") 'Pretend Chat switches while it is open until told otherwise'
    Choose (& $when) "While it's using the microphone"
    Screenshot 'profile-apps-microphone'
    (Pattern (Control 'OK' ([System.Windows.Automation.ControlType]::Button)) ([System.Windows.Automation.InvokePattern])).Invoke()
    # The row's line of apps is drawn; its apps button says it to a screen reader.
    $appsButton = { Control 'Apps for Gaming' ([System.Windows.Automation.ControlType]::Button) }
    Check (Until { $b = & $appsButton; $null -ne $b -and $b.Current.HelpText -eq 'Pretend Chat (while using the microphone)' }) 'the Gaming row says Pretend Chat switches on the microphone'
    Check (Until { (& $on) -eq 'Desk' } 8) 'Pretend Chat open but off the microphone puts the default, Desk, back on'
    Ask 'record PretendChat' | Out-Null
    Check (Until { (& $on) -eq 'Gaming' } 8) 'Pretend Chat using the microphone puts Gaming on'
    Ask 'unrecord PretendChat' | Out-Null
    Check (Until { (& $on) -eq 'Desk' } 8) 'Pretend Chat letting go of the microphone puts Desk back on'
    Collect
}

function PowerCycle {
    Ask 'off' | Out-Null
    Check (Until { (Header) -ne 'Headset connected' } 40) "switched off, the header says $(Header)"
    Screenshot 'home-off'
    $script:step = 'the profile bar with the headset off'
    $bar = Control 'Profile' ([System.Windows.Automation.ControlType]::Button)
    if ($null -ne $bar) {
        (Pattern $bar ([System.Windows.Automation.InvokePattern])).Invoke()
        Check (Until { @(Everywhere 'NameProperty' "Neap can't reach the headset's settings.").Count -gt 0 }) 'with the headset off, the profile list says why it is unavailable'
        $save = Control 'New profile' ([System.Windows.Automation.ControlType]::Button)
        Check ($null -ne $save -and -not $save.Current.IsEnabled) 'with the headset off, saving a new profile is not offered'
        Screenshot 'profile-off'
        try { (Pattern $bar ([System.Windows.Automation.ExpandCollapsePattern])).Collapse() } catch { }
    }
    else { Fail 'the window has a profile bar while the headset is off' }
    Ask 'on' | Out-Null
    Check (Until { (Header) -eq 'Headset connected' } 40) 'switched back on, the header says Headset connected'
}

# -- the run -------------------------------------------------------------------

if (-not (Test-Path $Exe)) { throw "no published app at $Exe; run dotnet publish src/Neap.Desktop -c Release -r win-x64 -f net10.0-windows10.0.19041.0 --self-contained" }
$Exe = (Resolve-Path $Exe).Path
$running = Get-CimInstance Win32_Process -Filter "Name = 'Neap.Desktop.exe'" | Where-Object { $_.CommandLine -like '*--pretend*' }
if ($running) { throw 'a pretend run is already open; quit it first' }

New-Item -ItemType Directory -Force -Path $Out | Out-Null
# A clean start: the pretend run's own folder, never the real app's.
Remove-Item (Join-Path $env:LOCALAPPDATA 'Neap\Pretend') -Recurse -Force -ErrorAction SilentlyContinue

$script:tookFocus = 0
$script:step = 'the launch'
# Behind every other window and never activated, so the run does not take
# the machine from whoever is using it.
$arguments = @('--pretend', '--behind') + @(if ($Theme) { "--$($Theme.ToLowerInvariant())" })
$process = Start-Process $Exe -ArgumentList $arguments -PassThru
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

    Section 'Connecting'
    Check (Until { (Header) -eq 'Headset connected' } 20) 'the app reads the pretend headset as connected'
    Check ($script:window.Current.Name -like '*pretend*') 'the window says it is a pretend run'

    Section 'Pages'
    foreach ($page in 'Home', 'Audio', 'Equaliser', 'Controls', 'Profiles', 'Device', 'Settings') {
        Page $page
        # A moment for the page's entrance to finish drawing.
        Start-Sleep -Milliseconds 400
        Screenshot $page.ToLowerInvariant()
    }

    Section 'Settings'
    foreach ($page in 'Audio', 'Controls', 'Device') { Settings $page }
    Drawer 'Audio' 'superhuman_hearing' 'shh_preset', 'shh_level'
    Drawer 'Audio' 'noise_gate' 'noise_gate_threshold'
    $missed = @($script:registry | Where-Object { $_.writable -and $_.kind -ne 'Text' -and -not $script:covered[$_.name] } | ForEach-Object { $_.name })
    Write-Host "  note  writable settings with no control on these pages: $($missed -join ', ')"

    Section 'Equaliser'
    Band 'Game' '1 kHz' '0x1270'
    Preset 'Game' 'Vocal Boost' '0x1210' '4'
    Band 'Microphone' '1 kHz' '0x1370'
    Preset 'Microphone' 'Clarity' '0x1310' '3'

    Section 'Windows'
    WindowsOwned
    Format

    Section 'Chat wheel'
    Wheel

    PresetTools

    Section 'Home'
    Quick
    HomeExtras

    Section 'Noise control'
    NoiseControl

    Section 'Profiles'
    Profiles
    AutoSwitching

    Section 'Headset'
    Battery
    Spare
    PowerCycle

    Section 'Everything sent'
    Collect
    $refusals = @(Ask 'refusals')
    Check ($refusals.Count -eq 0) "the pretend headset refused nothing$(if ($refusals.Count) { ': ' + ($refusals | ForEach-Object { $_.reason }) -join '; ' })"
    Check (@($script:everything | Where-Object { (Number $_.key) -in 0x400, 0x420, 0x440, 0x460 }).Count -eq 0) 'no transmitter slot base address was written'
    Write-Host "  note  $($script:everything.Count) writes in all"
    if (-not $Unattended) { Check ($script:tookFocus -eq 0) 'the app never came to the front' }
}
finally {
    if ($pipe) { $pipe.Dispose() }
    if (-not $KeepOpen) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
}

Section ''
Write-Host "$($script:passes) passed, $($script:failures.Count) failed. Screenshots in $Out"
if ($script:failures.Count -gt 0) { exit 1 }
