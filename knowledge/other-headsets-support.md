---
name: other-headsets-support
description: Findings and plan (2026-09-25) for supporting other Turtle Beach headsets in Neap; parked until the repo is public
metadata:
  node_type: memory
  type: project
  originSessionId: 78fcc4ad-e7fe-48cd-819c-17d7b1ff1c9d
  modified: 2026-09-25T04:53:32.147Z
---

On 2026-09-25 the owner asked whether Neap could support other Turtle Beach headsets. Parked until Neap is public on GitHub; then ask the community for help.

- **Same platform as the Stealth Pro II** (Swarm II's catalogue, `C:\Program Files\Turtle Beach Swarm II\settings.json`, tags them all "tb v2"): Stealth 700 Gen 3, Stealth 600 Gen 3, Stealth 500 (incl. NS), Atlas Air. Not a fit: Stealth Pro gen 1 (older, off in Swarm), Atlas 200 (different chip), Gen 2 / MAX and older (Audio Hub era).
- **Confirmed:** the 600 Gen 3 uses the identical HID channel and RACE framing (lx-exe's code matches Neap byte for byte). **Unknown:** whether its set_kvp keys/categories match the Pro II's.
- **Design consequence:** only the Pro II has noise cancellation/transparency, the Charging Dock, multiple transmitter slots and CrossPlay. Noise Control and Transmitters screens must adapt; the rest carries over. First step: screens that show only what a headset can do; then one model at a time, only with an owner to test. Start with the 600 Gen 3 (most demand, channel confirmed).
- **Decided not to:** fetch other models' Swarm plugins without the headset (needs Swarm's embedded keys or guessing server URLs), or touch firmware flashing (race-toolkit can; bricking risk, against Neap's rules).
- **Community ask:** the owner wants owners to contribute Swarm files once public. Caution: Swarm's plugin files are Turtle Beach's; prefer asking for Neap probe output and captures over public posting of their files. The probe finds any Turtle Beach device on the same HID channel (not limited to Pro II ids) and `read` changes nothing. The probe is command-line, too hard for non-coders, so the plan (in the backlog as "Help add this headset") is a flow inside Neap: detect an unsupported Turtle Beach headset, read it, strip name and transmitter addresses, show the contents, open a pre-filled GitHub issue to attach the file. GitHub over email (email would expose an address). Guided "change a setting in Swarm while Neap watches" is a later phase.
- **Leads (public GitHub):** lx-exe/stealth600-gen3-usb-fix (600 Gen 3, best), Ennosuke/stealth600_soc (600 Gen 3), appdoze in auracast-research/airoha-fota-mod issue 1 (700 Gen 3). A draft issue for lx-exe exists: asks them to run Neap's read-only probe on Windows. Post from the project account, not the owner's.

**Why:** Demand exists mainly for the 600 Gen 3; nothing can be verified without an owner's hardware.
**How to apply:** When Neap goes public or the topic returns, start here rather than redoing the research.


---

**SUPERSEDED (2026-10-17, memory migration):** the repository is public, and Atlas Air support is under way (GitHub issue 1, test builds 0.2.1.x). The plans are the local `.claude/headset-support-plan.md` and `.claude/atlas-air-plan.md`. Each model's support article is the reference for what it has (`feedback_headset_support_page.md`).
