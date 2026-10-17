RETIRED 2026-10-17: exact duplicate from an older memory folder; see ../parametric-eq-direction.md

---
name: parametric-eq-direction
description: "Neap's parametric EQ and test tone — built and in the daily copy on 2026-09-25; decisions the owner made along the way"
metadata:
  node_type: memory
  type: project
  originSessionId: a78e1739-b7af-4f35-931a-abbfc8f4aa7d
  modified: 2026-09-25T04:09:04.891Z
---

Built on 2026-09-25 and in the owner's daily copy (game bank only; the owner said the mic EQ stays band-by-band, no parametric).

- Four adjustments max (the owner chose to keep four). Width stops at 1 octave, the headset's band width.
- Plot: dashed "Your curve", solid "What you'll hear", gap shaded, band ticks; note only when miss > ~2 dB: "The headset can't match this exactly. The solid line is what you'll hear."
- Presets: adjustments stored in Neap's app settings, headset slot holds the ten gains.
- Choosing Parametric: resumes set-aside adjustments if bands untouched, else re-selects a parametric preset, else starts flat (via selecting the flat factory preset — writing ten bands in a burst showed flat but the sound didn't change).
- Test tone strip above the plot: manual frequency slider, Sweep/Hold/Resume, Stop, level, "Cut here" / "Boost here" (±3 dB, only while held), one instruction line. Plays on the headset output; silent stand-in in pretend runs.

- Layout after the 25 Sep space review: presets open from the preset name (a picker, like Home's tile) so the editor gets the full width; Cut/Boost on the tone's first row when wide; plot 200 tall; "Sent to the headset" folded in an expander. Home shows a Parametric tag + "Open the equaliser", never the tuning controls.

**Why:** the owner's own problem was a loud region near 4.9 kHz between the 4k and 8k bands.
**How to apply:** All headset checks passed 2026-09-25 (tone, sweep between bands, preset with Neap closed, light/high contrast). Build on this rather than re-deciding it.
