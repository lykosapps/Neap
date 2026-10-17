---
name: close-only-your-own-copies
description: "Several sessions run Neap on this machine at once; stop or drive only copies under your own build folder, and wait for another session's pretend run rather than closing it"
metadata:
  node_type: memory
  type: feedback
  originSessionId: e26f4011-5a6c-4060-8022-ef8007c4154d
  modified: 2026-09-24T03:45:11.571Z
---

Stop, relaunch or drive only Neap copies whose path is under your own build folder (a copy you published yourself; the owner's daily copy is E:\Dev\neap\build, see [[neap-folders]]). Never stop every Neap.exe or every --pretend process. Only one pretend run can hold the Neap.Pretend pipe, so wait until no --pretend copy is open before starting one.

**Why:** on 2026-09-24 a clean-up that stopped every Neap.exe closed the owner's running Option B copy and another session's pretend run.

**How to apply:** filter by ExecutablePath before Stop-Process, and poll for other pretend runs rather than killing them. See [[the-machine-is-in-use]].
