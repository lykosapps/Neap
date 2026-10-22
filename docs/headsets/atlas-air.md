# Atlas Air

In testing. On Windows it connects and most settings work, with a few on the
wrong key or range. On Linux the transmitter opens but the headset never
answers, so nothing works there yet.

- **Hardware:** USB Transmitter `225E`; the headset reports itself as `2260`;
  firmware 6.163.1646.0. No dock.
- **Turtle Beach's feature list:**
  [Atlas Air – Swarm II App (Desktop Version)](https://support.turtlebeach.com/s/?language=en_US&co=en_US#/331/Atlas-Air/1/PC/article/a32UR0000025kijYAA),
  updated 20 March 2026.
- **Testers:** one on Windows, one on Linux (CachyOS), on
  [issue 1](https://github.com/lykosapps/Neap/issues/1).
- **Latest test build:** 0.2.1.5. 0.2.1.6 is built, not yet published.

## Connection

| System | Neap connects | How sure | Next |
|---|---|---|---|
| Windows | Yes, through the USB Transmitter | Confirmed | — |
| Linux | No. The transmitter opens; every request gets an empty reply. | Fails | Cause unknown. The start-up lock-up SP2Tools saw on the Stealth Pro II's dock didn't reproduce for Neap on that dock. 0.2.1.5 restarts a silent transmitter once and logs what it hears, as a test. |

## Features

### Audio

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Master volume, on/off | Listed | Yes | Has | Seen | Blocked | `0x2A0` | Ask the tester to try the mute. |
| Master volume, level | Listed | Yes | Has | Confirmed | Blocked | `0x2A0`, mirror `0x260` | Slight shake on big dial turns; see Problems. |
| Superhuman Hearing, on/off | Listed | Yes | Has, test | Confirmed | Blocked | `0x800` | The tester heard it work from Neap's writes here, which settles it over the reading of Swarm II's code. |
| Superhuman Hearing, type (Legacy, Footsteps, Gunshots) | Listed | Yes | Has, test | Confirmed | Blocked | `0x810` | — |
| Superhuman Hearing, level | Listed | Yes | Has, test | Confirmed | Blocked | `0x820` | — |
| Game and chat mix | Listed | Yes | Has | Confirmed | Blocked | Done on the PC | Saved in profiles from 0.2.1.5; see Problems. |
| Chat Boost | Listed | Unknown; probably only on the PC | Missing | Untried | Blocked | Unknown; probably done on the PC | Decide whether Neap should do it. |
| Waves 3D (Games, Music, Movies) | Listed | Only with Neap's own sound processing | Missing | Read | Blocked | Done on the PC by Turtle Beach's Waves software | Not planned unless Neap does its own sound processing. |
| High Bandwidth Audio | Listed | Yes, through the PC's sound format | Has | Seen | Missing | The PC's sound format: the transmitter offers 48 and 96 kHz | Neap's format choice on Device, Windows only; recordings read the format. Ask the tester to try 96 kHz. |

### Microphone

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Microphone mute | Listed | Yes | Has | Seen | Blocked | `0x600`; no `0x640` or `0x650` in the recordings, so the arm alone decides | Ask the tester, in the next test build: with the arm down, Neap's switch should say "Muted on this PC"; with the arm up, "Muted by the boom arm", and the switch can't be pressed. |
| Sensitivity | Listed | Yes | Has | Fails | Blocked | Done on the PC; on the Stealth Pro II `0x610` mirrors the PC's microphone level. Swarm II writes `0x610` here, 0–100 (Read). | Jumps to 100% when the tester speaks; see Problems. |
| Monitoring, on/off | Listed | Unknown: Swarm II's switch writes nothing | Hidden | Untried | Blocked | Swarm II's switch writes nothing (Read) | Probably no on/off; the level alone. |
| Monitoring, level | Listed | Yes, at `0x630` (Read) | Has, test | Untried | Blocked | `0x630`, 0–100 (Read) | In 0.2.1.5; ask the tester to listen. |
| Noise gate, on/off | Listed | Yes | Has, test | Confirmed | Blocked | `0x700` | — |
| Noise gate, level | Listed | Yes | Has, test | Confirmed | Blocked | `0x710` | Heard clear at 0%, cut at 50%, almost muted at 100%. |
| High Bandwidth Microphone | Listed | Yes, through the PC's sound format | Has | Seen | Missing | The PC's sound format: the transmitter offers 16 and 48 kHz | As High Bandwidth Audio. |

### Equaliser

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Game equaliser, 4 presets | Listed | Yes | Has | Seen | Blocked | `0x1210` | Ask the tester to listen. |
| Game equaliser, 10 bands | Listed | Yes | Has | Seen | Blocked | `0x1220`–`0x12B0` | As above. |
| Mic equaliser, 4 presets | Listed | Yes | Has | Seen | Blocked | `0x1310` | As above. |
| Mic equaliser, 10 bands | Listed | Yes | Has | Seen | Blocked | `0x1320`–`0x13B0` | As above. |
| Custom presets, save and delete | Listed | Yes | Has | Untried | Blocked | `0x1600`–`0x1630` | Ask the tester to save and delete one. |

### Additional settings

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Battery level | Listed | Yes | Has | Seen | Blocked | `0x240` | — |
| Connection and transmitter status | Listed | Yes | Has | Confirmed | Blocked | `0x290` | — |
| Drivers status, platform | Listed | — | Not planned | — | — | — | Swarm II's own drivers; nothing for Neap to show. |
| Auto shut-off (never, 5, 10, 20, 30 min) | Listed | Yes | Has | Seen | Blocked | `0x280` | Ask the tester to check one. |
| Battery pop-up warnings | Listed | Yes, on the PC | Missing | — | — | Done on the PC | Decide whether Neap should warn. |
| Transmitter light brightness | Listed | Yes, at `0x430` (Read) | Has, test | Untried | Blocked | `0x430` in group `TX`, 0–100 (Read) | In 0.2.1.5: written and read there. Ask the tester. |
| Voice and tone prompt level | Listed | Yes | Has, test | Untried | Blocked | `0xC00`, 0–15 (Read) | 0–15 in 0.2.1.5; ask whether every step changes it. |
| Firmware updates | Listed | Yes, but Neap never writes firmware | Not planned | — | — | — | Neap never writes firmware. |

### Swarm II's own settings

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Updates | Listed | Neap's own | Has | Seen | Seen | — | Neap's own; both logs show the check running. |
| Start with the PC | Listed | Neap's own | Has | Untried | Untried | — | Neap's own. |
| Language | Listed | Yes | Not planned | — | — | — | English only, written so it can be translated. |
| Macro manager | Listed | Yes, on the PC | Not planned | — | — | — | Out of scope. |
| Profiles | Listed | Neap's own | Has | Seen | Blocked | — | Neap's own, by the app in use. Marks the one in use from 0.2.1.5. |

### Physical controls

From the quick start guide. Neap describes them; it doesn't set them.

| Control | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Volume wheel | Quick start guide | — | Has | Confirmed | Blocked | `0x260` | — |
| Flip-to-mute microphone | Quick start guide | — | Has | Seen | Blocked | `0x600` | See Problems. |
| QuickSwitch (transmitter or Bluetooth) | Quick start guide | — | Has | Seen | Blocked | `0x290` | — |
| Bluetooth button (pairing, calls) | Quick start guide | — | Has | Confirmed | Blocked | `0x290`, signal `0x310` | The tester says Bluetooth shows properly. |

### Shown by Neap, not on the Atlas Air

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| AI noise reduction | Not listed | No: the headset doesn't have it | Hidden | Fails | — | Neap writes `0x630`, which is mic monitoring here | Not offered from 0.2.1.5. |
| Dial and button functions | Not listed; the mobile article lists them, the quick start guide doesn't | No: Swarm II writes none | Hidden | — | — | `0xA00`–`0xB20`, never written by Swarm II (Read) | Already hidden on the Atlas Air. |
| Headset name | — | Yes | Has | Untried | — | `0x220` holds "My Headset" | "Atlas Air" from 0.2.1.5, and "Headset" with nothing connected. |

## Problems

| Problem | Found in | Status | Next |
|---|---|---|---|
| Linux: the headset never answers | 0.2.1.2 | Open | See Next. |
| Master volume shakes on big dial turns | 0.2.1.2 | Much better in 0.2.1.3; rare | Leave unless it gets worse. |
| Microphone shows on at start with the boom up, until it's lowered and raised | 0.2.1.2 | Probably fixed: the 0.2.1.3 recording reads the boom up from the start | Ask the tester. |
| Sensitivity slider jumps to 100% when the tester speaks | 0.2.1.2 | Probably fixed: in the 0.2.1.3 recording the level follows the slider with no jump | Ask the tester. If it comes back, check whether Teams' automatic gain is raising the PC's microphone level. |
| Game and chat mix isn't saved in profiles | 0.2.1.2 | Fixed for 0.2.1.5: on a headset with no chat wheel, profiles save and restore the mix (decision of 17 October) | Ask the tester. |
| Profiles page doesn't say which profile is active | 0.2.1.2 | Fixed for 0.2.1.5: the profile in use is marked "In use" | Ask the tester. |
| Master volume's mute button is highlighted when muted | 0.2.1.2 | By design: the mute button is on while muted | Say so in the reply. |
| Mic monitoring does nothing | 0.2.1.5 | Fixed for 0.2.1.6: every level above 1 was refused, checked as the Stealth Pro II's switch at `0x630` | Ask the tester to listen. |
| Voice prompts slider shakes, and shows 0–100 until moved | 0.2.1.5 | Fixed for 0.2.1.6: sliders move in whole steps; the headset held 100, written by 0.2.1.3, now shown at the slider's end | Ask the tester. |
| Microphone shows Muted after the headset is switched on with the arm down | 0.2.1.5 | Fixed for 0.2.1.6: a mute the headset set is undone on the first reading with the arm down, across restarts of Neap | Ask the tester. |
| Transmitter light has no reading at start | 0.2.1.5 | Open: `STX` gets no answer; writes to `0x430` work | 0.2.1.6 names the silent group in the log and recordings. |
| Linux: the restart switches the headset off | 0.2.1.5 | Open: the restart is once per plug-in only while Neap runs, so likely on every start of Neap | Ask the tester whether a second start of Neap restarts it again. |

## Next

- [x] Linux: a test build that also listens for answers on the input side of
      the control channel, and logs what it hears there. In 0.2.1.5.
- [x] Make Neap restart a silent transmitter itself on Linux, once per
      plug-in, reading it as it comes back. In 0.2.1.5; not yet tried on any
      Linux machine with a real transmitter.
- [ ] Ask the Linux tester to try 0.2.1.5: allow access again when Neap asks,
      then record.
- [ ] Publish test build 0.2.1.5 and ask both testers to try it. Built:
  - [x] voice prompts on 0–15
  - [x] transmitter light at `0x430`
  - [x] mic monitoring level at `0x630`
  - [x] hide AI noise reduction
  - [x] show "Atlas Air" as the name
- [ ] Fix the microphone state at start and the sensitivity jump, once the
      tester has tried 0.2.1.3's slider fix.
- [ ] Ask the Windows tester to check the equalisers, a custom preset, auto
      shut-off and the master volume's mute.
- [ ] Decide on Chat Boost, battery warnings and the High Bandwidth switches.
- [ ] Add what Swarm II's Atlas Air plugin showed to SP2Tools.

## Evidence

Newest first.

- 2026-10-18 — Windows tester, 0.2.1.5. Voice prompts and the transmitter
  light change the sound and the light. The light has no reading at start.
  The voice prompts slider shakes. Mic monitoring does nothing: the
  recording shows 99 writes to `0x630` refused as a switch. The mic shows
  Muted after switching on with the arm down; the log shows Neap started
  with the headset off, so it had no earlier reading.
- 2026-10-18 — Linux tester, 0.2.1.5. After allowing the restart, the
  transmitter answered and the headset connected. The restart switched the
  headset off. The battery showed Settling once it was back on, as it does
  on Windows for the first minutes after switching on.
- 2026-10-18 — Tag `v0.2.1.4` failed its release build (the read-pace test
  on Linux caught reads about every 14 ms) and made no release. Fixed, all
  tests run on Linux first, and released as 0.2.1.5.

- 2026-10-18 — The owner's dock on Linux again, with 0.2.1.5's read pace:
  Neap answered in 0.6 seconds. The restart read 16 reports as the dock came
  back, the first holding the same start-up messages SP2Tools recorded from
  a real unplug (`05 5d 03 00 10 31 ..`, `05 5c 03 00 80 2c 01`,
  `05 5c 03 00 80 2c 03`, `05 5d 0e 00 b1 2c ..`), then Neap's own replies.
  So the restart puts a transmitter through its real start-up, and the log
  shows what it said.

- 2026-10-17 — Before the headset first answers, Neap runs no Atlas Air
  code on Linux: the request is the same bytes as for the Stealth Pro II,
  and the same as on Windows, where the Atlas Air answers. The one
  difference in Neap's own behaviour is pace: on Linux each read takes
  about 1 ms, so Neap asked about every 4 ms (750 reads in three seconds),
  where Windows asks about every 15 ms (190). 0.2.1.5 asks at Windows' pace
  everywhere, as a test of whether the Atlas Air's transmitter can't keep
  up.

- 2026-10-17 — The owner's Stealth Pro II Charging Dock lent to Linux (WSL,
  kernel 6.18, through usbipd), plugged in with nothing reading it, on
  `hid-generic` as the tester's transmitter is. Neap's own connect code
  answered in 0.7 seconds. Neap's restart reset it cleanly, kept the same
  hidraw node, read 16 reports as it came back, and Neap answered again in
  0.4 seconds. So Neap's Linux path works with a real transmitter of this
  family, and the restart works as built. The Atlas Air's silence did not
  reproduce on the dock, so it is particular to the Atlas Air or the
  tester's system.

- 2026-10-17 — Tried to reproduce the lock-up on the owner's Stealth Pro II
  Charging Dock on Windows: replugged it with nothing reading it, headset on
  and its USB-C cable out, then started Neap two and a half hours later. It
  connected through the dock in three seconds, loudness level included. So
  the Stealth Pro II's dock does not lock up this way for Neap; the lock-up
  is not reproduced, and is now a weaker lead for the Atlas Air.

- 2026-10-17 — SP2Tools, proven on the Stealth Pro II's dock (`229B`): the
  dock answers only if the PC has been asking it since it was plugged in.
  Otherwise every reply is empty, `07 00 xx`, exactly as on Linux, and
  unplugging and replugging it with a program already asking puts it right.
  Just after plug-in it sends start-up messages, one of them
  `05 5c 03 00 80 2c 01`, which is the unasked report the Atlas Air's
  transmitter sent Neap on Windows straight after it was plugged in. So the
  Atlas Air's transmitter very likely shares the lock-up. On Linux the
  transmitter is plugged in long before Neap starts, so nothing reads those
  messages. Read across from another device, not yet seen on an Atlas Air.
- 2026-10-17 — The Windows tester's recordings show the transmitter giving
  the Linux symptom exactly, "opened and gave no answer: 190 reads of 62
  bytes with nothing in them, to a request of 62 bytes", then "Connected"
  within a minute, five times between 7 and 16 October: the headset off or
  out of reach. Report sizes are 62 bytes on both systems. While switched to
  Bluetooth the headset went on answering through the transmitter, so
  Bluetooth alone doesn't cause it. Every silent spell ended the moment the
  headset itself reported in (its sound link and battery first, then
  "Connected"), and one began as it switched itself off mid-session, so on
  Windows silence means the headset isn't linked. Just after the transmitter
  is plugged in, Windows' first ask gets an unasked report,
  `070700055C0300802C01`; Linux never has. On Linux, Neap stayed Quiet for
  ten minutes and more, so a headset in use should have linked in that time
  unless something else is wrong.
- 2026-10-17 — 0.2.1.5 built from the plugin's findings: the changes in Next,
  checked by the tests against the pretend Atlas Air. The screen check and the headset are still to come.
- 2026-10-17 — Turtle Beach's Swarm II article for the Atlas Air read: the
  feature list above. It has no AI noise reduction, no dial or button
  functions and no noise cancellation.
- 2026-10-17 — Swarm II's Atlas Air plugin (`ATLAS_AIR.dll` 1.0.0.24, from the
  Windows tester), read without running it. AI noise reduction and the
  monitoring switch write nothing. `0x630` is the monitoring level. Voice
  prompts are `0xC00`, 0–15. The transmitter light is `0x430`, 0–100. The
  name is always "Atlas Air". It also read Superhuman Hearing as going
  through Waves on the PC; the tester has heard it work from the headset's
  own keys, so that reading is set aside. `0x830` and `0xC10` are
  never used.
