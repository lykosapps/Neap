<#
.SYNOPSIS
Says whether main is ready to tag as a release, and what is left if not.

.DESCRIPTION
Read-only: it fetches from GitHub and reads, and changes nothing. It checks
that main is checked out with nothing uncommitted; what is unpushed, and
anything on GitHub that main has not taken in; the unpushed commits for
anything that could identify the owner (author, times in UTC, no trailers,
names, user folders, device addresses); that the version in
Directory.Build.props is the changelog's newest, dated today in UTC, with
nothing left under Unreleased and no tag for it yet; and the backlog's items
for before that version is tagged.

Each line is "ok", "todo" (to do before tagging) or "note". It exits 1 when
anything is to do.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools\release-check.ps1
#>

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$todo = 0
function Ok([string]$what) { Write-Host "  ok    $what" }
function Todo([string]$what) { $script:todo++; Write-Host "  todo  $what" -ForegroundColor Yellow }
function Note([string]$what) { Write-Host "  note  $what" }
function Read-Git { git.exe -C $root @args 2>$null }

Write-Host '== the working copy'
$branch = Read-Git branch --show-current
if ($branch -eq 'main') { Ok 'main is checked out' } else { Todo "main is not checked out (this is $branch)" }
$dirty = @(Read-Git status --porcelain)
if ($dirty.Count -eq 0) { Ok 'nothing uncommitted' } else { Todo "$($dirty.Count) files uncommitted: $(($dirty | ForEach-Object { $_.Substring(3) }) -join ', ')" }

Write-Host '== GitHub'
git -C $root fetch -q origin 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Todo 'could not fetch from GitHub; check which GitHub account is signed in' }
$ahead = [int](Read-Git rev-list --count origin/main..main)
$behind = @(Read-Git log --format='%h %s' main..origin/main)
if ($behind.Count -eq 0) { Ok 'main has everything on GitHub' } else { Todo "GitHub has $($behind.Count) commits main has not taken in: $($behind -join '; ')" }
if ($ahead -eq 0) { Ok 'nothing unpushed' } else { Note "commits to push: $ahead" }

Write-Host '== privacy, in the unpushed commits'
if ($ahead -gt 0) {
    $range = 'origin/main..main'
    $who = @(Read-Git log $range --format='%an <%ae>|%cn <%ce>' | Sort-Object -Unique)
    $expected = 'lykosapps <224482331+lykosapps@users.noreply.github.com>'
    if ($who.Count -eq 1 -and $who[0] -eq "$expected|$expected") { Ok 'every author and committer is the project''s' } else { Todo "authors other than the project's: $($who -join '; ')" }
    $local = @(Read-Git log $range --format='%h %ad|%cd' --date=iso-strict | Where-Object { $_ -notmatch ' [^|]*(Z|\+00:00)\|[^|]*(Z|\+00:00)$' })
    if ($local.Count -eq 0) { Ok 'every time is in UTC' } else { Todo "times not in UTC: $(($local | ForEach-Object { $_.Split(' ')[0] }) -join ', ')" }
    $trailers = @(Read-Git log $range --format='%h %B' | Select-String 'Co-Authored-By|Signed-off-by')
    if ($trailers.Count -eq 0) { Ok 'no trailers' } else { Todo "trailers in $($trailers.Count) commits" }
    # A user's folders and any device's hardware address, and the words that
    # would name the owner. Those are kept in this copy's .git\info, which git
    # never commits or pushes, one pattern a line: a list of them here would
    # publish what it looks for.
    $generic = @('C:\\Users\\', '[A-Z]:\\Dev\\', '[A-Z]:/Dev/', '/home/\w+', '\b(?:[0-9A-F]{2}:){5}[0-9A-F]{2}\b')
    $private = Join-Path (Read-Git rev-parse --git-common-dir) 'info\private-terms'
    $personal = @(if (Test-Path $private) { Get-Content $private | Where-Object { $_.Trim() -and -not $_.StartsWith('#') } })
    if ($personal.Count -eq 0) { Todo "no words to look for that would name the owner: put them in $private, one pattern a line" }
    $identifying = ($generic + $personal) -join '|'
    $inChanges = @(Read-Git diff $range -U0 | Where-Object { $_ -match '^\+' -and $_ -notmatch '^\+\+\+' } | Select-String -Pattern $identifying)
    $inMessages = @(Read-Git log $range --format='%B' | Select-String -Pattern $identifying)
    if ($inChanges.Count + $inMessages.Count -eq 0) { Ok 'nothing identifying in the changes or messages' }
    else { foreach ($hit in @($inChanges) + @($inMessages)) { Todo "identifying: $($hit.Line.Trim().Substring(0, [Math]::Min(120, $hit.Line.Trim().Length)))" } }
    $pictures = @(Read-Git diff --name-only --diff-filter=AM $range | Where-Object { $_ -match '\.(png|jpe?g|gif|webp)$' })
    if ($pictures.Count) { Note "pictures to look at by eye before pushing: $($pictures -join ', ')" }
}
else { Ok 'nothing to scan' }

Write-Host '== the version'
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$changelog = Get-Content (Join-Path $root 'CHANGELOG.md') -Encoding utf8
$headings = @($changelog | Select-String '^## (.+)$' | ForEach-Object { $_.Matches[0].Groups[1].Value })
$newest = $headings | Where-Object { $_ -ne 'Unreleased' } | Select-Object -First 1
$today = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
# The heading reads "0.3.0", a dash, then the date; the dash is matched as any mark, so this file stays plain ASCII.
if ($newest -match "^$([regex]::Escape($version))\s+\S+\s+(\d{4}-\d{2}-\d{2})") {
    Ok "the changelog's newest version is $version, as the app's is"
    if ($Matches[1] -eq $today) { Ok "it is dated today, $today" } else { Todo "it is dated $($Matches[1]); if it is tagged today, make that $today" }
}
else { Todo "the app's version is $version but the changelog's newest is '$newest'" }
$start = [Array]::IndexOf($changelog, '## Unreleased')
if ($start -ge 0) {
    $end = ($changelog | Select-Object -Skip ($start + 1) | Select-String '^## ' | Select-Object -First 1).LineNumber
    $under = @($changelog[($start + 1)..($start + $end - 1)] | Where-Object { $_.Trim() })
    if ($under.Count -eq 0) { Ok 'nothing left under Unreleased' } else { Todo "$($under.Count) lines under Unreleased that this version would leave out" }
}
if (Read-Git tag -l "v$version") { Todo "the tag v$version already exists" } else { Ok "no tag v$version yet" }

Write-Host "== the backlog, before $version is tagged"
# An item's bold title can run onto the next line, so only its first line is matched.
$waiting = @(Get-Content (Join-Path $root 'BACKLOG.md') -Encoding utf8 | Select-String "^- \*\*Before $([regex]::Escape($version)) is tagged: (.+)$")
if ($waiting.Count -eq 0) { Ok 'nothing waiting' } else { foreach ($item in $waiting) { Todo ($item.Matches[0].Groups[1].Value -replace '\*\*.*$', '') } }

Write-Host ''
if ($todo -eq 0) { Write-Host "Ready to tag v$version." } else { Write-Host "$todo to do before tagging v$version." }
exit ([int]($todo -gt 0))
