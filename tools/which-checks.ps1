<#
.SYNOPSIS
Says which checks a change needs, from the files it changed, and prints the
commands to run them.

.DESCRIPTION
Each changed file is matched against the rules below, in order; the first
rule that matches decides that file's checks. The commands are printed once
each, cheapest first, with the files that asked for them. A file no rule
knows is listed as unmapped rather than passed over, so the person choosing
can pick its checks by hand and add a rule.

The pretend walk is asked for only where a change can alter what a setting
control sends or how it follows the headset. Wording, layout and looks are
covered by the page's pictures and its own check.

.PARAMETER Since
Compare with this commit, such as main for a whole branch. By default the
uncommitted changes, or the last commit when there are none.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\which-checks.ps1

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\which-checks.ps1 -Since main
#>
param([string]$Since = '')
$root = Split-Path $PSScriptRoot -Parent

if ($Since) {
    $files = @(git -C $root diff --name-only "$Since...HEAD") + @(git -C $root diff --name-only HEAD) + @(git -C $root ls-files --others --exclude-standard)
}
else {
    $files = @(git -C $root diff --name-only HEAD) + @(git -C $root ls-files --others --exclude-standard)
    if ($files.Count -eq 0) { $files = @(git -C $root diff --name-only HEAD~1 HEAD) }
}
$files = @($files | Where-Object { $_ } | Sort-Object -Unique)
if ($files.Count -eq 0) { Write-Host 'nothing changed'; exit 0 }

