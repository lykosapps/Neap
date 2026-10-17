---
name: public-identity-and-commits
description: "how every commit in this repo must be authored — pseudonym lykosapps, UTC times, no AI co-author trailer"
metadata:
  node_type: memory
  type: project
  originSessionId: bda12273-502f-4412-86bf-f0c1654b5fe1
  modified: 2026-09-23T08:40:19.033Z
---

On 2026-09-23 the repo got a fresh history (branch `main`) for open-source publishing. The old 42-commit history is archived at `E:\Dev\neap\archive\old-history-2026-09-23.bundle` and must never be pushed.

Every commit: author `lykosapps <224482331+lykosapps@users.noreply.github.com>` (set in the repo's local git config; the machine-wide identity is the owner's real name and work email), made with `TZ=UTC` so no +HH:MM offset appears, and **no Co-Authored-By trailer**.

**Why:** the owner must not be identifiable from the repo. The old `stealthpro` noreply address belonged to a stranger's GitHub account, and the +HH:MM zone plus "<country>" in the docs narrowed down where the owner lives. The owner chose to disclose AI assistance once, in the README, not on every commit.

A local commit-msg hook (in the repo's .git/hooks folder, shared by all worktrees, never published) refuses any commit that breaks these rules. If a commit is refused, fix the commit; never skip the hook.

**How to apply:** check `git config user.email` before committing in any new clone or worktree. Any branch still based on the old history (e.g. `claude/chat-selection-multi-app-419ec7`) has to be moved onto `main` before its work can be published.
