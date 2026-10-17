---
name: bluetooth-flag-write-bricked-buttons
description: writing the Bluetooth-flag protocol key once made every headset button stop responding, cause never found
metadata:
  type: project
---

Writing to the Bluetooth flag key (`0x300` range, see [[semantics-come-from-the-manual]] and FINDINGS.md's Bluetooth section) once caused every physical button on the headset to stop responding. Nothing else was touched at the time. A battery pull fixed it; the root cause was never identified and the incident was never reproduced or written up in FINDINGS.md.

**Why:** this happened during early protocol probing of `stealth-pro-ii-control` (Neap's old codename), surfaced only in a raw session transcript, not in any doc.
**How to apply:** treat writes to that key range as riskier than other settings writes until the cause is understood — don't assume it's safe just because later sessions wrote it without incident. If it recurs, that's the second data point worth chasing.


---

**NOTE (2026-10-17, memory migration):** now written into `FINDINGS.md`, Bluetooth section.
