# Stealth Pro II

Supported. It is the headset Neap is built for. Every function has been tried
on the owner's headset, on one Windows PC and on a Steam Deck's desktop. The
gaps are Swarm II features that run in its Waves software, Swarm II's hotkeys,
and its battery warnings.

- **Hardware:** Xbox black: docks `2283`, `2284`; transmitters `2285`, `229F`;
  headset `2286`. Xbox white: docks `229B`, `229C`; transmitters `229D`,
  `22A0`; headset `229E`. PC: dock `2287`, transmitter `2288`, headset `2289`.
  Only `229B`, `229D` and `229E` have been plugged in here.
- **Turtle Beach's feature list:**
  [Stealth Pro II PC – Swarm II App (Desktop Version)](https://support.turtlebeach.com/s/?language=en_US&co=en_US#/390/Stealth-Pro-II/1/PC/article/a32UR0000025kpyYAA),
  updated 7 October 2026. The owner's headset is the Xbox edition, so check
  the Xbox article for any differences.
- **Testers:** the owner, on Windows and on a Steam Deck (SteamOS, desktop mode).
- **Latest release:** 0.2.1.

## Connection

| System | Neap connects | How sure | Next |
|---|---|---|---|
| Windows | Yes: Charging Dock, USB Transmitter and USB-C cable | Confirmed | — |
| Linux, Steam Deck | Yes, after the one-time access rule | Confirmed | — |
| Linux, other systems | Only against the pretend headset | Untried | A tester with another system. |

## Features

"Confirmed" on Linux means it was checked on the Steam Deck, the only Linux
system tried with a real headset.

### Driver and profile

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Driver: Turtle Beach, Dolby Atmos, Waves | Not planned | — | — | — | Neap works with whatever driver the PC uses. Spatial sound covers Dolby on Windows. |
| On-board profile slots (5) | Not planned | — | — | — | Neap keeps its own profiles, switched by the game or app in use. |

### Audio

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Master volume, on/off | Has | Confirmed | Confirmed | Done on the PC | — |
| Master volume, level | Has | Confirmed | Confirmed | `0x2A0`, mirror `0x260` | — |
| Superhuman Hearing, on/off | Has | Confirmed | Confirmed | `0x720` | — |
| Superhuman Hearing, type (Legacy, Footsteps, Gunshots) | Has | Confirmed | Confirmed | `0x730` | — |
| Superhuman Hearing, intensity | Has | Confirmed | Confirmed | `0x740` | — |
| Game and chat mix | Has | Confirmed | Confirmed | Done on the PC; the chat wheel reports at `0x510` | Works on the Xbox edition, unlike Swarm II's. Once, the mix didn't follow the wheel; see Problems. |
| Active noise cancellation, on/off | Has | Confirmed | Confirmed | `0x750` | Neap adds transparency: noise cancellation at zero. |
| Active noise cancellation, level | Has | Confirmed | Confirmed | `0x760` | — |
| Chat Boost (Bold, Dynamic, Active) | Missing | — | — | Waves software on the PC | Decide whether Neap should do it. |
| Waves 3D (Games, Music, Movies) | Missing | — | — | Waves software on the PC | Not planned unless Neap does its own sound processing. |
| High Bandwidth Audio (24-bit, 96 kHz) | Has | Confirmed | Missing | The PC's audio format | Neap also puts the dock back into high resolution after an app has used the mic. Linux has no format choice yet. |
| Spatial sound | Has | Confirmed | Missing | Windows | Windows only. |

### Microphone

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Mute | Has | Confirmed | Confirmed | Done on the PC; `0x600` reads the boom arm, and a write to it is ignored | — |
| Sensitivity | Has | Confirmed | Confirmed | Done on the PC; `0x610` mirrors the PC's microphone level | — |
| Monitoring, on/off and level | Has | Confirmed | Confirmed | `0x620` | — |
| Noise gate, on/off | Has | Confirmed | Confirmed | `0x700` | — |
| Noise gate, level | Has | Confirmed | Confirmed | `0x710` | — |
| High Bandwidth Microphone (16-bit, 48 kHz) | Has | Confirmed | Missing | The PC's audio format | As High Bandwidth Audio. |
| AI noise reduction | Has | Confirmed | Confirmed | `0x630` | — |

### Equaliser

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Game equaliser, 4 presets and 10 bands | Has | Confirmed | Confirmed | `0x1210`–`0x12C0` | Neap adds a parametric equaliser and a test tone. |
| Mic equaliser, 4 presets and 10 bands | Has | Confirmed | Confirmed | `0x1310`–`0x13C0` | — |
| Custom presets on the headset, save and delete | Has | Confirmed | Confirmed | `0x1600`–`0x1630` | Swarm II's software-only presets aren't in Neap; Neap's are all on the headset. |

### Assignment

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Mode button: noise cancellation, cycle game presets, noise gate | Has | Confirmed | Confirmed | `0xB20` | While Neap runs, the Mode button steps through noise cancellation, transparency and off. |
| Lower wheel: monitoring, game and chat mix, bass, treble, noise gate | Has | Confirmed | Confirmed | `0xA20` | — |

### Additional settings

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Battery level | Has | Confirmed | Confirmed | `0x240` | Shown as "Settling…" until a fresh battery's reading steadies. |
| Spare battery charging in the dock | Has | Confirmed | Confirmed | `0x250` | Shown with a bolt while it charges, since the level isn't reliable then. |
| Drivers status | Not planned | — | — | — | Swarm II's own drivers. |
| Connection, transmitter status, platform | Has | Confirmed | Confirmed | `0x290`, `0x400`–`0x460` | — |
| Pop-up battery warnings | Missing | — | — | Done on the PC | Decide whether Neap should warn. |
| Battery in the notification area | Has | Confirmed | Confirmed | — | Shown in the icon's status. |
| Auto shut-off (off, 5, 10, 20, 30 min) | Has | Confirmed | Confirmed | `0x280` | — |
| Wake on motion | Has | Confirmed | Confirmed | `0x2E0` | Turtle Beach's article lists it in Swarm II's desktop app, but the README says the desktop app lacks it. Check, and correct the README. |
| Hotkeys: volume mute, Superhuman Hearing, noise gate, mic mute, noise cancellation | Missing | — | — | — | Neap has hotkeys for the mix only. Profile hotkeys are on the backlog. |
| Voice prompt for each transmitter (none, 1–4) | Missing | — | — | Unknown | Turtle Beach's article lists it under Charging Hub. Find where it's kept. |
| Voice prompt volume | Has | Confirmed | Confirmed | `0x2D0` | Swarm II's desktop app has had it switched off since 1.0.0.47. |
| Dock light brightness, two lights | Has | Confirmed | Confirmed | `0x401`, `0x402` | — |
| Firmware updates | Not planned | — | — | — | Neap never writes firmware. |

### Swarm II's own settings

| Feature | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Updates | Has | Confirmed | Confirmed | — | One button on Windows; Linux opens the download page. |
| Start with the PC | Has | Confirmed | Confirmed | — | — |
| Language | Not planned | — | — | — | English only, written so it can be translated. |
| Macro manager | Not planned | — | — | — | Out of scope. |

### Physical controls

| Control | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|
| Volume wheel | Has | Confirmed | Confirmed | `0x260` | — |
| Flip-to-mute microphone | Has | Confirmed | Confirmed | `0x600` | — |
| CrossPlay button (switch transmitters) | Has | Confirmed | Confirmed | `0x400`–`0x460` | Moving between PCs needs the headset switched off and on; see Problems. |
| Bluetooth button | Has | Confirmed | Confirmed | `0x290`, `0x320` | — |

## Problems

| Problem | Found in | Status | Next |
|---|---|---|---|
| The mix didn't follow the chat wheel, on Windows, with the headset connected | Before 0.2.1 | Seen once | Needs the headset to pin down. |
| After the link dropped and came back several times, profiles kept loading and the mic button stopped working | Before 0.2.1 | Not reproduced | Needs the headset, with no other program asking the transmitter. |
| Neap showed the headset off for 30 hours while sound played through the dock | Before 0.2.1 | Logged better; waiting for it again | Read the log next time. |
| Moving the headset to a transmitter on another PC needs it switched off and on before settings can be read | Before 0.2.1 | Open; high priority | Research how Swarm II handles it. Every experiment needs the owner's go-ahead. |
| With both transmitter lights amber, no sound can still show as connected | Before 0.2.1 | Known limit | — |
| At power-on, a transmitter's stale answer can show the wrong transmitter for about 16 seconds | Before 0.2.1 | Known limit | — |

## Next

- [ ] Check whether Swarm II's desktop app has wake on motion, and correct
      the README if it does.
- [ ] Find where the per-transmitter voice prompt (none, 1–4) is kept.
- [ ] Decide on hotkeys for more than the mix, Chat Boost and battery warnings.
- [ ] Read the Xbox edition's article for anything it lists differently.
- [ ] Linux: the audio format on other systems, and a tester on a system
      other than the Steam Deck.

## Evidence

Newest first. [FINDINGS.md](../../FINDINGS.md) has every key and how each
was confirmed.

- 2026-10-17 — Turtle Beach's PC article read: the feature list above. It
  adds a per-transmitter voice prompt (none, 1–4) and lists wake on motion in
  the desktop app.
- 2026-10-08 — Release 0.2.0: every function tried on the owner's headset on
  Windows and on a Steam Deck's desktop.
- Before 0.2.0 — Each key found by watching every value while the hardware
  was used, then confirmed by writing it back; see FINDINGS.md.