- 2026-10-16 — Windows tester, 0.2.1.3. Superhuman Hearing works. Master
  volume much better. Voice prompts change only from 1 to 20%. AI noise
  reduction does nothing and gives no beep. The name shows "My Headset", or
  "Stealth Pro II" with nothing connected.
- 2026-10-16 — Linux tester, 0.2.1.3. 750 empty replies to the first request.
  One hidraw device, on interface 3, `hid-generic`. The USB listing shows that
  interface has an interrupt input and no output, so Linux sends requests by
  the same route as Windows.
- 2026-10-15 — Windows tester, 0.2.1.2: notes, a recording, Swarm II's
  `ATLAS_AIR.ini` and the device information. Superhuman Hearing, voice
  prompts and the transmitter light did nothing. The noise gate worked.
  Bluetooth shows properly. The notes also list the problems above.
- 2026-10-15 — Linux tester, 0.2.1.2: the transmitter opens and doesn't
  answer, with or without sudo.
- 2026-10-13 — Linux tester, 0.2.1: no access until the access rule was
  added, then no answer.
- Before 2026-10-15 — Windows tester, 0.2.0 and 0.2.1 recordings: the
  hardware ids. The volume mirror is at `0x260`. `0x290` follows QuickSwitch.
  `0x620` follows the mute.
