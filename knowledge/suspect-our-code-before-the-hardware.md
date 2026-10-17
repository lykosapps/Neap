---
name: suspect-our-code-before-the-hardware
description: "When hardware behaves 'intermittently', the intermittency is usually which of our code paths ran — check our own writes before blaming the device"
metadata:
  type: feedback
---

Twice in one session on the Stealth Pro II I concluded the headset was
misbehaving, wrote it up as a device quirk, and was wrong both times.

First: the endpoint "intermittently refused its own audio format". It did
not. We were writing 24-bit padded into a 32-bit container instead of
packed, so every 24-bit format we installed was one the device rejected.
It looked intermittent only because 16-bit happened to work by accident
(container and depth are the same number), so whichever path had run last
decided whether things worked.

Second: the hi-res LED "went purple and straight back to white", which I
was ready to call a link quirk. It was our own code reopening the audio
device too fast after a format change and catching the renegotiation.

**Why:** an intermittent-looking hardware fault is very often a
deterministic software fault with two code paths. The device is the same
every time; what varies is which of our branches ran. Blaming the hardware
also feels safe, because nobody can contradict it quickly — which is
exactly why it should raise suspicion, not lower it.

**How to apply:** before writing "the device does X intermittently", find
the two cases and ask what *we* did differently between them. Prefer a
test that exercises our write path directly over one that only observes
the symptom. And when a finding blames hardware, say what would falsify it
so the next session can check rather than inherit it. Correct the earlier
write-up in the repo when it turns out wrong; a confident wrong note costs
more than no note.

Related: [[parsers-must-not-drop-silently]],
[[confirm-which-device-youre-addressing]], [[semantics-come-from-the-manual]]
