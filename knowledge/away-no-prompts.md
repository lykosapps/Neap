---
name: away-no-prompts
description: "When the owner steps away, avoid commands that raise approval prompts; update the daily copy once at the end"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 434e2304-9c16-47c0-a535-5899f817473a
  modified: 2026-09-25T09:32:23.356Z
---

When the owner says he's away ("keep going", "do as much as you can without me"), don't run commands that need his approval mid-run. The ones that did on 25 Sep 2026: stopping and restarting his running Neap (PowerShell Stop-Process / Win32_Process Create), deleting files outside the project (e.g. the pretend folder in LOCALAPPDATA), and repeated pretend-run scripts.

**Why:** he got approval prompts on his phone while away and was annoyed: "Why do you keep asking me for approval I'm not supposed to be here".

**How to apply:** batch work so the daily-copy swap ([[always-update-daily-copy]]) happens once, at the very end; don't clean up pretend folders; keep pretend runs to as few as possible. Say in one line at the start that one prompt may come at the end.
