---
name: neap-0-2-0-released
description: "State after 8 Oct 2026: Neap 0.2.0 released from main, one Avalonia app for Windows and Linux; where the handoff notes and checks live"
metadata:
  node_type: memory
  type: project
  originSessionId: fbc97cb2-a5bc-4426-a3dd-08a7a872d8fa
  modified: 2026-10-08T00:37:19.707Z
---

**Atlas Air test build 0.2.1.1 published 2026-10-08** as a pre-release (tags with a fourth number are test builds); checklist posted on GitHub issue 1, waiting on the tester's results and Swarm II screenshots. Before 0.2.2: Atlas Air must carry only confirmed functions (BACKLOG). **Neap 0.2.1 released 2026-10-08** (tag v0.2.1 at 6d29a08, after moving it off a cancelled run): the centre-beep fixes, issue #2 closed. A 0.2.0 copy built from its tag was seen to find 0.2.1. Note the 0.2.0 exe is Neap.Desktop.exe.

Neap 0.2.0 was tagged and released on 2026-10-08 from `main` (the `linux` branch was merged and is gone or going). The old WinUI app is retired. The Windows try-it copy at `E:\Dev\neap\build` now runs the new app named `Neap.exe` (old copy kept in `E:\Dev\neap\build-previous`).

**Where to look first:** `.claude/handoff.md` in the main checkout (local only), then BACKLOG.md "Next, after 0.2.0" and the top of DECISIONS.md. Hardware checks: `tools/ui/run.ps1 hardware -Parts BFKLT` (owner away; it stops and restarts his copy and restores the shortcut). Battery readings are unreliable by hardware: see FINDINGS.md and [[real-launch-allowed-with-safeguards]].

**Release steps used:** from main's folder run `git merge --ff-only linux; if ($?) { git push origin main }` style commands in PowerShell (no `&&`); editing a published release needs `gh auth switch --user lykosapps` and back to `<work GitHub account>`. Agents cannot run git in main's folder from a worktree session, so the owner runs merges and pushes.

Related: [[label-files-repo-or-local]], [[no-foreground-windows-while-owner-works]].


---

**SUPERSEDED (2026-10-17, memory migration):** later releases and test builds are in `CHANGELOG.md`; the newest hand-over is the local `.claude/handoff-2026-10-15.md`, which is newer than `.claude/handoff.md`. The `linux` branch is merged.
