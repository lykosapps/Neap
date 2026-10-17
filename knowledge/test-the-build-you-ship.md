---
name: test-the-build-you-ship
description: A dev build that runs proves nothing about the release build; verify the artefact that ships
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 7f4b7543-1b28-44f0-9062-3f360160ece2
  modified: 2026-09-22T08:50:38.037Z
---

Verify the packaged, optimised, shipping artefact — not the development build
you have been iterating on. They are different programs.

**Why:** the WinUI app ran perfectly in Debug for a whole session. The Release
publish crashed on launch every time, because the publish step silently left
out the app's own resource index. Nothing in the source was wrong, so no
amount of Debug testing could ever have found it. If it had gone out that way,
the first thing a stranger did with it would have been watch it die with no
message.

**How to apply:** before claiming something is shippable, build it the way it
will ship and run *that*. Measure it too — memory and CPU differ enough
between Debug and Release to make Debug numbers misleading. Sibling of
[[reading-it-back-proves-nothing]]: the thing that succeeded is not
necessarily the thing you care about.
