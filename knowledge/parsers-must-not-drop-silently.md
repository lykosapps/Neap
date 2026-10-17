---
name: parsers-must-not-drop-silently
description: "When parsing unknown or reverse-engineered data, never let a pattern discard non-matching input silently — surface it, or the parser hides the discovery"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 69317492-c616-4797-9d17-41d8585ca95f
  modified: 2026-09-21T12:25:05.997Z
---

When writing a parser for data whose shape I do not fully know, a pattern
that only matches what I already expect will silently delete the evidence of
what I got wrong — and I will then report the absence as a finding.

This happened **three times** on the Stealth Pro II project, each time
costing hours and each time sending me down a wrong path:

- An event pattern of `"([A-Za-z]+)"` for a category name dropped every
  `3DT` event, which carried the game/chat mix. I captured the traffic three
  times, told the owner the wheel "emits nothing", and built an elaborate
  workaround for a problem that did not exist.
- A verb pattern of `([A-Za-z_]{3,20})` truncated `SCG1`..`SCG5` to five
  identical `SCG`s, hiding that the slot index is part of the verb.
- A non-greedy `\{.*?\}` for the event body cut nested objects in half, so
  every `SCG1`..`SCG5` reply failed to decode and was dropped. I recorded
  "the headset never answers these" as a finding and built an audible
  16-slot probing sweep to work around it. The headset answers all ten,
  instantly and silently. The sweep should never have existed.

**It is not only parsers.** The same shape turned up again in a *probe*:
to list the audio formats a device accepts we walked a list of sample
rates and asked about each. The list started at 44100, because that is
where music rates start. The headset microphone offers 16000Hz, so it was
never asked about, never found, and the app told the owner the microphone had
exactly one format — a fact about our list reported as a fact about his
hardware. Anywhere a candidate list decides what can be discovered, the
list is a filter with the same failure mode.

**Why:** a silent filter turns "I cannot parse this" into "this does not
exist". The second is a much more confident and much more wrong statement,
and it is the one I end up telling the user.

**How to apply:**

- Make the parser account for everything it sees. Collect what matched
  nothing and report it, so "nothing found" is distinguishable from
  "nothing recognised".
- **A guard that counts "matched" as "understood" does not work.** My
  unrecognised-fragment guard existed for the third bug and stayed silent,
  because the regex matched and only the decode failed. Only count input as
  understood once it has actually produced a parsed result.
- Find structure by counting delimiters, not by pattern-matching the end of
  it. Nested data breaks any regex that guesses where a value stops.
- Prefer permissive character classes (`[A-Za-z0-9_]`) over what the first
  sample happened to contain.
- Before reporting that something produces no data, check the parser
  against a case known to produce data.
- When enumerating what a device or API supports, compare the result with
  what the vendor's own interface shows before reporting it. If our list
  is shorter than theirs, ours is wrong.

Related: [[verify-before-declaring-absence]], [[read-the-vendors-own-files]]
