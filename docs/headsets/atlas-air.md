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
- **Latest test build:** 0.2.1.3.

## Connection

| System | Neap connects | How sure | Next |
|---|---|---|---|
| Windows | Yes, through the USB Transmitter | Confirmed | — |
| Linux | No. The transmitter opens; every request gets an empty reply. | Fails | Find why; see Next. |

## Features

### Audio

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Master volume, on/off | Has | Seen | Blocked | `0x2A0` | Ask the tester to try the mute. |
| Master volume, level | Has | Confirmed | Blocked | `0x2A0`, mirror `0x260` | Slight shake on big dial turns; see Problems. |
| Superhuman Hearing, on/off | Has, test | Confirmed | Blocked | `0x800` | Swarm II's code seems to do this on the PC, which clashes with the tester hearing it from Neap. The tester wins. |
| Superhuman Hearing, type (Legacy, Footsteps, Gunshots) | Has, test | Confirmed | Blocked | `0x810` | — |
| Superhuman Hearing, level | Has, test | Confirmed | Blocked | `0x820` | — |
| Game and chat mix | Has | Confirmed | Blocked | Done on the PC | Not saved in profiles; see Problems. |
| Chat Boost | Missing | Untried | Blocked | Unknown; probably done on the PC | Decide whether Neap should do it. |
| Waves 3D (Games, Music, Movies) | Missing | Read | Blocked | Done on the PC by Turtle Beach's Waves software | Not planned unless Neap does its own sound processing. |
| High Bandwidth Audio | Missing | Read | Blocked | Probably the PC's sound format: the transmitter offers 48 and 96 kHz | Check whether it is only Windows' format setting. |

### Microphone

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Microphone mute | Has | Seen | Blocked | `0x600` | Shows on at start with the boom up; see Problems. |
| Sensitivity | Has | Fails | Blocked | `0x610`, 0–100 (Read) | Jumps to 100% when the tester speaks; see Problems. |
| Monitoring, on/off | Hidden | Untried | Blocked | Swarm II's switch writes nothing (Read) | Probably no on/off; the level alone. |
| Monitoring, level | Hidden | Untried | Blocked | `0x630`, 0–100 (Read) | Offer at `0x630` in a test build. |
| Noise gate, on/off | Has, test | Confirmed | Blocked | `0x700` | — |
| Noise gate, level | Has, test | Confirmed | Blocked | `0x710` | Heard clear at 0%, cut at 50%, almost muted at 100%. |
| High Bandwidth Microphone | Missing | Read | Blocked | Probably the PC's sound format: the transmitter offers 16 and 48 kHz | As High Bandwidth Audio. |

### Equaliser

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Game equaliser, 4 presets | Has | Seen | Blocked | `0x1210` | Ask the tester to listen. |
| Game equaliser, 10 bands | Has | Seen | Blocked | `0x1220`–`0x12B0` | As above. |
| Mic equaliser, 4 presets | Has | Seen | Blocked | `0x1310` | As above. |
| Mic equaliser, 10 bands | Has | Seen | Blocked | `0x1320`–`0x13B0` | As above. |
| Custom presets, save and delete | Has | Untried | Blocked | `0x1600`–`0x1630` | Ask the tester to save and delete one. |

### Additional settings

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Battery level | Has | Seen | Blocked | `0x240` | — |
| Connection and transmitter status | Has | Confirmed | Blocked | `0x290` | — |
| Drivers status, platform | Not planned | — | — | — | Swarm II's own drivers; nothing for Neap to show. |
| Auto shut-off (never, 5, 10, 20, 30 min) | Has | Seen | Blocked | `0x280` | Ask the tester to check one. |
| Battery pop-up warnings | Missing | — | — | Done on the PC | Decide whether Neap should warn. |
| Transmitter light brightness | Wrong | Fails | Blocked | Neap writes `0x401`; Swarm II uses `0x430` in group `TX`, 0–100 (Read) | Move to `0x430` in a test build. On the Stealth Pro II `0x430` is inside the second transmitter slot, so check the Atlas Air reports it in `TX` before writing. |
| Voice and tone prompt level | Wrong | Fails | Blocked | `0xC00`; Swarm II's range is 0–15 (Read) | Map the slider to 0–15. The tester heard changes only up to about 20. |
| Firmware updates | Not planned | — | — | — | Neap never writes firmware. |

