---
name: feedback_headset_device_test_default
description: Default to testing headset investigations on the real device when safe; ask with a confidence level when not
metadata:
  node_type: memory
  type: feedback
  originSessionId: e6963d27-935a-40f5-be9f-d03e42d4e6b1
  modified: 2026-10-16T07:12:29.756Z
---

For headset investigation / reverse-engineering work, the default is to proceed to test on the real device when the agent judges the action safe — do not default to desk-only. When an action is not clearly safe, stop and ask the owner first, presenting a confidence level and what could go wrong, rather than guessing or silently staying desk-only.

The risk line:
- **Safe → do it on the device:** reads, and runtime non-persistent commands (e.g. AppkeyCode volume up/down 0x0A/0x0B, pretend-mode, heartbeat reads). Worst case is undone by a power cycle.
- **Not clearly safe → stop, ask, give a confidence level:** persistent writes — NVKey, firmware, chip settings. The brick-risk category (an SP2Tools firmware hook once stopped the headset starting), so "safe" is not assured there.

**Why:** The owner wants momentum on device-confirmable questions without a round-trip for every safe step, while keeping the genuinely dangerous writes gated behind an explicit decision.

**How to apply:** Classify each device action against the line above. Safe → do it, but serialize device access first (stop the running Neap / try-it copy so a probe does not steal Neap's reports — see [[feedback_no_hid_alongside_app]]). Unsafe or uncertain → ask with a confidence level and the failure modes. This governs headset testing; launching the real Neap app itself still follows [[feedback_no_real_launch]]. Related: [[feedback_long_runs_background]].


---

**NOTE (2026-10-17, memory migration):** owner decision D2 (2026-10-17) makes this the rule in every project: safe reads and non-persistent commands proceed without asking; writes and anything persistent need an explicit GO with a 1-5 risk rating, what could go wrong and the expected outcome. SP2Tools' 'ask before any read' rule is superseded by it.
