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
| Windows | Yes: Charging Dock, USB Transmitter and USB-C cable | Confirmed | Once, unplugging the Charging Dock with the cable in left the headset silent until it was switched off and on. Repeat it to learn whether it always does. On the cable alone the chat wheel reaches nothing, read or sent, with `0x500` at 0 or 1; the dial and keys still move the mix. |
| Linux, Steam Deck | Yes, after the one-time access rule | Confirmed | — |
| Linux, other systems | Only against the pretend headset | Untried | A tester with another system. |

## Features

"Confirmed" on Linux means it was checked on the Steam Deck, the only Linux
system tried with a real headset.

### Driver and profile

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Driver: Turtle Beach, Dolby Atmos, Waves | Listed | Partly: Dolby through Windows' spatial sound | Not planned | — | — | — | Neap works with whatever driver the PC uses. Spatial sound covers Dolby on Windows. |
| On-board profile slots (5) | Listed | Unknown | Not planned | — | — | — | Neap keeps its own profiles, switched by the game or app in use. |

### Audio

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Master volume, on/off | Listed | Yes | Has | Confirmed | Confirmed | Done on the PC | — |
| Master volume, level | Listed | Yes | Has | Confirmed | Confirmed | `0x2A0`, mirror `0x260` | — |
| Superhuman Hearing, on/off | Listed | Yes | Has | Confirmed | Confirmed | `0x720` | — |
| Superhuman Hearing, type (Legacy, Footsteps, Gunshots) | Listed | Yes | Has | Confirmed | Confirmed | `0x730` | — |
| Superhuman Hearing, intensity | Listed | Yes | Has | Confirmed | Confirmed | `0x740` | — |
| Game and chat mix | Listed, Waves driver only | Yes, on the PC | Has | Confirmed | Confirmed | Done on the PC; the chat wheel reports at `0x510` | Works on the Xbox edition, unlike Swarm II's. Once, the mix didn't follow the wheel; see Problems. |
| Active noise cancellation, on/off | Listed | Yes | Has | Confirmed | Confirmed | `0x750` | Neap adds transparency: noise cancellation at zero. |
| Active noise cancellation, level | Listed | Yes | Has | Confirmed | Confirmed | `0x760` | — |
| Chat Boost (Bold, Dynamic, Active) | Listed, Waves driver only | Only with Neap's own sound processing | Missing | — | — | Waves software on the PC | Decide whether Neap should do it. |
| Waves 3D (Games, Music, Movies) | Listed, Waves driver only | Only with Neap's own sound processing | Missing | — | — | Waves software on the PC | Not planned unless Neap does its own sound processing. |
| High Bandwidth Audio (24-bit, 96 kHz) | Listed | Yes | Has | Confirmed | Missing | The PC's audio format | Neap also puts the dock back into high resolution after an app has used the mic. Linux has no format choice yet. |
| Spatial sound | Not listed | Yes, on Windows | Has | Confirmed | Missing | Windows | Windows only. |

### Microphone

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Mute | Listed | Yes | Has | Confirmed | Confirmed | Done on the PC; `0x600` reads the boom arm, and a write to it is ignored; `0x650` reads the built-in microphones' mute | With the boom in, nothing found mutes it on the headset, `0x3D` included; see FINDINGS. |
| Sensitivity | Listed | Yes | Has | Confirmed | Confirmed | Done on the PC; `0x610` mirrors the PC's microphone level | — |
| Monitoring, on/off and level | Listed | Yes | Has | Confirmed | Confirmed | `0x620` | — |
| Noise gate, on/off | Listed | Yes | Has | Confirmed | Confirmed | `0x700` | — |
| Noise gate, level | Listed | Yes | Has | Confirmed | Confirmed | `0x710` | — |
| High Bandwidth Microphone (16-bit, 48 kHz) | Listed | Yes | Has | Confirmed | Missing | The PC's audio format | As High Bandwidth Audio. |
| AI noise reduction | Listed | Yes | Has | Confirmed | Confirmed | `0x630` | — |

### Equaliser

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Game equaliser, 4 presets and 10 bands | Listed | Yes | Has | Confirmed | Confirmed | `0x1210`–`0x12C0` | Neap adds a parametric equaliser and a test tone. |
| Mic equaliser, 4 presets and 10 bands | Listed | Yes | Has | Confirmed | Confirmed | `0x1310`–`0x13C0` | — |
| Custom presets on the headset, save and delete | Listed | Yes | Has | Confirmed | Confirmed | `0x1600`–`0x1630` | Swarm II's software-only presets aren't in Neap; Neap's are all on the headset. |

### Assignment

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Mode button: noise cancellation, cycle game presets, noise gate | Listed | Yes | Has | Confirmed | Confirmed | `0xB20` | Stock firmware: while Neap runs, the Mode button steps through noise cancellation, transparency and off. Edited firmware (chosen on Controls, hidden until firmware edits are released): the headset steps through off, transparency and noise cancellation itself, and Neap only follows; on the owner's headset Neap shows Transparency while the headset is in noise cancellation; see Evidence. |
| Lower wheel: monitoring, game and chat mix, bass, treble, noise gate | Listed | Yes | Has | Confirmed | Confirmed | `0xA20` | — |

