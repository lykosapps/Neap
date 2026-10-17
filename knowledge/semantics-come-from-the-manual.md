---
name: semantics-come-from-the-manual
description: "Captures and binaries tell you what a vendor control DOES to the wire; only the vendor's user-facing documentation tells you what it MEANS"
metadata:
  type: feedback
---

When a vendor UI control resists explanation, read the vendor's **public
support documentation** before theorising about the protocol. Reverse
engineering answers "what goes on the wire"; it does not answer "what did
they intend this control to be".

The Stealth Pro II case: Swarm's "Audio Master Volume" toggle sent nothing
to the headset in two captures, so a whole thread was built around it being
a broken or vestigial setting about who may drive Windows volume. The owner read
Turtle Beach's own support article, which says in one line that toggling it
off mutes incoming audio. It is a mute. Every observation then fitted at
once, including the "auto-revert" that had looked like a vendor bug.

**Why:** a control that sends nothing to the device is not evidence of a
broken control — it is evidence that the control acts somewhere else. The
documentation names the *intent*, which is the thing captures structurally
cannot show. Worse, a wrong intent propagates: a misread toggle in the
vendor app became a misleading toggle of our own, copied in good faith.

**How to apply:** for any vendor control being reimplemented, spend the two
minutes on the official product documentation first, and treat a mismatch
between the docs and the captures as the interesting signal rather than
noise. If our UI copies a vendor control, be able to state what that
control does in the vendor's own words — if you cannot, do not ship the
copy.

Related: [[read-the-vendors-own-files]], [[parsers-must-not-drop-silently]]
