---
name: parallel-pretend-runs
description: Another Claude session runs pretend/UI checks on the same PC at the same time; how to keep Linux-branch runs from colliding with them
metadata:
  node_type: memory
  type: project
  originSessionId: fbc97cb2-a5bc-4426-a3dd-08a7a872d8fa
  modified: 2026-10-06T08:01:01.645Z
---

Another session (on main) runs the WinUI pretend app and `tools/ui/run.ps1` screen checks on this PC while the linux branch works. Both share the pretend data folder (`%LOCALAPPDATA%\Neap\Pretend`) and the pretend control pipe, so a run can read the other's settings or log, or find the pipe busy.

**Why:** Seen 2026-10-06 while aligning the linux branch with main: profile screenshots showed another run's profile, and the log was deleted mid-run.

**How to apply:** Run the Avalonia app's pretend runs with `NEAP_PRETEND_FOLDER=PretendLinux` (own folder and log; "pipe busy" in that log is harmless) and `--wait 20` for pages that read profiles. Before running `tools/ui/run.ps1`, wait until no process with `pretend` in its command line is running. Never kill the other session's processes.


---

**SUPERSEDED (2026-10-17, memory migration):** written for the `linux` branch, which is merged; there is one Avalonia app now. What still holds: one pretend run at a time, wait for another session's run, never stop its processes (`close-only-your-own-copies.md`).