# What each file needs. Check is the code check, Snap a page's pictures,
# Walk sections of the pretend walk (Whole for all of it), Ui a check in
# tools\ui. Notes are said once, as they are.
$rules = @(
    @{ Like = '^CHANGELOG\.md$'; Needs = @{ Check = 1; Ui = 'notes' } }
    @{ Like = '^(docs/|BACKLOG|DECISIONS|FINDINGS|README|CONTRIBUTING|CLAUDE|LICENSE|THIRD-PARTY|packaging/|\.tenon)|\.md$'; Needs = @{} }
    @{ Like = '^(\.editorconfig|global\.json)$'; Needs = @{ Check = 1 } }
    @{ Like = '^tests/'; Needs = @{ Check = 1 } }

    # Rules and the wiring under every setting.
    @{ Like = '^src/Neap\.Core/Settings/Registry\.cs$'; Needs = @{ Check = 1; Walk = 'Settings', 'Home', 'NoiseControl' } }
    @{ Like = '^src/Neap\.Core/(Settings|Protocol|Pretend)/|^src/Neap\.Core/HeadsetClient\.cs$|^src/Neap\.Services/(HeadsetService|Pretend)\.cs$'; Needs = @{ Check = 1; Walk = 'Whole' } }
    @{ Like = '^src/Neap\.Core/(Firmware/|Hid/Silent)'; Needs = @{ Check = 1; Ui = 'firmware' } }
    @{ Like = '^src/Neap\.Core/(Audio/Pulse/|Hid/(Hidraw|Udev|AccessPrompt)|Input/)'; Needs = @{ Check = 1; Note = 'Linux only: the tests cover it, and the pretend walk cannot' } }
    @{ Like = '^src/Neap\.Core/(Hid|Connection)/'; Needs = @{ Check = 1; Walk = 'Headset'; Note = 'it changes how Neap talks to the hardware: the headset-checks skill too' } }
    @{ Like = '^src/Neap\.Core/Presets/Parametric'; Needs = @{ Check = 1; Walk = 'Equaliser'; Ui = 'parametric-on', 'parametric-presets' } }
    @{ Like = '^src/Neap\.Core/Presets/|^src/Neap\.Services/(PresetService|GameEqBackups)\.cs$'; Needs = @{ Check = 1; Walk = 'Equaliser' } }
    @{ Like = '^src/Neap\.Services/Parametric(Service|Setup)\.cs$'; Needs = @{ Check = 1; Ui = 'parametric-on', 'parametric-presets' } }
    @{ Like = '^src/Neap\.Core/Profiles/|^src/Neap\.Services/(ProfileService|AutoSwitchService)\.cs$'; Needs = @{ Check = 1; Walk = 'Profiles'; Ui = 'profiles' } }
    @{ Like = '^src/Neap\.Core/Mix/|^src/Neap\.Services/(MixService|MixHotkeys|HotkeyService|X11Hotkeys)\.cs$'; Needs = @{ Check = 1; Walk = 'ChatWheel'; Ui = 'overlay' } }
    @{ Like = '^src/Neap\.Core/Audio/LoudnessBoost'; Needs = @{ Check = 1; Ui = 'loudness' } }
    @{ Like = '^src/Neap\.Core/Mini'; Needs = @{ Check = 1; Ui = 'mini' } }
    @{ Like = '^src/Neap\.Core/Audio/|^src/Neap\.Services/(AudioFormats|AudioRoute|SoundVolume)\.cs$'; Needs = @{ Check = 1; Walk = 'Windows' } }
    @{ Like = '^src/Neap\.Services/NoiseService\.cs$'; Needs = @{ Check = 1; Walk = 'NoiseControl' } }
    @{ Like = '^src/Neap\.Core/Updates/|^src/Neap\.Services/Update'; Needs = @{ Check = 1; Ui = 'banner' } }
    @{ Like = '^src/Neap\.Services/(Firmware|BootRomPort)'; Needs = @{ Check = 1; Ui = 'firmware' } }
    @{ Like = '^src/Neap\.(Core|Services|WinRt|Probe)/'; Needs = @{ Check = 1 } }

    # Screens: pictures always; the walk where the screen holds setting controls.
    @{ Like = '^src/Neap\.Desktop/Views/(Audio|Controls|Device)Page'; Needs = @{ Check = 1; Snap = 'page'; Walk = 'Settings' } }
    @{ Like = '^src/Neap\.Desktop/Controls/(ChoiceList|PresetChoice|PresetListItems|ProfileBar)\.'; Needs = @{ Check = 1; Ui = 'lists' } }
    @{ Like = '^src/Neap\.Desktop/Controls/(SettingRow|SettingChoice|SettingLink|SettingsCard|ChoiceList|ModeButtonChoice|SliderLine|LoudnessBoostRow|FormatRow|SystemVolumeRow|OptionNames)\.'; Needs = @{ Check = 1; Snap = 'audio', 'controls', 'device'; Walk = 'Settings' } }
    @{ Like = '^src/Neap\.Desktop/Controls/Parametric'; Needs = @{ Check = 1; Snap = 'equaliser'; Ui = 'parametric-screen' } }
    @{ Like = '^src/Neap\.Desktop/(Views/EqualiserPage|Controls/(Equaliser|Band|Curve|DecibelBox|FrequencyText|PresetChoice|PresetListItems|PresetNames|CompareHold|FilterImportDialog))'; Needs = @{ Check = 1; Snap = 'equaliser'; Walk = 'Equaliser' } }
    @{ Like = '^src/Neap\.Desktop/(Views/HomePage|Controls/(Quick|MicTile|SpatialTile|NoiseControlPanel|NoiseModes|BatteryMeter))'; Needs = @{ Check = 1; Snap = 'home'; Walk = 'Home' } }
    @{ Like = '^src/Neap\.Desktop/(Views/ProfilesPage|Controls/(Profile|ProfilesSettings))'; Needs = @{ Check = 1; Snap = 'profiles'; Ui = 'profiles' } }
    @{ Like = '^src/Neap\.Desktop/Views/SettingsPage'; Needs = @{ Check = 1; Snap = 'settings'; Ui = 'settings' } }
    @{ Like = '^src/Neap\.Desktop/(Views/FirmwarePage|Controls/Firmware)'; Needs = @{ Check = 1; Ui = 'firmware' } }
    @{ Like = '^src/Neap\.Desktop/(MiniPanelWindow|Controls/Mini)'; Needs = @{ Check = 1; Ui = 'mini' } }
    @{ Like = '^src/Neap\.Desktop/(MixOverlayWindow|Controls/(Mix|GameChatDial|LevelBars))'; Needs = @{ Check = 1; Ui = 'overlay'; Walk = 'ChatWheel' } }
    @{ Like = '^src/Neap\.Desktop/Controls/(Headset(StatusStrip|Title|Notice|Section)|RoutingNotice|MicMeter|SignalMeter|TransmittersPanel|ToneStrip|Tones|Takeover|NameNote)'; Needs = @{ Check = 1; Snap = 'home', 'device'; Note = 'a shared panel: snap the other pages that show it too' } }
    @{ Like = '^src/Neap\.Desktop/(UpdateNotice|Controls/(Update|Notice))'; Needs = @{ Check = 1; Ui = 'banner' } }
    @{ Like = '^src/Neap\.Desktop/Strings/'; Needs = @{ Check = 1; Note = 'wording changed: snap the page that shows it, and search tools\ for the old sentence' } }
    # What every screen shares: startup, the window, navigation, themes.
    @{ Like = '^src/Neap\.Desktop/(App\.|MainWindow|Program\.cs|Themes/|Controls/(NavItem|PageView|Neap\.|NeapControls))'; Needs = @{ Check = 1; Ui = 'all', 'access' } }
    @{ Like = '^src/Neap\.Desktop/(Assets/|Controls/(Space|Keys|OnScreen|NeapDialog|RecordingDialogs|DialAutomationPeer|EntranceTransition)|Localization/|Pretend|Motion|OneCopy|Window|Tray|HighContrast|DesktopPlatform|FluentKeys|Views/NotYet)'; Needs = @{ Check = 1; Note = 'a Desktop file with no page of its own: snap the page that shows it, and add a rule here' } }
    @{ Like = '^(Directory\.Build\.props|Neap\.sln|.*\.csproj)$'; Needs = @{ Check = 1 } }

    # The checks themselves: run the one changed.
    @{ Like = '^tools/ui/(Ui|run)\.ps1$'; Needs = @{ Ui = 'all' } }
    @{ Like = '^tools/ui/(snap|looks|prepare-headset-check|headset-sequence|hardware)\.ps1$'; Needs = @{ Note = 'a tool or a check for the real headset: run it only as its own skill says' } }
    @{ Like = '^tools/ui/(?<check>[a-z-]+)\.ps1$'; Needs = @{ Ui = '<check>' } }
    @{ Like = '^tools/pretend\.ps1$'; Needs = @{ Note = 'the walk changed: run the sections you changed with -Only' } }
    @{ Like = '^(tools/|\.claude/|\.github/|\.git|TestResults/)'; Needs = @{} }
)

