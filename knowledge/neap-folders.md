---
name: neap-folders
description: where Neap lives on the owner's machine since 25 Sep 2026 — E:\Dev\neap\neap (project), \build (daily copy, start with Windows), \archive (backups, USB captures)
metadata:
  type: project
---

Since 2026-09-25 everything Neap is in `E:\Dev\neap`:

- `neap`: the git repository (was `E:\Dev\stealth-pro-ii-control`, then `E:\Dev\neap\code`; projects are `Neap.App`, `Neap.Core`, `Neap.Probe` under `src`, `Neap.Core.Tests` under `tests`, `Neap.sln` at the root).
- `build`: The owner's daily copy, `Neap.exe`, which start with Windows points at. Since 25 Sep, publish every committed change there without asking (he said "always update the daily copy"), stopping only that copy first and relaunching through Win32_Process.Create ([[close-only-your-own-copies]]).
- `archive`: `branches-2026-09-24.bundle` (every old branch), `old-history-2026-09-23.bundle` (the pre-publishing history, never to be pushed), the old code-review prompt, and `usb-captures` (840 MB of Swarm II captures, kept because Swarm II is uninstalled and they can't easily be remade).

All old worktrees and branches were removed on 2026-09-25; only `main` remains.

Done 2026-09-25: folder renamed from `code` to `neap` so the Code menu shows "neap"; the old `stealth-pro-ii-control` copy's contents went to the Recycle Bin. The empty folder left behind was removed once the old sessions were archived.

**Why:** the owner asked for one Neap folder with names to convention and nothing unused.

**How to apply:** use these paths; do not recreate `neap-builds` or `neap-archive`. Anything older in memory that names the old paths is out of date.


---

**SUPERSEDED (2026-10-17, memory migration):** the repository is `E:\Dev\neap\Neap-Repo` (GitHub `lykosapps/Neap`, public); `E:\Dev\neap\neap` no longer exists. The projects are now `Neap.Core`, `Neap.Desktop`, `Neap.Probe`, `Neap.Services` and `Neap.WinRt` under `src`, with `Neap.Core.Tests` under `tests`; `STRUCTURE.md` is the current map. Beside the repository, `build`, `build-previous`, `archive`, `linux-test`, `swarm-firmware` and `scratch` exist. 'Only `main` remains' is out of date: sessions keep worktree branches under `.claude/worktrees`.
