---
name: feedback_no_hid_alongside_app
description: "Never open the headset's control collection (probe, scratch watcher) while Neap is running; it steals reports and Neap shows not connected"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 4ac3c2d9-0253-4992-b291-c4cdd9f0fa47
  modified: 2026-10-08T03:38:10.489Z
---

Never run anything that opens the headset's control collection (Neap.Probe, a scratch watcher, a passive HeadsetClient) while the owner's Neap copy is running. Windows hands each input report to one reader, so the second reader takes replies and wheel readings meant for Neap: on 2026-10-08 a passive wheel recorder caught zero readings and Neap went "Quiet"/not connected within seconds, and the owner noticed ("the polling is messing with the connection again" — it had happened before).

**Why:** the owner uses the try-it copy live; a dropped connection mid-test wastes their time and looks like a Neap bug.

**How to apply:** to see per-reading wheel data, log it from inside Neap itself, or stop the try-it copy first (as the wheel write test did) and restart it after. Audio-only scratch tools (WASAPI meter, beeps) are fine alongside the app.
