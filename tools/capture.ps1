<#
.SYNOPSIS
Records the headset's USB traffic while you operate Swarm II, for the probe's
decode command.

.DESCRIPTION
Runs USBPcapCMD against one USB device for a fixed time and writes a .pcap.
Start Swarm II and close Stealth Pro II Control first: only one program can
read the headset's replies at a time. Then operate the control you want to
identify while the capture runs, and decode the result:

    StealthPro.Probe decode <file.pcap>

Needs USBPcap (https://desowin.org/usbpcap/), installed and followed by a
reboot, and an elevated PowerShell. To find the interface and device address,
run USBPcapCMD.exe on its own: it lists each USBPcap interface and the
devices attached to it. Pick the Turtle Beach composite device.

.PARAMETER Interface
The USBPcap interface number, as in \\.\USBPcap<n>.

.PARAMETER Device
The device's address on that interface.

.PARAMETER Seconds
How long to capture.

.PARAMETER Name
The capture's file name, without extension.

.PARAMETER Folder
Where to write it. Keep the path short: USBPcap fails silently past the
Windows path limit.

.EXAMPLE
.\capture.ps1 -Interface 2 -Device 1 -Seconds 60 -Name anc-toggle
#>
#Requires -RunAsAdministrator
param(
    [Parameter(Mandatory)][int]$Interface,
    [int]$Device = 1,
    [int]$Seconds = 120,
    [string]$Name = 'capture',
    [string]$Folder = (Join-Path $env:TEMP 'tbcap')
)

$ErrorActionPreference = 'Stop'

$usbpcap = Join-Path $env:ProgramFiles 'USBPcap\USBPcapCMD.exe'
if (-not (Test-Path $usbpcap)) { throw "USBPcap is not installed: $usbpcap not found" }

New-Item -ItemType Directory -Force -Path $Folder | Out-Null
$out = Join-Path $Folder "$Name.pcap"
Remove-Item $out -ErrorAction SilentlyContinue

$arguments = "-d \\.\USBPcap$Interface -o `"$out`" --devices $Device --inject-descriptors -b 67108864"
Write-Host "Capturing for $Seconds s: USBPcapCMD $arguments"

$process = Start-Process -FilePath $usbpcap -ArgumentList $arguments -PassThru -WindowStyle Hidden
Start-Sleep -Seconds $Seconds
if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
Start-Sleep -Seconds 2

if (-not (Test-Path $out) -or (Get-Item $out).Length -eq 0) {
    throw "Nothing was captured. Check the interface and device address, and that the path is short."
}
Write-Host "Wrote $out ($((Get-Item $out).Length) bytes)"
