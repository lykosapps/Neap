---
name: feedback-long-runs-background
description: "Run anything that could take minutes (UI scripts, builds, checks) in the background with a hard time limit and a log file, and say so in a line when it is slow, so it never looks hung"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 11ae904b-61a5-4365-8048-f96c391c9f84
  modified: 2026-10-14T22:44:17.652Z
---

Run any script or check that could take more than about 30 seconds in the background, with a timeout and output going to a log file I can read as it goes. Say in a few words what is running if it takes a while.

**Why:** on 2026-10-05 an accessibility script sampled pixel colours too slowly (a sort that recomputed luminance per pixel) and sat silently for minutes. The owner thought it had hung, interrupted it, asked twice if I was stuck, and told me to catch that next time rather than get stuck indefinitely.

**How to apply:** start long runs with run_in_background and a timeout, write progress lines to a file (a fresh filename per run, since an old run can still hold the file), and read the log instead of waiting blind. A foreground polling loop that waits on a process started with Win32_Process.Create counts as a long run too: on 2026-10-15 a 200-second foreground wait for the screen checks drew "why aren't you doing it in the background?" Start the outside process, then wait on it with a run_in_background command that exits when it does. Write scripts so slow work is cheap (dedupe before computing, sample instead of scanning every pixel). If something has produced no new output for a minute, stop it, find the slow step and fix it, rather than waiting. Related to [[feedback-scoped-pretend-runs]].