$check = @(); $snaps = [ordered]@{}; $walk = [ordered]@{}; $ui = [ordered]@{}; $notes = [ordered]@{}; $unmapped = @()
foreach ($file in $files) {
    $rule = $rules | Where-Object { $file -match $_.Like } | Select-Object -First 1
    if (-not $rule) { $unmapped += $file; continue }
    $needs = $rule.Needs
    if ($needs.Check) { $check += $file }
    foreach ($page in @($needs.Snap)) {
        if (-not $page) { continue }
        if ($page -eq 'page') { $page = ($file -replace '^.*/Views/(\w+)Page.*$', '$1').ToLowerInvariant() }
        $snaps[$page] = @($snaps[$page]) + $file
    }
    foreach ($section in @($needs.Walk)) { if ($section) { $walk[$section] = @($walk[$section]) + $file } }
    foreach ($name in @($needs.Ui)) {
        if (-not $name) { continue }
        if ($name -eq '<check>') {
            # A check that was deleted has nothing to run.
            if (-not (Test-Path (Join-Path $root $file))) { continue }
            $null = $file -match $rule.Like; $name = $Matches['check']
        }
        $ui[$name] = @($ui[$name]) + $file
    }
    if ($needs.Note) { $notes[$needs.Note] = $true }
}

# The whole of something stands in for its parts.
if ($walk.Contains('Whole')) { $whole = $walk['Whole']; $walk.Clear(); $walk['Whole'] = $whole }
if ($ui.Contains('all')) { foreach ($name in @($ui.Keys | Where-Object { $_ -notin 'all', 'access' })) { $ui.Remove($name) } }

function Say([string]$command, $because) {
    Write-Host $command
    Write-Host "    for $((@($because | Where-Object { $_ } | Sort-Object -Unique)) -join ', ')" -ForegroundColor DarkGray
}
$run = 'powershell -ExecutionPolicy Bypass -File'
if ($check) { Say "$run tools\check.ps1" $check }
foreach ($page in $snaps.Keys) { Say "$run tools\ui\snap.ps1 -Page $page" $snaps[$page] }
foreach ($name in $ui.Keys) { Say "$run tools\pretend-quiet.ps1 -Run 'tools\ui\run.ps1 $name -Inside'" $ui[$name] }
if ($walk.Count) {
    $only = if ($walk.Contains('Whole')) { '' } else { " -Only $($walk.Keys -join ',')" }
    Say "$run tools\pretend-quiet.ps1 -Run 'tools\pretend.ps1 -Unattended$only'" @($walk.Values | ForEach-Object { $_ })
}
foreach ($note in $notes.Keys) { Write-Host "note  $note" }
foreach ($file in $unmapped) { Write-Host "note  no rule for $file; choose its checks by hand and add a rule here" }
if (-not ($check -or $snaps.Count -or $ui.Count -or $walk.Count)) { Write-Host 'no checks needed beyond reading it over' }
