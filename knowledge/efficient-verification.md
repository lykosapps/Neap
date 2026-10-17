---
name: efficient-verification
description: Prefer fast/headless checks over repeated full UI-automation runs when verifying a Neap change
metadata:
  node_type: memory
  type: feedback
  originSessionId: 434e2304-9c16-47c0-a535-5899f817473a
  modified: 2026-09-26T02:30:18.199Z
---

When verifying a change to Neap, reach for `dotnet test` (headless, seconds) first, and a small targeted script for the one control being iterated on, rather than rerunning `tools/pretend.ps1`'s full every-page walk repeatedly. That script pulls the window to the front for every page it visits and takes real time; running it 3-4 times in a row to chase a flaky or unrelated failure is disruptive and wasteful. Run the full walk once, near the end, as a final pass — not as an iteration loop. If it turns up a failure unrelated to the change at hand, look at it once and say so rather than blind-retrying.

**Why:** the owner noticed the pattern directly (25-26 Sep 2026, while working on the spatial sound feature): "It feels like you get stuck in loops sometimes when you're running it 3 or 4 times one after another." He was actively using the machine at the time, which the script's own docs already warn makes its checks unreliable (it steals foreground focus per page).

**How to apply:** order of checks for a Core-layer change: `dotnet test` → `dotnet format`/`xstyler` → a narrow targeted script for the specific control (if one exists or is cheap to write) → the full `pretend.ps1` walk once, only right before publishing/committing. Never loop the full walk hoping a flaky check clears. See [[always-update-daily-copy]] for what happens after checks pass, and [[away-no-prompts]] for the related but distinct case of minimizing disruption while the owner is away rather than present and watching.


---

**NOTE (2026-10-17, memory migration):** merged with `feedback_scoped_pretend_runs.md` and `project_speed_up_agent_checks.md` into Tenon's 'Cheapest check first' verifying standard.
