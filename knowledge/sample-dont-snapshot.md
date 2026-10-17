---
name: sample-dont-snapshot
description: "Reading a live value once, after the fact, measures where it settled — not whether it moved"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 0678a02a-d7b3-4614-8ae9-4e6f818eaa23
  modified: 2026-09-22T12:28:27.141Z
---

Testing whether the chat wheel reached the app, I read the mix once before and
once several seconds after the owner turned it. Both read 50, so I reported the
wheel dead. It had moved across the full range and settled back on the centre
detent. The owner: "i think you're wrong - i can see the slider moving." Sampling
every 40ms showed 39 changes from 0 to 100.

**Why:** a single reading answers "what is it now", never "did it change".
Anything with a resting position — a detent, a default, a timeout, a retry —
returns to it, and the snapshot is of the resting position.

**How to apply:** to test whether something responds, sample continuously
across the window and report the range, not the endpoints. This is the same
discipline as the positive control in
[[drive-the-app-through-accessibility]]: prove the harness can see the thing
before trusting it not to. And when the user contradicts a measurement, they
are watching the live UI — believe them and re-measure.
