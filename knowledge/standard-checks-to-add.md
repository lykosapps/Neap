---
name: standard-checks-to-add
description: "Pre-commit formatting check installed 26 Sep 2026; still to do — a standards review plus required checks on each pull request once the repo is on GitHub"
metadata:
  node_type: memory
  type: project
  originSessionId: 0678a02a-d7b3-4614-8ae9-4e6f818eaa23
  modified: 2026-09-23T23:16:40.752Z
---

As of 2026-09-24 the coding standard in the repo's CLAUDE.md is enforced by
the build (analyzers at latest-recommended, warnings as errors) and by a local
commit-msg hook (pseudonymous identity, UTC, no Co-Authored-By). The GitHub
workflows are written but cannot run yet: the repo has no remote. The owner asked
to keep two additions as a to-do for later:

1. **Before each commit:** a fast formatting check, C# and XAML. Done
   2026-09-26: the pre-commit hook is installed alongside commit-msg.
2. **On each pull request, once the repo is on GitHub:** a review of the
   change against the standard, and the build, tests and formatting checks
   made required before anything joins main.

**Why:** the usual practice is fast checks locally and the full gate on pull
requests. Checking at packaging re-reads unchanged code, after it is already
in a history that will be public.

**How to apply:** raise item 2 when the repo is published. Also recommended before
publishing: one read-through by an experienced .NET developer, which no
automated step replaces. Related: [[test-the-build-you-ship]].


---

**SUPERSEDED (2026-10-17, memory migration):** the repository is on GitHub with workflows in `.github/workflows`; read them for which pull-request checks run now.
