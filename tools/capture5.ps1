param([int]$Seconds = 160, [string]$Tag = 'toggles')

$dir  = (Join-Path $env:TEMP 'tbcap')
$cmd  = 'C:\Program Files\USBPcap\USBPcapCMD.exe'
$log  = Join-Path $dir "$Tag.log"
$done = Join-Path $dir "$Tag.done"
$out  = Join-Path $dir "$Tag.pcap"

Remove-Item $log,$done,$out -ErrorAction SilentlyContinue
function W($m) { Add-Content -Path $log -Value $m -Encoding utf8 }

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName               = $cmd
$psi.Arguments              = "-d \\.\USBPcap2 -o `"$out`" --devices 1 --inject-descriptors -b 67108864"
$psi.UseShellExecute        = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError  = $true
$psi.CreateNoWindow         = $true
W "ARGS: $($psi.Arguments)"

$p = [System.Diagnostics.Process]::Start($psi)
W "started pid=$($p.Id); capturing ${Seconds}s"
Start-Sleep -Seconds $Seconds
try { if (-not $p.HasExited) { $p.Kill() } } catch { W "kill failed: $_" }
Start-Sleep -Seconds 2
try { W ("STDERR: " + $p.StandardError.ReadToEnd().Trim()) } catch {}
$sz = 0; if (Test-Path $out) { $sz = (Get-Item $out).Length }
W "done bytes=$sz"
Set-Content -Path $done -Value "ok $sz" -Encoding utf8
