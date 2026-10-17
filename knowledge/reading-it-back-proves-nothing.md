---
name: reading-it-back-proves-nothing
description: "A setting that reads back correctly is not evidence it took effect — verify by observing the effect, through a different path from the one that wrote it"
metadata:
  type: feedback
---

Twice in one session, the same trap: we wrote a value, read it back, saw
what we expected, and were wrong both times.

On the Stealth Pro II's Windows audio format, writing the property store
persisted the value and every dialog read it back correctly — while the
device carried on at the old rate, no stream was interrupted and the
hardware never switched mode. Changing a device format is a request to the
audio service, not a property write. Earlier the same day, 24-bit written
in the wrong container read back as 24-bit while being a format the device
would not open.

**Why:** a read-back usually consults the same store the write touched, so
it confirms the write, not the effect. Where writing and taking effect are
separate steps — device configuration, caches, anything with a service or
daemon in between — round-tripping the value proves only that you can talk
to yourself. Worse, it is actively reassuring, so it stops the
investigation early and the real cause gets blamed on hardware.

**How to apply:** before trusting a setting, find a second source that
reflects the *effect* and compare. For the audio format that was the
stored default against `IAudioClient::GetMixFormat`; they disagreed on
every attempt and nobody looked for an hour. Generally: what is the system
actually running, as opposed to what did we record? If there is no such
second source, say so rather than reporting success. And when the user
says "doing it the vendor's way works, doing it your way does not", that
is the strongest evidence available — the mechanism differs, so go and
find how.

Related: [[suspect-our-code-before-the-hardware]],
[[confirm-which-device-youre-addressing]]
