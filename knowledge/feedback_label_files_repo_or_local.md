---
name: label-files-repo-or-local
description: "When mentioning any file to the owner, say whether it goes into the repository (and pushed) or stays local only"
metadata:
  node_type: memory
  type: feedback
  originSessionId: fbc97cb2-a5bc-4426-a3dd-08a7a872d8fa
  modified: 2026-10-07T13:11:00.279Z
---

The owner asked (2026-10-08) to start tagging files with whether they go into the repo or stay local, after wondering whether the scratch scripts (health.ps1 etc.) would be published.

**Why:** he can't tell from a path whether something is public; the repo is going public and nothing identifying him may be in it (see the project CLAUDE.md).

**How to apply:** whenever a file is mentioned in chat (or linked), label it: "in the repo" (tracked or about to be committed; pushed when he pushes), "local only" (scratchpad, TestResults, ignored folders, his build folders, memory), or "outside the project" (his machine's own files). When creating a file, say which it is at the time. Keep it to a few words. Scratch work goes in the scratchpad folder, which is never in the repo.
