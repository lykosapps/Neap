RETIRED 2026-10-17: exact duplicate from an older memory folder; see ../diagnose-symptom-not-blame.md

---
name: diagnose-symptom-not-blame
description: When something breaks, check the specific likely cause first, not my own innocence
metadata:
  type: feedback
---

When the owner reports something broke, investigate the actual symptom before checking whether I caused it. Check the thing most directly tied to what we were just doing (a state I already know can change, e.g. Windows' default audio device reverting to speakers) before running generic/systemic health checks. Read the app's own log for the specific symptom before reasoning from general system health — it usually has the answer directly.

**Why:** On 26 Sep 2026, the owner's Dolby Access showed licence trouble right after headset/spatial-sound work. I checked Windows Store services and network reachability (real, but generic, and didn't address the specific symptom) and told him it wasn't me — twice — before actually reading Dolby's own log, which immediately showed the real cause: the headset had dropped from being the default output (something I'd already flagged as a live risk minutes earlier in that same session) and Dolby Access was correctly showing licence status for the speakers instead. He had to push back twice with screenshots before I looked at the right thing.

**How to apply:** lead with "let me check what's actually happening," not "here's why it wasn't me" — let the innocence conclusion fall out of the real diagnosis. Prefer the narrowest, most contextually relevant check over a broad-but-shallow one, especially for a variable already known to be unstable this session. See [[efficient-verification]] for the related "cheapest check" guidance — cheapest means most likely to explain the specific symptom, not just fastest to run.
