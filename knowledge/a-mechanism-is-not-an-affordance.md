---
name: a-mechanism-is-not-an-affordance
description: "If two things are possible, show two controls — a second meaning reachable only by guessing is not being offered"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 0678a02a-d7b3-4614-8ae9-4e6f818eaa23
  modified: 2026-09-22T10:41:58.908Z
---

The owner, on a save flow where one button opened a dialog and changing the name
turned a replace into a new preset: "the save preset button implies an
overwrite and isn't clear that to save it as a new preset you have to change
the name. this is something you should have caught from a users perspective."

**Why:** the mechanism was sound and the affordance did not exist. Nothing on
screen said renaming was how you got a new preset, so nobody would find it.
Elegance in how a control resolves two intents is worth nothing if a person
cannot see that both are on offer.

**How to apply:** when a design collapses two user intents into one control,
that is a warning, not a win — surface both. Before proposing an interaction,
ask what a first-time user would see, not what the control can do. This is
the counterweight to [[design-by-removal-not-addition]]: remove what is not
needed at that moment, never remove the evidence that a choice exists.
