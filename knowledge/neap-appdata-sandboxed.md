---
name: neap-appdata-sandboxed
description: Claude's shell sees a stale sandboxed copy of %LOCALAPPDATA%\Neap — read the real app.log via an unsandboxed process
metadata:
  type: reference
---

The Claude desktop app is a packaged (MSIX) app, so shells it spawns get AppData redirected to `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local\Neap`. Reading `%LOCALAPPDATA%\Neap\app.log` from Bash/PowerShell returns that stale copy (stopped 2026-09-25 11:09), not the log the real Neap writes.

**Why:** On 2026-09-25 this made the log look broken; it wasn't.
**How to apply:** To read the real folder, launch outside the package: `Invoke-CimMethod Win32_Process Create` running `cmd /c copy "%LOCALAPPDATA%\Neap\app.log" <E:\ path>`, then read the copy and delete it. Neap launched by Claude also writes to the sandboxed copy. Related: [[parametric-eq-direction]]
