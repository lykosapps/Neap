---
name: design-by-removal-not-addition
description: "the owner's standard for UI: at any given moment show what is needed then and nothing more — fix confusing states by replacing them, not by adding a control to explain them"
metadata:
  type: feedback
---

When a piece of interface is unclear, my first instinct is to add
something that clarifies it. The owner's correction: work out what the user
needs *at that point* and show only that.

The case that earned this. Deleting a preset asked for confirmation by
recolouring the chip and changing its "Custom" tag to read "Delete?",
expecting a second click on the cross. Nobody could tell what to press.
My first fix added a red Delete button next to everything already there
— so the preset name was still clickable, the tag was still present, the
cross was still there with a changed meaning, and two red things competed
for the eye. He called it crude, and it was.

What worked was subtraction: while asking, the chip's whole face is
*replaced* by `Delete | Cancel`. The name is not repeated, because it is
the same chip in the same place and they just clicked it. Nothing else is
in the way, and no control changes meaning under the pointer.

**Why:** a confusing state is usually confusing because it is carrying
things that belonged to a different moment. Adding a control to explain
it leaves all of those in place and adds one more thing to read. Asking
"what is needed here, and what is merely still here" removes the
ambiguity at its source.

**How to apply:** before adding an affordance to clarify a state, list
what that state currently shows and strike everything the user cannot act
on or does not need to answer with. Prefer replacing a state over
annotating it. Never leave a control whose meaning changes between
states — a cross that means "delete" in one and "confirm" in another is
worse than no cross.

Two seconds' worth of checking that is easy to skip: does the layout move
when the state changes? Replacing a wide face with two short words
reflowed the row and moved the answer out from under the pointer that had
just asked for it. And always say plainly which is the safe option — put
focus there, not on the destructive one.