### Additional settings

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Battery level | Listed | Yes | Has | Confirmed | Confirmed | `0x240` | Shown as "Settling…" until a fresh battery's reading steadies. |
| Spare battery charging in the dock | Not listed | Yes | Has | Confirmed | Confirmed | `0x250` | Shown with a bolt while it charges, since the level isn't reliable then. |
| Drivers status | Listed | — | Not planned | — | — | — | Swarm II's own drivers. |
| Connection, transmitter status, platform | Listed | Yes | Has | Confirmed | Confirmed | `0x290`, `0x400`–`0x460` | — |
| Headset name | Not listed | Shown; changing it is unknown | Has | Confirmed on the dock | Untried; the reading passes a test laid out as sysfs is, on Linux | The USB product string of the piece Neap talks through: "Stealth Pro II Xbox" from the white Xbox dock (Seen, and read by Neap on the headset); "Stealth Pro II Xbox Headset" from the headset on its cable (Seen in Windows' device list). Shown as the firmware gives it, so the cable alone shows the longer name. With the dock answering too, Neap goes on talking through the dock and shows its name. `0x220`, "My Headset" from the factory, is no longer shown | Check the name on the USB Transmitter and the PC edition, and on Linux. A rename of the Bluetooth name (SP2Tools: NVKey `0xF202`) is not offered. |
| Pop-up battery warnings | Listed | Yes, on the PC | Missing | — | — | Done on the PC | Decide whether Neap should warn. |
| Battery in the notification area | Listed | Yes | Has | Confirmed | Confirmed | — | Shown in the icon's status. |
| Auto shut-off (off, 5, 10, 20, 30 min) | Listed | Yes | Has | Confirmed | Confirmed | `0x280` | — |
| Wake on motion | Listed | Yes | Has | Confirmed | Confirmed | `0x2E0` | Turtle Beach's article lists it in Swarm II's desktop app, but the README says the desktop app lacks it. Check, and correct the README. |
| Hotkeys: volume mute, Superhuman Hearing, noise gate, mic mute, noise cancellation | Listed | Yes, on the PC | Missing | — | — | — | Neap has hotkeys for the mix only. Profile hotkeys are on the backlog. |
| Voice prompt for each transmitter (none, 1–4) | Listed | Unknown: not yet found | Missing | — | — | Unknown | Turtle Beach's article lists it under Charging Hub. Find where it's kept. |
| Voice prompt volume | Listed; mobile app only since June 2026 | Yes | Has | Confirmed | Confirmed | `0x2D0` | Swarm II's desktop app has had it switched off since 1.0.0.47. |
| Dock light brightness, two lights | Listed | Yes | Has | Confirmed | Confirmed | `0x401`, `0x402` | — |
| Firmware updates | Listed | Yes, but Neap never writes firmware | Not planned | — | — | — | Neap never writes firmware. |

### Swarm II's own settings

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Updates | Listed | Neap's own | Has | Confirmed | Confirmed | — | One button on Windows; Linux opens the download page. |
| Start with the PC | Listed | Neap's own | Has | Confirmed | Confirmed | — | — |
| Language | Listed | Yes | Not planned | — | — | — | English only, written so it can be translated. |
| Macro manager | Listed | Yes, on the PC | Not planned | — | — | — | Out of scope. |

### Physical controls

| Control | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Volume wheel | Quick start guide | — | Has | Confirmed | Confirmed | `0x260` | — |
| Flip-to-mute microphone | Quick start guide | — | Has | Confirmed | Confirmed | `0x600` | — |
| Mute button, with the boom microphone off | Quick start guide | — | Has | Confirmed | Untried | `0x650` | The button is followed, and a write to `0x650` mutes and unmutes with the headset's tone. `0x640` says which microphone is in use and is reported the moment the boom is unplugged or plugged in. Neap follows the arm with the boom in and the button with it out. With the boom in the button does nothing, and the headset undoes a write to `0x650`, so Neap's switch mutes in Windows then and on the headset with the boom out. Confirm the switch with the boom on and off. |
| CrossPlay button (switch transmitters) | Quick start guide | — | Has | Confirmed | Confirmed | `0x400`–`0x460` | Moving between PCs needs the headset switched off and on; see Problems. |
| Bluetooth button | Quick start guide | — | Has | Confirmed | Confirmed | `0x290`, `0x320` | — |

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

- [ ] Firmware edit, in SP2Tools: report `0x760` on each level change of the
      noise cycle, and set a mark Neap can read that says which edits are
      installed.
- [ ] Neap, once SP2Tools has chosen the mark: read it, show each screen for
      the edits found, and keep the Stock or Edited choice on Controls only as a
      fallback.
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

- 2026-10-22 — Owner's headset, edited firmware, Neap recording of one full
  Mode button cycle: four presses gave three reports, all `0x750`. The step
  from transparency to noise cancellation, which changes only `0x760`, sent
  nothing, and Neap went on showing Transparency although it asks the `SAF`
  group about every 0.7 seconds. So the edit changes the level the headset
  uses but not the `0x760` it reports, pushed or asked. A fix in Neap cannot
  help; the edit has to update and announce `0x760` as the stock path does.

- 2026-10-22 — SP2Tools F090, proven on the owner's headset: edited firmware
  makes the Mode button step off, transparency, noise cancellation, with a
  tone for each, and keeps `0x750` and `0x760` in step as it goes (read from
  the code, not yet seen on the wire). Neap now asks which firmware a headset
  has, since the headset reports the same version either way.

- 2026-10-17 — Turtle Beach's PC article read: the feature list above. It
  adds a per-transmitter voice prompt (none, 1–4) and lists wake on motion in
  the desktop app.
- 2026-10-08 — Release 0.2.0: every function tried on the owner's headset on
  Windows and on a Steam Deck's desktop.
- Before 0.2.0 — Each key found by watching every value while the hardware
  was used, then confirmed by writing it back; see FINDINGS.md.
