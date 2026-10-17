---
name: model-per-task
description: "Owner wants background work matched to the cheapest model that can do it, to save tokens"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 59d02808-f707-413b-9c68-6eb1ab3d544b
  modified: 2026-10-16T22:56:05.099Z
---

Match each task to the cheapest model that can do it well, especially for background work.

**Why:** the owner (2026-09-27) doesn't want tokens burned in the background unless necessary.

**How to apply:** Claude can't switch its own session's model (only the owner can, in the model menu), so hand self-contained work to subagents with a model override and a tight brief:
- Haiku: searches and lookups.
- Sonnet: mechanical fixes, small well-specified features, research write-ups.
- Opus (main session): judgment work (code review, security write-up, design proposals) and checking every subagent's output before committing.
Always pass `model` explicitly on every Agent call: never let a subagent inherit Opus. Default to Sonnet, including reverse-engineering passes (owner, 2026-10-17: "Your sub agents should run in sonnet").
Keep subagents in the background; don't spawn for work the main session can do in a few steps.


---

**NOTE (2026-10-17, memory migration):** merged with SP2Tools' delegate-to-cheaper-models note into one Tenon working standard ('Delegate').