### Swarm II's own settings

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Updates | Has | Seen | Seen | — | Neap's own; both logs show the check running. |
| Start with the PC | Has | Untried | Untried | — | Neap's own. |
| Language | Not planned | — | — | — | English only, written so it can be translated. |
| Macro manager | Not planned | — | — | — | Out of scope. |
| Profiles | Has | Seen | Blocked | — | Neap's own, by the app in use. Show which is active; see Problems. |

### Physical controls

From the quick start guide. Neap describes them; it doesn't set them.

| Control | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Volume wheel | Has | Confirmed | Blocked | `0x260` | — |
| Flip-to-mute microphone | Has | Seen | Blocked | `0x600` | See Problems. |
| QuickSwitch (transmitter or Bluetooth) | Has | Seen | Blocked | `0x290` | — |
| Bluetooth button (pairing, calls) | Has | Confirmed | Blocked | `0x290`, signal `0x310` | The tester says Bluetooth shows properly. |

### Shown by Neap, not on the Atlas Air

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| AI noise reduction | Hide | Fails | — | Neap writes `0x630`, which is mic monitoring here | Not in Turtle Beach's list; Swarm II's switch writes nothing. Hide it. |
| Dial and button functions | Hide | — | — | `0xA00`–`0xB20`, never written by Swarm II (Read) | Not in Turtle Beach's list. Hide them. |
| Headset name | Wrong | Fails | — | `0x220` holds "My Headset" | Swarm II always shows "Atlas Air". Show the model name, and not "Stealth Pro II" when nothing is connected. |

## Problems

| Problem | Found in | Status | Next |
|---|---|---|---|
| Linux: the headset never answers | 0.2.1.2 | Open | See Next. |
| Master volume shakes on big dial turns | 0.2.1.2 | Much better in 0.2.1.3; rare | Leave unless it gets worse. |
| Microphone shows on at start with the boom up, until it's lowered and raised | 0.2.1.2 | Open | Check what Neap reads at start. |
| Sensitivity slider jumps to 100% when the tester speaks | 0.2.1.2 | Open; 0.2.1.3's slider fix may cover it | Ask the tester. |
| Game and chat mix isn't saved in profiles | 0.2.1.2 | Open | Check how profiles keep the mix on a headset without `3DT`. |
| Profiles page doesn't say which profile is active | 0.2.1.2 | Request | Decide. |
| Master volume's mute button is highlighted when muted | 0.2.1.2 | Question from the tester | Confirm it's by design and reply. |

## Next

- [ ] Linux: a test build that also listens for answers on the input side of
      the control channel, and logs what it hears there.
- [ ] Ask the Linux tester whether the headset was on the transmitter, not
      Bluetooth, while recording.
- [ ] Test build 0.2.1.4, Windows:
  - [ ] voice prompts on 0–15
  - [ ] transmitter light at `0x430`
  - [ ] mic monitoring level at `0x630`
  - [ ] hide AI noise reduction and the dial and button functions
  - [ ] show "Atlas Air" as the name
- [ ] Fix the microphone state at start and the sensitivity jump, once the
      tester has tried 0.2.1.3's slider fix.
- [ ] Ask the Windows tester to check the equalisers, a custom preset, auto
      shut-off and the master volume's mute.
- [ ] Decide on Chat Boost, battery warnings and the High Bandwidth switches.
- [ ] Add what Swarm II's Atlas Air plugin showed to SP2Tools.

## Evidence

Newest first.

- 2026-10-17 — Turtle Beach's Swarm II article for the Atlas Air read: the
  feature list above. It has no AI noise reduction, no dial or button
  functions and no noise cancellation.
- 2026-10-17 — Swarm II's Atlas Air plugin (`ATLAS_AIR.dll` 1.0.0.24, from the
  Windows tester), read without running it. AI noise reduction and the
  monitoring switch write nothing. `0x630` is the monitoring level. Voice
  prompts are `0xC00`, 0–15. The transmitter light is `0x430`, 0–100. The
  name is always "Atlas Air". Superhuman Hearing seems to go through Waves
  on the PC, which clashes with the tester's ears. `0x830` and `0xC10` are
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
