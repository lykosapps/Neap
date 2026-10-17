---
name: fixes-left-on-option-branches
description: "bug fixes made on the Option A/B/Console branches never reached main, so the rebuild (from main) lost them; check those branches before calling a bug new"
metadata:
  node_type: memory
  type: project
  originSessionId: b200c3be-64ae-4c4c-a719-2d840f07a16f
  modified: 2026-09-24T09:48:07.926Z
---

The Neap rebuild (`claude/neap-rebuild`) started from main. Fixes made on the redesign branches (Option B, Console, and others) had not been merged to main, so they were missing. On 2026-09-24 the audio format crash ("Catastrophic failure" on changing the format; fix 4df3400, which keeps the list's items) came back and cost a debugging round before the owner pointed out it had been fixed already.

**Why:** the owner expects a known bug to be recognised, not re-investigated.

**How to apply:** Before debugging a known-looking fault, search main's history, and the old branches in `E:\Dev\neap\archive\branches-2026-09-24.bundle` (fetch it into a scratch clone), for an earlier fix. The branches themselves were removed on 2026-09-25 ([[neap-folders]]).
