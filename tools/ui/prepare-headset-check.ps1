# Prepares a firmware headset check (docs/design/HEADSET-CHECKS.md, checks 37-44) and proves the build before anyone
# touches the headset. Run it before every such check; the check starts only if this ends with 0 failures.
#   1. Publishes the app with the Firmware page on (FirmwareEdit.Released and the page switch), then puts the source back.
#      Skipped when the source has not changed since the last such publish (a hash of src/ is kept beside the build).
#   2. Opens that build for real and checks the Firmware page is there and its edits are listed (not with -PublishOnly,
#      for a script that opens the build itself, as headset-sequence.ps1 does).
#   3. Says how the headset is connected now, from the app log.
param([switch]$PublishOnly)
. "$PSScriptRoot\Ui.ps1"
$root = (Resolve-Path "$PSScriptRoot\..\..").Path
$edit = Join-Path $root 'src\Neap.Core\Firmware\FirmwareEdit.cs'
$window = Join-Path $root 'src\Neap.Desktop\MainWindow.axaml.cs'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$stamp = Join-Path (Split-Path $script:Exe) 'test-build.stamp'
# The stamp is the source's hash and the published Neap.Core.dll's hash: run.ps1 publishes the same folder for the pretend
# checks, without the Firmware page, and a stamp on the source alone missed that (4 November 2026).
$core = Join-Path (Split-Path $script:Exe) 'Neap.Core.dll'
function Get-BuildHash { if (Test-Path $core) { (Get-FileHash $core -Algorithm SHA256).Hash } else { '' } }

function Get-SourceHash {
    $files = Get-ChildItem (Join-Path $root 'src') -Recurse -File -Include *.cs, *.axaml, *.resw, *.csproj, *.props |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Sort-Object FullName
    $sha = [System.Security.Cryptography.SHA256]::Create()
    foreach ($file in $files) {
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        [void]$sha.TransformBlock($bytes, 0, $bytes.Length, $null, 0)
    }
    [void]$sha.TransformFinalBlock([byte[]]@(), 0, 0)
    [BitConverter]::ToString($sha.Hash).Replace('-', '')
}

Section 'prepare: publish the test build with the Firmware page on'
$hash = Get-SourceHash
if ((Test-Path $script:Exe) -and (Test-Path $stamp) -and (Get-Content $stamp -Raw).Trim() -eq "$hash $(Get-BuildHash)") {
    Note 'the source and the test build are as they were at the last publish; not publishing again'
}
else {    $sources = @{ $edit = [IO.File]::ReadAllText($edit); $window = [IO.File]::ReadAllText($window) }
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
    if ($script:Fails -eq 0) { Set-Content $stamp "$hash $(Get-BuildHash)" }
}
if ($PublishOnly) { Write-Host "$script:Fails failures"; exit $script:Fails }
Section 'prepare: the test build shows the Firmware page'
Protect-RealRun {
    $lines = @(Get-RealLog).Count
    Start-NeapReal -Page firmware
    [void](Wait-Until { Get-RealLog $lines | Where-Object { $_ -match 'firmware edits:' } } 15)
    $log = @(Get-RealLog $lines)
    Check (-not ($log -match 'no page with the tag firmware')) 'the Firmware page is in the build'
    $listed = Wait-Until { Find-Named 'No blinking when disconnected' } 15
    Check $listed "the edits are listed (if not, switch the headset on once so Neap sees it)"
    $log | Where-Object { $_ -match 'headset: (Quiet|Connected|its firmware)|firmware edits: read' } | ForEach-Object { Note "log: $_" }
}
Write-Host "$script:Fails failures"
exit $script:Fails
