---
name: project-speed-up-agent-checks
description: "Neap's screen-check loop: what was slow, what was built (tools/ui, three checks in about a minute), what is open"
metadata:
  node_type: memory
  type: project
  originSessionId: 11ae904b-61a5-4365-8048-f96c391c9f84
  modified: 2026-10-05T12:07:15.499Z
---

The owner asked (2026-10-05) to optimise the build-fix-verify loop after a background agent took about 25 minutes on small update-bar fixes.

**Built and committed (873b333, c31761f):** `tools/ui/run.ps1 banner|settings|profiles|all` with shared `tools/ui/Ui.ps1`. It publishes only if a source file is newer than the build, runs the check from outside the Claude package, launches the app once per theme and resizes in place, polls instead of sleeping, retakes black frames, refuses a second practice run, and stops with a message if the screen is dark. Banner check alone: 33 s and 106 checks, against 141 s and 87 for the agent's script (23 launches). All three: about 57 s and 208 checks. Documented in CLAUDE.md under Verifying.

**Correction from another session (commit 293b07f):** the helper's "screen is off" test sampled the desktop, and a black wallpaper with a hidden taskbar looks the same as a screen that is off, so it gave false alarms; checks now judge the app's own window. That weakens the 5-minute-timeout theory below.

**Still unproven (do not state as fact):** why pictures come out black. The display turns itself off after 5 idle minutes on AC power (timeout 300 s), and runs after long idle gaps went black, which fits; but some black pictures appeared when the owner was probably at the PC, and pictures taken from inside the tools' package were black while a process started with Win32_Process.Create saw a lit screen. A decisive test would log lit state against idle time for 6 minutes with no input. The helper's wake step (keep-display-on flag plus a zero-distance mouse event) is not proven to hold the display on.

**Other facts:** only one `--pretend` run can exist at once (a collision logs "started again while running"). A stale compiler server left from a stricter-rules build made a clean build report 10 errors; `dotnet build-server shutdown` cleared it. Builds take about 15 s, tests 20 s, publish 40 to 60 s, the full pretend walk (tools/pretend.ps1) about 3 minutes and brings windows forward; keep it for releases or wide changes.

**Working practice:** use the Edit tool, not Python heredocs, for file edits (backslash escapes get mangled and cost reruns); for UI tweaks this size do the work inline rather than briefing an agent, and when an agent is used give it the ready `run.ps1` command. Related: [[feedback-scoped-pretend-runs]], [[feedback-long-runs-background]].


---

**NOTE (2026-10-17, memory migration):** 'use the Edit tool, not Python heredocs' and `never-round-trip-source-through-powershell.md` ('edit with Python or the Edit tool') agree on the main point: prefer the Edit tool.
