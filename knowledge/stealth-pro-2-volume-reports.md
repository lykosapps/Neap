---
name: stealth-pro-2-volume-reports
description: "Volume complaints from r/TurtleBeach for the Stealth Pro II (Oct 2026) — groups, what users say, what is unconfirmed, and what Swarm II's binaries actually do"
metadata:
  node_type: memory
  type: project
  originSessionId: 3ce8a83d-6554-414f-8341-88f0f46f852b
  modified: 2026-10-16T06:26:34.465Z
---

Log of volume complaints gathered for the owner's planned volume work. Sources are user self-reports, not confirmed bugs. All reporters are Swarm II users; the owner wants to know whether Neap can do better than Swarm on the same hardware.

**Source:** r/TurtleBeach thread "still low volume" (comments/1x5poo7). Reddit could not be fetched; the owner pasted the comments on 15 Oct 2026. Logged as issue lykosapps/Neap#6.

**Support article (not Reddit):** Turtle Beach says the Stealth Pro on PlayStation can sound unusually quiet with the transmitter in PC mode on firmware from 10 Aug 2023; a 29 Aug 2023 update was meant to fix it. Article is for the original Stealth Pro, not confirmed for the Pro II.

**Groups from the pasted comments:**

1. **Too quiet even at max.** Xbox Series X, PC and PS5. Tops out ~40%; latest update did not fix it for them. Some use +12 dB EQ (Peace EQ). Some suspect hardware.
2. **Call volume does not respond.** Cannot change volume during a phone call. One user regained control by toggling phone Bluetooth before a call.
3. **Stops responding after PC sleep.** Windows slider moves, headset volume does not change, until the receiver is unplugged and replugged. Most reproducible; the candidate for an in-app check.
4. **Must sit at 100% in every game on PC.**
5. **Phone spikes.** Volume jumps on a phone; same headset ~1/4 as loud on PS5/PC.

**Swarm II reverse-engineered (REA/Ghidra, 16 Oct 2026):**

- **Master volume is the Windows system endpoint, not a headset setting.** Config blob in `STEALTH_PRO_II.dll` says: *"Master volume and mic use system data"*. Same approach as Neap (registry key 0x2A0 is a read-only mirror). So Swarm has no secret loudness lever Neap lacks.
- **The headset-protocol "VOLUME UP / VOLUME DOWN" are button-remap actions only** (assign a physical button to step volume over the HID/AppkeyCode path), not the master slider. Matches SP2Tools AppkeyCode 0x0A/0x0B.
- **Swarm has NO sleep/resume handling anywhere.** Checked all three binaries: the plugin DLL, `Turtle Beach Device Service.exe`, `Turtle Beach Swarm II Session Monitor.exe`. No power-broadcast / suspend / resume / RegisterPowerSettingNotification / WM_DEVICECHANGE. The Device Service only touches audio endpoints for high-bandwidth mode. The Session Monitor only watches WTS logon/logoff/lock/unlock to relaunch the GUI — not power.

**What this means for Neap:**

- Problems 1/4 (too quiet): hardware/firmware ceiling; both apps use system volume. Only software lever is EQ pre-gain (what users already do). Neap can make it discoverable, not raise the ceiling.
- Problems 2/5 (phone): Bluetooth-to-phone; neither Swarm nor Neap is in that path. Out of scope.
- Problem 3 (sleep): the break is in the USB-audio-class volume relay in the dongle, downstream of both apps. Swarm does nothing on resume — so **Neap adding resume handling (detect the desync; re-assert volume; possibly nudge over the HID AppkeyCode channel, which stays alive) would be doing something Swarm never does.** Unconfirmed whether the HID channel bypasses the broken relay — needs the hardware test.

**How to apply:** Chase problem 3. Reproduce on the owner's headset first (sleep, wake, test Windows slider vs a raw AppkeyCode 0x0A). Neap has no AppkeyCode path today (Frames.cs speaks only set_kvp 02 99); adding it is the experiment. Keep group 1 separate from group 5.

Related: [[feedback_headset_support_page]]
