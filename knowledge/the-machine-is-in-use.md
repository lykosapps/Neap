---
name: the-machine-is-in-use
description: "the owner uses the PC all day while I work; state changing, gaps and everyday device use are normal — mention in a line at most, never dig in"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 0678a02a-d7b3-4614-8ae9-4e6f818eaa23
  modified: 2026-09-23T23:52:44.661Z
---

The owner is at the same machine I am measuring, and uses it throughout the day.
They switch the headset on, unplug transmitters, press CrossPlay, take
meetings on it and change the volume without narrating any of it. State
changing under me is the expected condition.

**Why:** I wrote up a "one-way transmitter" finding built from readings taken
either side of them switching the headset on. They were not two views of one
state, they were two states, and I invented the story joining them. They had
to tell me I "shouldn't be so shocked to see the status of things change".
Later (2026-09-23), after a 30-minute gap in the log while they took a
meeting: don't dig into absences, gaps or ordinary device use such as volume
changes — calling one out is fine, and they will interrupt if it is worth
looking into.

**How to apply:** a reading is evidence about the moment it was taken.
Anything spanning more than a few seconds needs the state confirmed at both
ends, or one line asking what they just did — always cheaper than a
retraction. When something surprising appears, first suspect they changed
something, then [[suspect-our-code-before-the-hardware]], and only then reach
for a new theory. Do not narrate state changes as discoveries, and do not
investigate gaps, idle periods or volume/device changes unprompted: one line
at most, then carry on with the task.

**Windows that steal focus (2026-09-24):** driving the app through UI
Automation pulls its window to the front on every page change, even opened
behind everything and marked no-activate, and that interrupted the owner's work
and took their keystrokes. Never run on-screen UI checks while they are
working; they said to leave solving it for later. Headless tests are fine.

Related: [[drive-the-app-through-accessibility]], [[sample-dont-snapshot]], [[confirm-which-device-youre-addressing]],
[[reading-it-back-proves-nothing]].
