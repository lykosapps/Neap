---
name: no-foreground-windows-while-owner-works
description: "Don't run checks that open or raise app windows while the owner is at the PC; ask first or wait until they step away"
metadata:
  node_type: memory
  type: feedback
  originSessionId: fbc97cb2-a5bc-4426-a3dd-08a7a872d8fa
  modified: 2026-10-07T10:50:27.452Z
---

The owner told me "stop bringing to foreground, I'm using the computer" while the tools/ui screen checks and desktop.ps1 audit were running.

**Why:** the UIA checks launch the app, resize it and focus controls, which steals focus mid-work.

**How to apply:** before running `tools/ui/run.ps1` or `desktop.ps1`, ask or wait for the owner to say they're away; do file-only work (edits, build, tests, commits) otherwise. Related: [[feedback_long_runs_background]].
