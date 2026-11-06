<#
.SYNOPSIS
Builds, tests, checks formatting and publishes, and ends with one line
saying whether the change is ready to look at.

.DESCRIPTION
The code check for every change, in about forty seconds. It stops at the
first step that fails and shows that step's output; a step that passes
shows only its time. Formatting is checked for whitespace here, since the
build already holds every analyzer rule as an error; the full format check
runs before a push.

.PARAMETER NoPublish
Skip the publish, for a change that no screen check will run against.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\check.ps1
#>
param([switch]$NoPublish)
$root = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $root 'Neap.sln'

$steps = [ordered]@{
    build   = { dotnet build $solution -c Release --nologo -v:q }
    tests   = { dotnet test --project (Join-Path $root 'tests\Neap.Core.Tests') -c Release --no-build }
    format  = { dotnet format whitespace $solution --verify-no-changes }
    publish = { dotnet publish (Join-Path $root 'src\Neap.Desktop') -c Release -r win-x64 -f net10.0-windows10.0.19041.0 --self-contained --nologo -v:q }
}
if ($NoPublish) { $steps.Remove('publish') }

$total = [Diagnostics.Stopwatch]::StartNew()
foreach ($name in $steps.Keys) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $output = & $steps[$name] 2>&1
    if ($LASTEXITCODE -ne 0) {
        $output | ForEach-Object { Write-Host $_ }
        Write-Host ("FAIL  {0} failed after {1:0}s" -f $name, $clock.Elapsed.TotalSeconds)
        exit 1
    }
    Write-Host ("ok    {0} {1:0}s" -f $name, $clock.Elapsed.TotalSeconds)
}
Write-Host ("ok    ready in {0:0}s" -f $total.Elapsed.TotalSeconds)
