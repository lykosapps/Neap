---
name: redesign-review-verdict
description: "Option A vs B review (2026-09-24): A is the design base, rebuilt on a fresh branch from main; the owner accepted all eight decisions as recommended"
metadata:
  node_type: memory
  type: project
  originSessionId: b200c3be-64ae-4c4c-a719-2d840f07a16f
  modified: 2026-09-24T04:42:47.179Z
---

On 2026-09-24 a headless review compared Option A (Console) and Option B (Focus). Nothing in it was confirmed by running. Recommendation, not yet approved by the owner:

- Option A is the design base (mix, mic mute and noise cancelling one click from Home). From B it takes the single column of settings rows on every page but Home, the dial that only reacts to presses on its ring, and state given in words.
- Home is reduced to the future quick panel: status, mix, microphone, noise cancelling, preset.
- The code goes on a new branch from main. Pieces come over file by file from each option, not by merging either branch.

The full review (a part for the owner and a phased build plan) is `redesign-review.md` in the scratchpad of session b200c3be (worktree option-a-b-ui-ux-review-a867f8), next to the `review-prompt.md` that produced it.

Later on 2026-09-24 the owner accepted all eight of the Part 1 decisions as recommended:
- Home holds only status, mix, microphone, noise cancelling and preset.
- No volume on Home.
- Superhuman Hearing stays on Audio.
- The mix folds away when the headset is off.
- The mic meter stays on Microphone only.
- The tray tooltip shows state and battery now.
- The future panel looks like Home.
- The Dock lights move to Device.

**Why:** these settle what Home and the quick panel are, so the rebuild can start without reopening them.

**How to apply:** build to the review's Part 2 plan. Do not re-ask these decisions. Follows [[console-redesign-status]] and [[redesign-builds-folders]]. The quick panel intent is in [[taskbar-quick-access-wanted]].


---

**SUPERSEDED (2026-10-17, memory migration):** the shipped Avalonia app (0.2.0 onwards) replaced this plan. The first paragraph's 'not yet approved' was overtaken by the acceptance later in the same note. `[[console-redesign-status]]` and `[[redesign-builds-folders]]` were never in this memory set, and the scratchpad review file may be gone.
