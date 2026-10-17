RETIRED 2026-10-17: exact duplicate from an older memory folder; see ../always-update-daily-copy.md

---
name: always-update-daily-copy
description: "After every Neap change is built, tested and committed, publish it to the owner's daily copy (E:\\Dev\\neap\\build) without asking"
metadata:
  node_type: memory
  type: feedback
  originSessionId: a78e1739-b7af-4f35-931a-abbfc8f4aa7d
  modified: 2026-09-25T04:17:54.177Z
---

Once a Neap change builds, passes its tests and is committed, put it in the daily copy straight away: publish Release from a clean `E:\Dev\neap\neap`, stop only `E:\Dev\neap\build\Neap.exe`, copy the publish output over, relaunch through `Win32_Process.Create` (so it runs outside Claude's package and uses the real app folder), then confirm it reads "Headset connected".

**Why:** on 2026-09-25 the owner said "always update the daily copy" after approving it eight times in a row; he tests every change by ear on the real headset.

**How to apply:** don't ask "shall I put it in your daily copy?"; do it and say it's there, with what to try. Still say plainly what wasn't checked. Supersedes "publish there only when he agrees" in the neap-folders note.
