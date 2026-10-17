---
name: feedback-flow-review-absence
description: "Flow and UI reviews must look for what a screen doesn't offer (ways to supply an input), from the person's real situations, and must not downgrade findings for being \"by design\" or unrun"
metadata:
  node_type: memory
  type: feedback
  originSessionId: cfc7bd46-df47-49fc-88b4-4526f8cd1618
  modified: 2026-09-28T02:33:48.131Z
---

When reviewing a flow, judge whether the person can finish in their real situation, not whether each control works. Check every list/picker for what its source can't see and for missing ways to bring something in from outside the app (browse, paste, drag, type).

**Why:** the owner caught that the profiles review saw the apps picker only listed apps making sound now (finding 8) but rated it 2 and never named the real gap: no way to browse for a program, so a game can't be assigned before it runs. Causes: judged each screen on its own instead of the task from the person's moment; pretend-mode fixtures (two running apps) hid the gap; I built the feature so I knew what fed the list; I downgraded it as "by design / not run".

**How to apply:** before walking, write 3-4 real situations ("game about to launch"); ask "what could they need here that this doesn't offer?" at every input step; compare with 2-3 similar products; never lower severity for by-design, cost or unrun (record those separately). I added these to the local flow-review skill (`~/.claude/plugins/cache/tenon/tenon-design/0.10.0/skills/flow-review/SKILL.md`), a plugin cache that an update can overwrite: re-apply them if they are missing. Also keep review runs small (see [[feedback-scoped-pretend-runs]]).


---

**SUPERSEDED (2026-10-17, memory migration):** these additions are now in Tenon's own source (the `tenon-design` flow-review skill, from Tenon 0.15.0), so a plugin update no longer loses them. The plugin cache is not the place to fix a skill.
