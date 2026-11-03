# Prepares a firmware headset check (docs/design/HEADSET-CHECKS.md, checks 37-44) and proves the build before anyone
# touches the headset. Run it before every such check; the check starts only if this ends with 0 failures.
#   1. Publishes the app with the Firmware page on (FirmwareEdit.Released and the page switch), then puts the source back.
#   2. Opens that build for real and checks the Firmware page is there and its edits are listed.
#   3. Says how the headset is connected now, from the app log.
. "$PSScriptRoot\Ui.ps1"
$root = (Resolve-Path "$PSScriptRoot\..\..").Path
$edit = Join-Path $root 'src\Neap.Core\Firmware\FirmwareEdit.cs'
$window = Join-Path $root 'src\Neap.Desktop\MainWindow.axaml.cs'
$utf8 = New-Object System.Text.UTF8Encoding($false)

Section 'prepare: publish the test build with the Firmware page on'
$sources = @{ $edit = [IO.File]::ReadAllText($edit); $window = [IO.File]::ReadAllText($window) }
try {
    $on = $sources[$edit].Replace('public const bool Released = false;', 'public const bool Released = true;')
    $page = $sources[$window].Replace('bool wanted = (Core.Firmware.FirmwareEdit.Released && AppSettings.Current.ShowFirmwareEdits)', 'bool wanted = Core.Firmware.FirmwareEdit.Released')
    Check ($on -ne $sources[$edit]) 'the Released switch was found'
    Check ($page -ne $sources[$window]) 'the page switch was found'
    [IO.File]::WriteAllText($edit, $on, $utf8)
    [IO.File]::WriteAllText($window, $page, $utf8)
    dotnet publish (Join-Path $root 'src\Neap.Desktop') -c Release -r win-x64 -f net10.0-windows10.0.19041.0 --self-contained --nologo -v:q
    Check ($LASTEXITCODE -eq 0) 'the build published'
}
finally {
    foreach ($file in $sources.Keys) { [IO.File]::WriteAllText($file, $sources[$file], $utf8) }
}
Check (@($sources.Keys | Where-Object { [IO.File]::ReadAllText($_) -ne $sources[$_] }).Count -eq 0) 'the source is back as it was'

Section 'prepare: the test build shows the Firmware page'
Protect-RealRun {
    $lines = @(Get-RealLog).Count
    Start-NeapReal -Page firmware
    [void](Wait-Until { Get-RealLog $lines | Where-Object { $_ -match 'firmware edits:' } } 15)
    $log = @(Get-RealLog $lines)
    Check (-not ($log -match 'no page with the tag firmware')) 'the Firmware page is in the build'
    $listed = Wait-Until { Find-Named 'Light off when disconnected' } 15
    Check $listed "the edits are listed (if not, switch the headset on once so Neap sees it)"
    $log | Where-Object { $_ -match 'headset: (Quiet|Connected|its firmware)|firmware edits: read' } | ForEach-Object { Note "log: $_" }
}
Write-Host "$script:Fails failures"
exit $script:Fails
