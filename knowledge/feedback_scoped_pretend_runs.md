---
name: feedback-scoped-pretend-runs
description: "When driving Neap's pretend mode for screenshots, write a small targeted script for the screens in question rather than extending and rerunning the full tools/pretend.ps1 walk"
metadata:
  node_type: memory
  type: feedback
  originSessionId: cfc7bd46-df47-49fc-88b4-4526f8cd1618
  modified: 2026-09-27T12:09:26.140Z
---

Don't bolt new screenshot needs onto Neap's full `tools/pretend.ps1` walk and rerun the whole thing to iterate. Write a small, separate, targeted pretend-mode script that only launches the app, drives the specific screens/states needed, and screenshots those — nothing else.

**Why:** during a UI review of the Profiles feature, the full walk (every page, every setting, chat wheel, noise control, presets, battery, power cycle) was rerun three times just to fix the timing on one screenshot. Each run takes over a minute and repeatedly brings the pretend window to the front, hijacking the owner's screen. The owner called this out directly: it burns time/credits and interrupts them for far longer than the actual task needs. The pretend-mode skill itself says "keep to one walk per piece of work" — the exhaustive walk is a regression check meant to run once per feature, not an iteration tool for getting one screenshot right.

**How to apply:** when a task only needs to see a handful of screens or states (a design review, checking one control's rendering, verifying one flow), write a minimal standalone script scoped to just that — launch, drive the specific interaction, screenshot, exit. Reserve a run of the full `tools/pretend.ps1` walk for actual regression verification of a finished change, run once, not for iterative screenshot-fixing. This generalizes beyond Neap: for any project with a similar full-app pretend/E2E walk, don't reuse it as a screenshot-iteration tool — build a narrow script for the narrow need.


---

**NOTE (2026-10-17, memory migration):** merged with `efficient-verification.md` and `project_speed_up_agent_checks.md` into Tenon's 'Cheapest check first' verifying standard.
