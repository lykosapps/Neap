# How the Stealth Pro II works

A reference for the headset's protocol and behaviour, as far as it is known.
Everything here was measured on one Stealth Pro II (Xbox edition, white) and
one Windows 11 PC, unless marked:

- *(measured)*: observed directly, usually both ways.
- *(inferred)*: follows from measurements but was not observed on its own.
- *(research)*: from Turtle Beach's documentation, Swarm II's files, or other
  public sources.

The settings registry in `src/Neap.Core/Settings/Registry.cs` is the
authoritative list of keys. This document explains them.

## Contents

1. [Hardware and product ids](#hardware-and-product-ids)
2. [The control channel](#the-control-channel)
3. [Verbs and categories](#verbs-and-categories)
4. [Settings](#settings)
5. [Connection state](#connection-state)
6. [Transmitter slots](#transmitter-slots)
7. [Equaliser presets](#equaliser-presets)
8. [Buttons, dial and wheels](#buttons-dial-and-wheels)
9. [Windows audio](#windows-audio)
10. [The firmware layer](#the-firmware-layer)
11. [What is not possible](#what-is-not-possible)
12. [Pitfalls](#pitfalls)
13. [Capturing Swarm II](#capturing-swarm-ii)
14. [Open questions](#open-questions)

## Hardware and product ids

The headset ships with two transmitters: the **Charging Dock**, which also
charges a spare battery, and the **USB Transmitter**, a small USB-A dongle.
It can pair with up to four and uses one at a time. The chips are Airoha's,
named in the headers of Turtle Beach's firmware images *(research)*: the
headset has two, an AB1577 master and an AB1571 slave, each with its own
image and its own settings dictionary; the transmitters have an AB1571D,
built as Airoha's "ULL2 dongle". Both sides start from Airoha's reference
designs.

The PC sees the transmitter, not the headset. The headset is reached over the
transmitter's 2.4 GHz link, or directly when it is plugged in by its USB-C
cable.

USB vendor id `0x10F5`. Swarm II's product catalogue (`settings.json`, zlib
behind a four-byte header) names the whole family *(research)*:

| Product id | Swarm's name | What it is | Colour |
|---|---|---|---|
| `2283`, `2284` | Xbox base | Charging Dock | Black |
| `2285` | Xbox transmitter | USB Transmitter | Black |
| `229F` | Xbox transmitter (GIP) | USB Transmitter | Black |
| `2286` | Xbox headset | Headset, on its cable | Black |
| `229B`, `229C` | Xbox base | Charging Dock | White |
| `229D` | Xbox transmitter | USB Transmitter | White |
| `22A0` | Xbox transmitter (GIP) | USB Transmitter | White |
| `229E` | Xbox headset | Headset, on its cable | White |
| `2287` | PC base | Charging Dock | Black |
| `2288` | PC transmitter | USB Transmitter | Black |
| `2289` | PC headset | Headset, on its cable | Black |

Only `229B`, `229D` and `229E` have been plugged in here *(measured)*. The
Xbox and PC editions are different hardware and their transmitters are not
interchangeable *(research)*.

### The Atlas Air

Neap's first other headset. An owner's recording from Neap 0.2.0, sent on
GitHub issue 1, shows it on transmitter `225E`, reporting itself as `2260`
at `0x110`, firmware 6.163.1646.0, with all four transmitter slots empty. It
answered for the same settings under the Stealth Pro II's keys, with nothing
at all from `0x500` to `0x5FF` or `0x700` to `0x7FF`, even when asked again.
`0x260` followed Windows' volume as it was turned. Its speakers and
microphone are named "Atlas Air" *(measured, on the owner's PC)*.

What it has, from Turtle Beach's support articles for it *(research)*:

- **Swarm II, desktop:** master volume, Superhuman Hearing (on/off; Legacy,
  Footsteps or Gunshots), a game and chat mix, Chat Boost, Waves 3D, high
  bandwidth audio, microphone mute, sensitivity, monitoring, a noise gate
  with its level, high bandwidth microphone, game and microphone equalisers
  with four presets each and custom ones, auto shut-off (off, 5, 10, 20 or
  30 minutes), the transmitter's light brightness, and voice and tone prompt
  level.
- **Swarm II, mobile:** the same, and a lower volume wheel and Mode button
  that can be given a job. This conflicts with the quick start guide.
- **Quick start guide,** the word on its controls: a master volume wheel, a
  power button, a QuickSwitch button (wireless or Bluetooth), a Bluetooth
  button (held, pairing; pressed, calls and playback) and a flip-to-mute
  microphone. No Mode button, second wheel, chat wheel, noise cancellation
  or dock.

A second recording, an hour of Neap 0.2.1 on the same issue, shows three
keys meaning something else on it *(measured, on the owner's PC)*:

- `0x260` followed the Windows output level throughout, 58 at the start and
  76 at the end as the endpoint was, while `0x2A0` stayed at 100. `0x260` is
  its volume mirror.
- `0x290` read 1 on its transmitter and 0 while QuickSwitch had it on
  Bluetooth, flipping three times in thirty seconds as the owner tried it.
  Bit 0 is not "Bluetooth connected" here. Once Bluetooth was connected,
  `0x310` sent a signed level every ten seconds, -40 to -79, and went on
  sending it after the headset switched back to its transmitter.
- `0x620` turned on and off with the mute at `0x600`, three times, in the
  same notification. Whatever it is, it is not mic monitoring alone, and the
  owner heard no monitoring.

`0x720` answered from the start this time, and `0x700`, `0x710`, `0x730`
and `0x740` were there by the end, after the owner had used their controls.
`0x630` read 20 rather than 1 and went to 0 when switched off. `3DT`, the
game and chat mix, still never answered, asked twice.

Swarm II's catalogue gives the Atlas Air its own device plugin, `ATLAS_AIR`,
for the transmitter (`225E`, type 129) and the headset (`2260`, type 130,
listed under the transmitter), on the same "tb" v2 controller library as the
Stealth Pro II *(research)*. The Stealth Pro II's plugin declares only types
163 to 176, so it does not serve the Atlas Air. Swarm II fetches a device's
plugin only when that device is first plugged in, so a PC that has only seen
the Stealth Pro II has no Atlas Air plugin to read *(research)*.

The shared library under that plugin, `Turtlebeach.Mosit`, has Airoha-level
commands for sidetone level and state and for voice prompts, separate from
`set_kvp`. The bridge Swarm II's plugins call it through does not expose them
in the installed copy, so whether the Atlas Air uses them in place of `0x620`
and `0x2D0` is not known until its plugin is read *(research)*.

Swarm II's settings file for the Atlas Air, from the same owner (`ATLAS_AIR.ini`,
Neap 0.2.1.2 test, 15 Oct), groups the headset's values under names of its own
*(research)*:

- `SHH`, `0x800` to `0x830`: `0`, `0`, `60`, `0`. Read in the order of the
  Stealth Pro II's `0x720` to `0x740`, the first is on or off, the second the
  type and the third the intensity. The fourth is unknown.
- `Prm`, `0xC00` and `0xC10`: `15` and `0`. `0xC00` is read as the voice prompt
  volume. `0xC10` is unknown.
- `Swm`, `0x500` to `0x540`: `1`, `50`, `0`, `0`, `0`. The place of the Stealth
  Pro II's `3DT` group, which the Atlas Air never answers for.
- `NG`, `0x700` and `0x710`: the noise gate, the same keys as the Stealth Pro II.
- `TX`, `0x400` to `0x490`: `1`, `2`, `0`, `50`, then zeros, with `100` at
  `0x480`. One of these is probably the transmitter's light; which is not known.
- No `0x720`, `0x730`, `0x740` or `0x2D0` at all, which are the keys Neap sent
  for Superhuman Hearing and voice prompts. The tester heard no change from
  Neap's controls for either, and a clear one from Swarm II's.

The order and values are one snapshot, not a capture of a control being
moved, so the readings above are to be confirmed by ear, as `0x730` and
`0x740` were.

The Linux recordings from the other tester (CachyOS) show the transmitter's
control collection opening once the udev rule is in place and giving no answer
to the first question, with nothing at all coming back. Only one collection
is asked, so a wrong choice between the two plugged in is ruled out *(measured,
on the tester's PC)*.

Swarm II's plugin for the Atlas Air (`ATLAS_AIR.dll` 1.0.0.24, from the same
owner's PC), read without running it, settles more *(research)*:

- **AI noise reduction:** the Microphone page's switch calls a function that
  returns at once, and so does the Monitoring switch. Neither writes
  anything. Turtle Beach's article for the headset has no AI noise
  reduction. `0x630` is the mic monitoring level, 0 to 100, which is why it
  read 20 and went to 0 when Neap's AI noise reduction switch turned it
  "off".
- **Voice prompts:** `0xC00` in `Prm`, on a slider from 0 to 15. The tester
  heard no change above about 20.
- **Transmitter light:** `0x430` in a group `TX`, 0 to 100, not the Stealth Pro
  II's `0x401`. On the Stealth Pro II `0x430` is inside the second
  transmitter slot's block, so only the Atlas Air's own key is written there.
- **Name:** always the words "Atlas Air"; `0x220` is never read.
- **Dial and buttons:** `0xA00` to `0xB20` are only in its list of defaults,
  never written.
- **Superhuman Hearing:** the reading had the switch going to Swarm II's
  Waves audio software on the PC rather than `0x800`. The tester heard
  Superhuman Hearing work from Neap's writes to `0x800` to `0x820` *(measured,
  by ear, on the tester's headset)*, so the headset does it and that reading
  is set aside.
- `0x830` and `0xC10` are never used.

The Linux tester's USB listing shows the transmitter's control interface
(interface 3) with an interrupt input and no interrupt output, so Linux sends
Neap's requests as SET_REPORT on the control pipe, as Windows does
*(measured, on the tester's PC)*. Linux also queues what the interrupt input
carries for a read, which Neap does not otherwise read.

Neap's Windows log from the same tester shows the transmitter's first reply
after being plugged in is an unasked `05 5c 03 00 80 2c 01` *(measured)*, one
of the start-up messages SP2Tools records from the Stealth Pro II's Charging
Dock. SP2Tools proved on that dock that it answers only if something has been
reading report 7 since it was plugged in, and gives empty reports otherwise,
which is what the Linux tester's transmitter gives. So Neap on Linux restarts
a silent transmitter once per plug-in (USBDEVFS_RESET) while reading it
throughout *(a test, not yet seen to work on an Atlas Air)*.

Lent to Linux (WSL, kernel 6.18, usbipd) and read by Neap a minute after it
was plugged in, the owner's Stealth Pro II Charging Dock (`229B`) answered in
0.7 seconds. The restart reset it with the hidraw node kept, 16 reports were
read as it came back, and it answered again in 0.4 seconds *(measured)*. On
Windows the same dock, replugged and left unread for two and a half hours,
answered Neap in three seconds *(measured)*. Neither reproduces the Atlas Air's
silence.

With reads paced at Windows' rate, the dock on Linux answered in 0.6 seconds.
After Neap's restart, the first report read held the start-up messages
`05 5d 03 00 10 31 ..`, `05 5c 03 00 80 2c 01`, `05 5c 03 00 80 2c 03` and
`05 5d 0e 00 b1 2c ..`, the ones SP2Tools recorded after a real unplug
*(measured)*: a USB reset puts the transmitter through its full start-up.

SP2Tools then read the Atlas Air's decoded firmware for Neap's questions
(F114 to F122, 22 October), with no Atlas Air on its bench, so all of what
follows is *(research, read from the firmware)* until a tester confirms it.
Its questions and answers are in SP2Tools' `knowledge/iceman-neap-asks.md`.

- **Linux silence:** the transmitter answers a read only while its USB state
  is "configured". A bus event that is not followed by the host setting it up
  again can leave every reply empty until a reset; a USB suspend is the most
  likely such event, not yet proven (F115). Nothing in the transmitter powers
  the headset off, so the switch-off after a reset is probably the headset's
  own reaction (F118).
- **Link question:** RACE `0x9931` is answered by the transmitter itself, 1
  while the headset is linked and 0 while not, so an answer proves the
  channel works whatever the headset is doing (F115).
- **Transmitter light:** the transmitter has no settings of its own to read,
  so a read of the light gets nothing; RACE `0x0006` operation 3 gives the
  level it shows, 0 to 100 (F116).
- **Which device answered:** the transmitter keeps no copy of the headset's
  settings, so it never answers for a headset it cannot reach (F117), unlike
  the Stealth Pro II's.
- **Mic monitoring:** `0x630` is its level, from -29.7 dB at 1 to 0 dB at 100,
  with 0 off and 20 as shipped; `0x620` is its own on/off. The headset mutes
  monitoring with the microphone, which is why `0x620` followed `0x600` above
  (F120).
- **Mute and the boom:** a write to `0x600` is refused while a switch on the
  headset reads low, and that switch sets the mute at power-on. It is
  probably the boom's; no key reports the boom, and which position reads low
  is not known (F120).
- **Noise gate:** `0x700` (on or off) and `0x710` (its level, 0 to 100),
  which the headset serves as a group `NG` of their own, read like `Mic` and
  answered at start with the saved values. The firmware serves 25 groups:
  `Dbg Inf GSI BT TX Swm Mic NG SHH Enc Btn Prm AQG AQM CEC CG1`–`CG5`
  `CM1`–`CM5`, with no `SAF` or `3DT`, so a read of either goes unanswered
  (SP2Tools F125). That is why the Atlas Air reports the noise gate when it
  changes but not in the read Neap made at start, which asked the Stealth
  Pro II's `SAF` *(measured, in testers' recordings)*. Writes work whatever
  the group, as they name only the key. The Bluetooth equaliser (`0x1400`
  to `0x14C0`, `0x1640`, `0x1650`, `0x1900` to `0x1980`) is in no group, so
  it can be written but not read back.
- **Sensitivity:** `0x610` is a gain in the headset, which the transmitter
  sets from the PC's microphone level (F120).
- **Voice prompts:** `0xC00` has 16 steps, -58.5 dB to -15.5 dB; above 15 is
  15, and 50 as shipped is the top step, which is why the tester heard no
  change above about 20 (F120). `0xC10` is the tones' level, held only until a
  restart.
- **Volume:** `0x260` is the wheel, in steps of 6; `0x2A0` is the PC's level
  coming back from it, so the two move apart for a moment on a fast turn
  (F120).
- **Loudness boost:** the same RACE `0x000A` command, parameter and stock
  level (-12 dB) as the Stealth Pro II (F114).
- **Game and chat mix:** the `0x500` to `0x540` group is Swarm II's own flags,
  and the headset mixes game and chat only for an Xbox (F121). Chat Boost,
  `0x270`, is a flag with no sound stage behind it (F121).
- **Superhuman Hearing:** `0x800` to `0x820` drive the headset's own sound
  processing; `0x830` is the same switch while on Bluetooth. A change to an
  equaliser turns it off without a word, and it is off after every power
  cycle (F122).
- **Auto shut-off:** `0x280`, 0 never, 1 to 4 for 5, 10, 20 and 30 minutes, 4
  as shipped (F122).
- **Equalisers:** encoded as the Stealth Pro II's, ten bands from 32 Hz to
  16 kHz in tenths of a dB, up to 9 dB either way; the bands are not kept
  through a power cycle (F122).
- **Dial and buttons:** the headset reports `0xA00` to `0xB20` *(measured,
  in a tester's recording)*, one of each for a PlayStation, an Xbox and a PC.
  The dial jobs (`0xA00` to `0xA30`) belong to a second wheel the Atlas Air
  does not have: its one wheel is the volume, which cannot be given another
  job, so a write is kept but does nothing, and `0xA30` is refused. The
  button jobs (`0xB00` to `0xB20`, PC's is `0xB20`) set what one short press
  of a button does: 0 the next game equaliser preset, 1 the noise gate on or
  off, 2 chat boost on or off. Which button that is, and whether it is a
  button at all rather than a power input, is not known without a test on
  the headset (SP2Tools F126, read from the firmware).

### Which headsets share the platform

Swarm II's catalogue (`settings.json`, a zlib-compressed JSON list of 140
Turtle Beach products) tags 61 headset and transmitter entries with the same
chip family as the Stealth Pro II *(research, Swarm II as installed for the
Stealth Pro II)*: the Stealth 500 (PC, PlayStation, Xbox, Switch), 600 and 700
(PC, PlayStation, Xbox), the Atlas Air and the Stealth Pro II itself. The older
Stealth Pro and the Atlas 200 use other chips and are not in this family. The
catalogue gives each entry's product ids; it says nothing about what each
headset can do, so the support articles above and an owner's recording are
still needed.

### The Stealth 600 Gen 3

What it has, from Turtle Beach's "Swarm II App (Desktop Version)" article for
the Xbox version, updated 24 June 2026 *(research)*: master volume (on/off and
a level), Superhuman Hearing (on/off; Legacy, Footsteps or Gunshots), a game
and chat mix, Chat Boost, microphone mute, sensitivity, monitoring (on/off and
a level), a noise gate (on/off and a level), game and microphone ten-band
equalisers with four presets each (game: Signature Sound, Bass Boost, Bass and
Treble Boost, Vocal Boost; microphone: Signature Sound, Full, Clarity, Smooth)
and custom ones, auto shut-off (none, 5, 10, 20 or 30 minutes; 30 by
default), the transmitter's light brightness, and voice and tone prompt level.
The lower volume dial can be given microphone monitoring level, bass boost
level, treble boost level or noise gate level, and in Xbox mode the game and
chat mix, which is its default there; the Mode button can cycle game presets
or switch the noise gate, with a different default in each transmitter mode.

What it does not list: noise cancellation or transparency, a charging dock or
spare battery, wake on motion, spatial sound. The PC and PlayStation versions
have their own articles, not yet read. It is the Atlas Air's list plus the
dial and the Mode button.

### The Stealth 700 Gen 3

From the Xbox version's article, updated 22 June 2026 *(research)*: the
600 Gen 3's list, with the same equaliser presets, dial and Mode button jobs,
and Superhuman Hearing also getting a level from 0 to 100. The hotkey
functions it lists: volume mute, Superhuman Hearing, noise gate, microphone
mute and microphone monitoring. Its quick controls differ: an upper volume
dial that moves Swarm II's master volume, a lower dial, a Mode button, and on
the right earcup a separate Bluetooth volume dial, a CrossPlay button and a
Bluetooth multifunction button. The master volume does not move Bluetooth
audio. The article says the firmware must first be updated from the mobile
Swarm II before the desktop app can change settings. No noise cancellation,
dock or spatial sound.

### The Stealth 500

From the Xbox version's article, updated 23 June 2026 *(research)*: the
leanest of the family. It has master volume, Superhuman Hearing (on/off and
type, no level listed), microphone mute, sensitivity, monitoring and noise
gate, both ten-band equalisers with four presets each (±9 dB per band) and
custom ones, auto shut-off, hotkeys, the transmitter's light brightness and
prompt level. It does not list a game and chat mix, Chat Boost or an
Assignment page, so no mappable dial or Mode button. Its presets are split
into Onboard, saved to the USB Audio Adapter and always available, and
Software, kept in Swarm II until moved onto the headset. There are separate
articles for PC, PlayStation and a Nintendo Switch 2 version, not yet read.

## The control channel

Each device presents two HID collections. One is standard consumer control
(media and volume keys). The other is the control channel:

| | |
|---|---|
| Usage page | `0xFF13` (vendor-defined), interface 3 |
| Outbound | output report id 6, 62 bytes |
| Inbound | **input** report id 7, 62 bytes, read with `HidD_GetInputReport` |
| Access | read and write from a normal, non-elevated process |

The collection declares no feature reports, so `HidD_GetFeature` fails
*(measured)*.

### Frames

The transport is Airoha's RACE protocol *(research: documented publicly for
a sibling Turtle Beach headset)*, with a Turtle Beach text protocol inside
it. A captured command setting noise cancellation intensity to 100:

```
06                      HID output report id
2A 00                   total length, little-endian (42)
05                      RACE start byte
5A                      RACE type: 5A command, 5B response, 5D notification
26 00                   inner length, little-endian (38)
02 99                   RACE command id 0x9902 (Turtle Beach vendor command)
48                      constant
03                      01 for a bare command, 03 when an argument follows
FB DE 44                message counter
00 x7                   padding
B7                      tag before the verb
73 65 74 5F 6B 76 70    "set_kvp"
FF                      separator
7B 22 30 78 37 36 30    {"0x760":"100"}
22 3A 22 31 30 30 22 7D
00 ...                  zero-padded to 62 bytes
```

Total length is the argument's length plus 27; inner length is plus 23. The
tag is `B7` before `set_kvp` and `61 00` before a read verb; what it
encodes is not known.

### Replies

Replies and notifications arrive on report 7 with the same framing, RACE type
`5B` or `5D`. The inbound length is 16-bit little-endian, like the outbound
one. Long replies continue in further reports that carry raw continuation
bytes with no RACE header, split anywhere, including mid-string, and
unrelated notifications interleave with them. Accumulate payloads and consume
only what parses.

A reply to a read names its category; a pushed update says `UP`:

```
{"OR":"GSI","KVP":{"200":"1","210":"0","220":"My Headset", ...}}
{"UP":"SAF","KVP":{"750":"1"}}
```

KVP values are strings, except in the preset and transmitter slots, whose
values are JSON objects.

## Verbs and categories

`set_kvp` writes one or more keys. Everything else is a read verb, one per
category, and returns every key in that category's range *(measured)*.

| Category | Read verb | Range | Contents |
|---|---|---|---|
| `Inf` | `SInf` | `0x1xx` | identity and firmware |
| `GSI` | `SGSI` | `0x2xx` | general state: battery, link, volume, power |
| `BT` | `RBT` | `0x3xx` | Bluetooth and link signal |
| `TX1`–`TX4` | `STX1`–`STX4` | `0x400`–`0x460` | transmitter slots |
| `3DT` | `S3DT` | `0x5xx` | game and chat mix (the chat wheel) |
| `Mic` | `SMic` | `0x6xx` | microphone |
| `SAF` | `SSAF` | `0x7xx` | noise cancellation, noise gate, Superhuman Hearing |
| `Enc` | `SEnc` | `0xAxx` | dial assignment |
| `Btn` | `SBtn` | `0xBxx` | Mode button assignment |
| `AQG` | `SAQG` | `0x12xx` | game equaliser |
| `AQM` | `SAQM` | `0x13xx` | microphone equaliser |
| `CEC` | `SCEC` | `0x16xx` | preset counts and delete registers |
| `CG1`–`CG5` | `SCG1`–`SCG5` | `0x1700`–`0x1780` | custom game presets |
| `CM1`–`CM5` | `SCM1`–`SCM5` | `0x1800`–`0x1880` | custom microphone presets |
| `Ver` | `SVer` | | protocol version |

A read verb is `S` followed by the category, with one exception: Bluetooth is
`RBT`. `SBT` and other guesses get no reply at all. Category names are not all
letters (`3DT`), and are two to three characters long.

Swarm II itself only ever sends `set_kvp` and `SGSI`, plus the ten preset slot
reads. The others were found by probing and confirmed by their replies.

The headset drops requests sent back to back *(measured)*: send one, wait for
its reply, then send the next. Each read takes about 100 ms, so reading all
twelve settings categories takes about 1.2 s.

## Settings

The registry holds 89 keys: 72 identified and 17 read back but not yet
matched to anything. Key numbers are hex; values travel as decimal strings.

### Identity (`Inf`)

| Key | Meaning |
|---|---|
| `0x100` | USB vendor id |
| `0x110` | USB product id: `229E`, the headset's own, whichever transmitter answers |
| `0x120` | serial number |
| `0x130` | firmware version |
| `0x150` | changes when the headset moves between transmitters; its value is not understood. See [Connection state](#connection-state) |

### General state (`GSI`)

| Key | Meaning |
|---|---|
| `0x220` | a name the headset keeps, "My Headset" from the factory; no recording shows Swarm II changing it. Neap shows the USB product string instead |
| `0x230` | the wireless sound link: 2 while a transmitter sends the headset sound, 0 while none does. See [Connection state](#connection-state) |
| `0x240` | battery, percent |
| `0x250` | charging: 1 while the battery is filling *(measured; see below)* |
| `0x280` | auto shut-off: 0 off, 1 = 5 min, 2 = 10 min, 3 = 20 min, 4 = 30 min |
| `0x290` | link state: bit 0 = Bluetooth connected; bits 1–2 = attachment, 1 = 2.4 GHz, 2 = USB *(measured)* |
| `0x2A0` | a **mirror** of the Windows output volume, not a control |
| `0x2D0` | voice prompt volume, 0–100 |
| `0x2E0` | wake on motion, 0/1 |

`0x2D0` and `0x2E0` are not in Swarm II's desktop app; they were found by
driving the Swarm II mobile app and watching which value moved.

`0x290` was settled by watching it through five changes: 3 (2.4 GHz,
Bluetooth on), 2 (Bluetooth off), 4 (cable in), 5 (Bluetooth back), 3 (cable
out). Only the bit layout above fits all five.

`0x250` went to 1 as the cable went in, the battery climbed only while it read
1, and at 100% with the cable still in it went back to 0 *(measured)*. It is
read as charging rather than "cable attached", because `0x290` already
reports the attachment. What would fully settle it: at 100% with the cable
in, `0x250` at 0 while `0x290` still says USB, read at the same moment.

### Bluetooth (`BT`)

`0x320` is signal strength in dBm, signed in pushed updates and unsigned in
reads. It keeps reporting with the phone's Bluetooth off, so it most likely
measures the 2.4 GHz link *(inferred)*. `0x300` accepts a write and keeps it
across a power cycle, but is not a Bluetooth switch: Bluetooth connects
either way.

### Mix, microphone and noise

| Key | Meaning |
|---|---|
| `0x510` | game and chat mix, 0–100. The chat wheel reports its position here. Writing it is ignored: set to 80 and then 50, it read back unchanged each time, and the wheel carried on from where it was *(measured)* |
| `0x600` | microphone muted by the boom arm, 0/1: 1 while it is flipped up. Writing 1 with the arm down is accepted and does not mute; the microphone went on picking up speech. A Windows mute of the microphone does not change it *(both measured)* |
| `0x610` | a **mirror** of the Windows recording level, not a control |
| `0x640` | which microphone is in use: 1 for the boom microphone, 2 for the built-in ones. Reported at once when the boom is unplugged or plugged in while connected. With the boom in, the mute button does nothing and reports nothing, and the built-in microphones' mute at `0x650` keeps whatever it was, so only the arm counts; with it out, the arm reads 0 and only `0x650` counts *(measured)* |
| `0x650` | the built-in microphones' mute, 0/1, used while the boom microphone is off: the headset's mute button sets it to 1 and back, and `0x600` reads 0 throughout. Writing it mutes and unmutes, with the headset's tone, as the button does, but only while the built-in microphones are in use: with the boom on, a write of 1 was reported and then undone about two seconds later. The firmware keeps this mute across restarts, unlike the arm's *(measured; keeping it from the firmware, research)* |
| `0x620` | microphone monitoring (sidetone), 0–100 |
| `0x630` | AI noise reduction, 0/1 |
| `0x700` | noise gate, 0/1 |
| `0x710` | noise gate threshold, 0–100 |
| `0x720` | Superhuman Hearing, 0/1 |
| `0x730` | Superhuman Hearing type: 0 Legacy, 1 Footsteps, 2 Gunshots *(named from Swarm II; confirmed by ear, not by capture)* |
| `0x740` | Superhuman Hearing intensity, 0–100 *(as `0x730`)* |
| `0x750` | active noise cancellation, 0/1 |
| `0x760` | noise cancellation intensity, 0–100. At 0, with `0x750` on, outside sound comes through, unlike `0x750` off *(confirmed by ear by the owner)* |

The headset receives one stereo stream from a PC, so it cannot mix game
against chat there: `0x510` is an input to the PC's mix, not a mix itself. It
does act where two streams really arrive: on Xbox, and with Bluetooth audio
alongside 2.4 GHz *(inferred)*.

### Settings that belong to Windows

`0x2A0` and `0x610` look like headset settings and are not *(measured both
ways, for both)*. Move the Windows level and the headset reports the new
number within a second. Write the headset key and only the reported number
moves: nothing sounds different, and Windows puts its own value back. Change
these through the Windows endpoint.

Swarm II's "Audio Master Volume" switch is the Windows endpoint's mute
*(research: Turtle Beach's support pages; confirmed by two captures in which
it sends nothing to the headset)*. The volume wheel sends volume-up keys, and
Windows clears mute on any volume-up, which is why the switch turns itself
back on.

### Assignments (`Enc`, `Btn`)

Mode button, `0xB20`, zero-based:

| Value | Function |
|---|---|
| 0 | noise cancellation on/off *(default)* |
| 1 | cycle game presets |
| 2 | noise gate on/off |

Lower dial, `0xA20`, **one-based**: restoring the factory default writes 1,
and choosing Treble then Bass wrote 4 then 3 *(measured)*. 0 has never been
seen.

| Value | Function |
|---|---|
| 1 | microphone monitoring *(default)* |
| 2 | game and chat mix |
| 3 | bass boost |
| 4 | treble boost |
| 5 | noise gate |

### Equaliser (`AQG`, `AQM`)

The game bank is `0x1210` (selected preset id), `0x1220`–`0x12B0` (ten bands,
step `0x10`) and `0x12C0` (preset name). The microphone bank is the same one
block along, at `0x1310`, `0x1320`–`0x13B0` and `0x13C0`. Bands are tenths of a
decibel, −90 to +90, but the headset takes them only in half-decibel steps
*(measured)*: writing 27 left the band at 20, and 23 became 20, while 25, 20,
5 and 0 were stored as written. A write between steps still clears the
selected preset, so nothing says it missed. Neap sends only whole steps.

Band centres, read from Swarm's device plugin (`STEALTH_PRO_II.dll`)
*(research)*:

| Bank | Bands |
|---|---|
| Game | 32, 63, 125, 250, 500 Hz, 1, 2, 4, 8, 16 kHz |
| Microphone | 100, 160, 250, 400, 630 Hz, 1, 1.6, 2.5, 4, 6.3 kHz |

Eight microphone labels appear literally in the plugin. The 250 Hz and 1 kHz
placements were inferred, since Qt pools identical strings, and are
confirmed by the filter tables below.

The filters themselves are in the headset's NV key file that Swarm II ships
for firmware 4.107.703 (`CORONA_RX_MASTER_NVkey_v4_107_703.xml`) *(research)*.
Each table is a count, then per filter four little-endian floats (frequency,
Q, gain, sample rate) and a 16-bit type:

| Key | What | Filters |
|---|---|---|
| `0x500B` game GEQ | ten of type 4 | 31.5 Hz to 16 kHz, Q 1.414, 48 kHz |
| `0x5059` mic GEQ | ten of type 4 | 100 Hz to 6.3 kHz, Q 2.167, 16 kHz |
| `0x500A` headphone compensation | fixed | 120 Hz type 6 +1.5 dB Q 0.5; 300 Hz +3 Q 0.7; 4.2 kHz +2 Q 1.7; 5.5 kHz type 7 −2.2 Q 0.7 |

Type 4 is taken to be a peaking filter, and 6 and 7 low and high shelves;
the codes are not documented. The game path's input level control
(`0x5008`) sits at −12 dB, which reads as headroom for the equaliser's
boosts. The equaliser's own level (`0x5003`) is 0 dB.

Measured on the headset *(measured, 28 Sep 2026)*: tones played through the
game bank, recorded by a separate microphone against the sealed ear cup,
and compared with the flat preset, so the microphone's and the cup's own
response cancel. The recording held from about 60 Hz to 6 kHz; the
microphone's chain cut both ends, so nothing outside that was measured.
The ear cup was held against a desk microphone, not sealed on a coupler:
two flat runs, before and after, agreed to 0.6 dB (median) and 2.2 dB at
worst from 100 Hz to 5 kHz, so differences under about 2 dB are within the
setup's own error. Inside it:

| Setting | Measured against the model |
|---|---|
| One band, +9 or −9 dB | Centre within 1 dB; the skirts an octave out about 1.5 dB lower than a Q 1.414 peaking filter, inside the setup's error |
| Neap's fitted curves | Within about 1 dB (rms) of the curve drawn as heard |
| All ten bands at +9 dB | About +8 to +11 dB, 2.6 dB below the model's +11 to +13.5, at every frequency measured |

So the filters match the model within what this setup can tell, except
that neighbouring bands add up to less than the model says.

Writing any band sets the selected preset (`0x1210`) to 0 and empties its name
*(measured)*: the curve is no longer that preset. Which preset an edit
started from has to be remembered by the app.

## Connection state

Nothing on the wire says "connected". The state is assembled from which
device answers, what the transmitter slots say, `0x230`, and whether the
headset's own sound device is in Windows.

### Settings stay with the transmitter the headset was switched on with

The headset keeps its settings and chat wheel on the transmitter it was
switched on with. CrossPlay moves only its sound *(measured: five
arrangements, no exceptions)*.

| How it got there | Sound on | Answers for settings |
|---|---|---|
| on with the Charging Dock, CrossPlay to the USB Transmitter | USB Transmitter | Charging Dock |
| on with the USB Transmitter | USB Transmitter | USB Transmitter |
| then CrossPlay to the Charging Dock | Charging Dock | USB Transmitter |
| on with the Dock, CrossPlay to the USB Transmitter, Dock unplugged | USB Transmitter | nothing |
| Dock unplugged, switched off and on with the USB Transmitter | USB Transmitter | USB Transmitter |

So which transmitter carries the sound and which answers for the settings are
two facts, and can differ. In the fourth row the headset plays sound while
nothing can read or change its settings, and switching it off and on, not
plugging anything in, is what brings them back. Settings written through one
transmitter do reach a headset whose sound comes through the other: noise
cancellation changed audibly, and the chat wheel moved the mix across its full
range *(measured)*.

The probe's `who` command asks each transmitter separately, which is the only
way to see which one is answering.

Which transmitter the headset joins when switched on is its own choice, and
not the one it last used: in one evening it came back on the Charging Dock
twice and on the USB Transmitter five times, with both plugged in
*(measured)*. To make it start on one, unplug the other first.

### A transmitter answers from memory

A transmitter goes on answering for a headset it can no longer reach, with
the values it last had *(measured)*:

- Switched off while on the Charging Dock, the Dock went quiet and, three
  seconds later, the USB Transmitter answered, with the sound link at 0.
- Just after a switch-on, the Charging Dock answered for sixteen seconds with
  the sound link at 2, then went quiet, and the USB Transmitter, where the
  headset really was, answered.

So no single answer proves the headset is there. After the headset goes
quiet, the app takes an answer from a different transmitter only once its
sound link is up, which covers the first case and not the second.

### No sound, and nothing says so

Once more, both transmitter lights went amber with the headset on and no
sound anywhere. It followed unplugging and replugging the Charging Dock,
which carried the settings, while the USB Transmitter carried the sound. The
Dock kept answering live (its signal strength changed every ten seconds), the
slots said the USB Transmitter was selected, and every other value matched a
healthy connection, `0x150` and `0x230` included. CrossPlay moved the sound
to the Dock and brought it back; only the slots' selection changed
*(measured)*.

Unplugging the one transmitter that carried both the sound and the settings
leaves the other amber: no sound, no microphone, and nothing answers for the
settings. The headset's volume wheel still reaches Windows through that other
transmitter, so some link remains. Switching the headset off and on brings
everything back on it; CrossPlay brings back only the sound *(measured)*.

### The sound link, `0x230`

| What happened | `0x230` | Sound |
|---|---|---|
| headset switched on | 0 for about 15 s, then 2 | arrived as it went to 2 |
| the transmitter carrying sound unplugged, the other carrying settings | 2 → 0 | none |
| CrossPlay from that state | 0 → 2 | back |
| on the cable with the Dock connected | 2 | through the cable |
| Dock unplugged, cable still in | 2 → 0 | carried on through the cable |
| headset switched off | did not drop ahead of it | |

So it is the **wireless** sound link, and says nothing about the cable. It
does not catch every silent state: once, with both transmitter lights amber,
it read 2 while no sound played anywhere, and CrossPlay brought the sound
back. While it reads 0 the chat wheel's changes do not arrive, though
settings still answer; the turns made meanwhile arrive together when sound
returns.

### A change of transmitter, `0x150`

With both transmitters plugged in, asked of the Charging Dock, it went 2 to 1
as the headset CrossPlayed away and 1 to 2 as it came back, in both
directions, with sound playing through the other transmitter throughout
*(measured at ten samples a second)*. It changed about three seconds before
the transmitter slots did, so it is a cheap trigger for re-reading them.

Its value is not "on this transmitter": with the Dock the only transmitter
plugged in, paired and in use, it read 1 *(measured)*. That fits it being the
number of the slot in use, but that is one data point *(inferred)*. Use only
the fact that it changed; which transmitter is selected comes from the slots.
It does not mean sound is flowing.

### Over the USB-C cable

Plugged in by its cable, the headset is a USB audio device of its own
("Stealth Pro II Xbox Headset", `229E`) and answers for its settings there
too. With the Charging Dock also connected *(measured)*:

| What was done | What happened |
|---|---|
| cable plugged in, music on the Dock | Windows moved sound and microphone, every role, to the cable; the music carried on |
| cable unplugged | Windows moved everything back to the Dock |
| cable in, output set back to the Dock | music played through the Dock; Windows left the communications output and the microphone on the cable |
| a beep sent to the cable every 3 s, music on the Dock | each beep cut the music out: the headset plays one source at a time |
| spoken into, both microphones recorded | the cable's microphone carried the voice; the Dock's recorded silence |

So with the cable in, the cable is the only right place for sound, calls and
the microphone. Not yet tried: whether the Dock's sound also cuts out the
cable's, and all of this with the USB Transmitter.

Once, the Dock was unplugged while the headset was on its cable with music
playing. The wireless sound link went to 0, Neap carried on over the cable,
and Windows kept sound and microphone on the cable with the music reaching
it, but the headset played nothing *(measured, once)*. CrossPlay did not
bring it back; switching the headset off and on did. Whether it happens
every time is not yet known.

On its cable alone, the chat wheel reaches nothing *(measured)*. Asked
about seven times a second while it was turned both ways, `0x510` stayed at
50 and nothing else moved but the battery, so it is neither sent nor
readable. `0x500` read 0 there, where the Dock shows 1; set to 1 (SP2Tools
reads it in the Atlas Air's firmware as the flag that says an app is
listening), it held, and the wheel still changed nothing. SP2Tools finds the
wheel's position reaching the main chip as a message from the other earcup's
chip, which would explain it if that message travels over the wireless
link *(inferred)*.

Switched off on its cable, the headset's speakers, microphone and consumer
control leave Windows, one vendor HID collection stays, and it keeps
answering, battery included. Switching on or off restarts the USB connection
for about three seconds. The sound device being present is what says the
headset is on.

### The USB Transmitter on its own

Once, with only the USB Transmitter plugged in, it played sound and answered
nothing on the control channel: 428 reports read with no payload, both write
paths accepted, no reply in 8 s, and no chat wheel reports on either
collection while the volume wheel's still arrived *(measured)*. How the
headset had got onto it was not recorded; the fourth row of the table above
accounts for it *(inferred)*. It is not a limit of the USB Transmitter:
switched on with it, the headset answers for everything.

## Transmitter slots

The headset keeps a slot for each of up to four paired transmitters, at
`0x400`, `0x420`, `0x440` and `0x460`, each read with its own verb:

```
{"OR":"TX1","KVP":{"400":{"info":["2","17","1","100","1","10F5","229B",
                                  "4.107.703.0","AA:BB:CC:DD:EE:FF"],
                          "control":["0","100","100"]}}}
```

In `info`: the first field is 0 empty, 1 paired, 2 selected; then vendor and
product id, firmware and Bluetooth address. Empty slots answer with zeros.
"Selected" means the transmitter the headset is assigned to, not one that
is carrying it: through the both-amber episode the USB Transmitter's slot
said 2 with no link at all.

The fourth field (`info[3]`) is the **spare battery** in the Charging Dock's
slot: its charge, 0–100, and 255 while the slot is empty *(measured)*. Taken
out, put back and taken out again, it went 100, 255, 100, 255 in step. In
Swarm II's log of a hot swap, it went to 255 as the full spare came out,
then 37 as the headset's drained battery went in, and climbed to 100 over
two hours while the headset reported 100. Nothing separate says it is
charging; the dock charges whatever is in the slot. Swarm II's desktop app
does not show it; its phone app shows it as the Charging Hub's battery
*(research)*. The USB Transmitter, with no slot, reports 0.

The spare's reading is not steady while it charges *(measured, 8 Oct)*. A
battery that read 10% in the headset went into the dock; the dock first kept
the previous spare's 100, then read 55, 57, 65 and 69 over about an hour and a
half while charging, and 45 just before it was taken out, which is what the
headset then read for it (49, falling to 46). The reading is therefore
inflated and uneven while the dock is charging, and settles only at rest or
when full. The dock-side figure behaves like a voltage estimate, not a count.

In the other direction, the headset's own `0x240` falls fast for a few minutes
after a battery goes in: 49, 46, 35, 32, 31 over about twenty minutes, 1% in 30
seconds at the end, far faster than the headset drains in use *(measured)*.
Watching all 89 values for two and a half minutes during that fall, `0x240` was
the only one besides the signal strength that moved, so the headset reports no
better figure. Neither number is the app's to correct.

The second and third fields are 17 and 1 for the Charging Dock and 33 and
0 for the USB Transmitter, on every reading. They look like the kind of
transmitter and whether it has a battery slot, but nothing has moved them
*(inferred)*. The fifth has only been seen as 1.

The headset sends a slot's whole record, as an update, when something in
it changes: the spare going in or out, and a light's brightness
*(measured)*. The dock's slot kept its spare reading while the headset was
on the USB Transmitter, but whether it is live then, or the last value the
headset heard, is not known.

`0x260` and `0x270` sit beside the headset's battery and charging flag and
look like a second pair, but read 100 and 0 throughout ten days of Swarm
II's logs and did not move with the spare. They are not it.

In `control`: entries 1 and 2 are that transmitter's two light brightnesses,
0–100, readable and writable at slot + 1 and slot + 2 (`0x401`/`0x402` for
slot one, `0x421`/`0x422` for slot two). Writing slot two's changed only the
Dock's values, and its status ring visibly *(measured)*. On the Charging Dock,
light 1 is the status ring, which turns purple when the link runs high
bandwidth, and light 2 the battery slot ring *(confirmed by the owner, moving
each slider and watching which light changed)*.

**Never write a slot's base address.** `control[0]` takes the same values as
`info[0]`. Writing 2 to it once was followed minutes later by the headset
selecting that transmitter unasked and then talking to neither; writing 0 did
not undo it. Not proven, and not to be proven on anyone's headset: the app's
client refuses these writes.

## Equaliser presets

Each bank has four factory presets, fixed in firmware, and five custom slots,
ids 16 to 20 *(measured)*:

| Bank | Factory presets |
|---|---|
| Game | 1 Signature Sound, 2 Bass Boost, 3 Bass & Treble Boost, 4 Vocal Boost |
| Microphone | 1 Signature Sound, 2 Full, 3 Clarity, 4 Smooth |

**Listing.** Each custom slot answers its own read with its name and all ten
bands, without selecting it:

```
{"OR":"CG1","KVP":{"1700":{"name":"De Mud","bands":["30","20","-30", ...]}}}
```

An empty slot does not answer. Swarm II lists presets with exactly these ten
reads and keeps no list of its own.

**Saving** is three writes, with no save command: the slot id to `0x1210`,
the ten bands, then the name to `0x12C0`, which commits it. The id is only a
hint: the headset uses the lowest free slot, and naming an occupied slot adds
a second preset rather than replacing it. Replacing is delete, then save.
Names are up to 19 characters.

**Deleting** is by name: write the preset's name to `0x1610` (game) or
`0x1630` (microphone). The count at `0x1600` (game) or `0x1620` (microphone)
drops by one and the slot is freed *(measured for the game bank)*. Reading
the delete register returns the last name deleted. `0x1640` and `0x1650` look
like a third count and delete pair; `0x1640` reads 1, and the Bluetooth
equaliser is the likely owner *(inferred)*. Deleting the selected preset
leaves the headset on an empty slot, so select a factory preset first.

Swarm II's "save to software" writes nothing to the headset; those presets
live only in Swarm II's own `STEALTH_PRO_II.ini` *(research)*.

## Buttons, dial and wheels

- **The buttons send nothing to Windows.** Pressing one produces only a
  change notification on the control channel.
- **Holding the Mode button sends nothing** while it is set to noise
  cancellation on/off. Three two-second holds changed nothing and produced
  no report, in two runs, while short presses in the same run reported
  `0x750` each time *(measured)*. A hold cannot be given a function by the
  PC.
- **The volume wheel is hardware.** It sends consumer-control volume keys
  (`0C 01` up, `0C 02` down, `0C 00` release), so Windows shows its volume
  overlay with nothing running. Hiding the overlay would mean swallowing
  every volume key on the machine, or a signed HID filter driver.
- **The chat wheel** reports on the control channel, as `0x510`, through the
  transmitter carrying the headset's settings. The wheel turns smoothly,
  without notches, and the count stops at 0 and 100. At an ordinary pace it
  reports every 0.1 to 0.2 s in steps of five or ten; turned fast, a single
  reading jumps 20 to 30 (40 straight to 70, 70 to 40, 35 to 65) *(measured)*.
- **The Bluetooth button** drives the headset's own radio and sends nothing
  to the PC, short or long press. It is not remappable.
- **The headset keeps a record of its inputs,** which the PC can read: RACE
  command `0x0000` with the byte `01` (read) is answered with a flags word, one
  bit for each press and each release, which stays set once it has happened,
  then where each of three wheels stands, all little-endian 32-bit numbers.
  On the Atlas Air the bits are, in pairs of press then release: 0 and 1 the
  power button, 2 and 3 the microphone mute switch, 4 and 5 the source switch,
  6 and 7 the button `0xB00` to `0xB20` set, 8 and 9 the media and call button,
  10 and 11 the mute switch's second contact, then 16 to 21 each wheel up and
  down (SP2Tools F126, read from the firmware). A Stealth Pro II on the
  Charging Dock answers it, relayed: `flags 0x00001002, wheels 30, 41, 0`,
  just after the app started, with its two wheels numbered *(measured)*; what
  each of its bits is has not been worked out. On the Atlas Air, Neap writes
  the reading to its log at connect and after each change the headset
  reports, so a tester's recording shows which button and which switch
  contact moved.

## Windows audio

### One stereo stream, so the mix happens on the PC

Each transmitter's USB descriptor offers one playback endpoint, two channels,
and no other mode *(measured)*. The headset receives audio already mixed, so a
game and chat mix on a PC has to be made on the PC.

Session volume does it with nothing in the audio path *(measured with a real
Discord call and music playing together)*:

```
mix  50   chat x1.00  game x1.00   endpoint peak 1.0000   both
mix 100   chat x1.00  game x0.00   endpoint peak 0.8529   Discord alone
mix   0   chat x0.00  game x1.00   endpoint peak 0.6876   Spotify alone
```

The alternative, capturing the chat app by process loopback and silencing its
own session, cannot work: process loopback taps the stream after session
volume, so silencing the session silences the capture (peak 0.4450, then
0.0000) *(measured)*.

The limit is that chat must be its own process. Discord, Teams and Steam are;
a call in a browser is not, because the browser also carries other audio.
That case needs a virtual audio cable. Any virtual audio device is a kernel
driver needing an EV certificate and Microsoft attestation; the USB/IP route
needs test signing, which breaks common anti-cheat *(research)*.

Per-endpoint session volumes are separate: an app can sit at 0 on the headset
and 1.0 on the speakers at once *(measured)*.

### Device formats

The format shown in Sound settings is a device property, not the mix format,
which is always 32-bit float in shared mode.

- **Changing it takes `IPolicyConfig::SetDeviceFormat`**, the undocumented
  interface the Sound control panel uses. Writing
  `PKEY_AudioEngine_DeviceFormat` directly persists and reads back, and
  changes nothing: the endpoint stays at the old rate *(measured)*.
- **It only takes effect on a live endpoint.** With nothing playing, the call
  records the new default and the next client pins whatever the service was
  last running.
- **24-bit is packed.** Block align 6 for stereo, not a 32-bit container;
  the padded form is refused by `IsFormatSupported` and by the device.
- **A written format must match the driver's byte for byte.** Mono uses
  channel mask `0x4` (front centre). Writing `0x1` works and adds a second,
  unlabelled entry to the Sound dialog.
- **Check with `GetMixFormat`.** When it disagrees with the stored default,
  the stored default is not in effect.

Formats offered by this headset *(measured)*: output 16- or 24-bit at 48 or
96 kHz; microphone 16-bit at 16 or 48 kHz.

**High bandwidth** is the Windows format and nothing more. At 24-bit/96 kHz
the Charging Dock's status ring turns purple and stays purple. Comparing all
101 values the headset and transmitter report before and after, only the
signal strength moved, and Swarm II stores no such setting *(measured)*.

**The microphone turns the ring white.** When an application starts
recording from the headset, the ring turns white within moments and stays
white after the recording stops, while Windows goes on reporting 24-bit/96
kHz throughout. Stepping the format to 48 kHz and back turns it purple
again, with the microphone still open, and it stays purple *(confirmed by
the owner, three times, with Discord and Teams)*. The format Windows sends
does not change while it is white; whether the dock's radio link to the
headset does is not known.

Only the first recording turns it white. Joining a Discord call while
another application already had the microphone open left the ring purple
*(confirmed by the owner)*.

**What the dock is sent when the ring turns white** *(seen on the wire, USB
capture of the dock alone, 23 October 2026, ring colours read by the owner)*.
Opening the headset's microphone sends the dock two requests and nothing
else: the microphone's streaming interface set to its working setting, and
the sampling frequency 48 kHz. The ring was white a few seconds later.
Closing the microphone sends the interface back to its idle setting, and the
ring stayed white.

Setting the format again did not turn it purple in two tries, once with the
microphone closed and once with it open, with nothing else playing. In both,
the dock saw the playback stream stop, then start at the old rate for under
40 ms and stop again, and never a playback stream at 96 kHz after the step.
A silent stream, and the stream of an inaudible tone, sent the dock nothing
at all, and the ring stayed white.

**Starting real playback turns it purple** *(seen on the wire, a second
capture, owner reading the ring)*. With the ring white and nothing playing,
the owner played music. The dock was sent the playback interface set to idle
and then to its working setting 42 ms later, then the sampling frequency
96 kHz; five seconds later the ring was purple. That is the only playback
start in the capture, and the only thing that changed the ring after the
microphone turned it white. So the ring follows the last stream the dock is
told to start: the microphone, at 48 kHz, white; playback at 96 kHz, purple.
Setting the format again turns it purple only when something is playing,
because that makes Windows start the playing stream again at 96 kHz, which is
the owner's confirmed case with Discord and Teams. With nothing playing, the
ring stays white until anything plays, and then turns purple by itself, as it
did between two tests earlier the same day. Whether the colour follows the
rate or only the start, and whether the dock's radio link to the headset
changes while the ring is white, are not known. Windows keeps sending
24-bit 96 kHz to the dock throughout *(measured)*.

**The Charging Dock can go silent at 96 kHz.** Windows was mixing at
96 kHz, playing to the dock at a healthy level, with the sound link at 2,
and nothing was heard; 48 kHz played, and so did the Sound dialog's own
setting of 96 kHz once the dock had been unplugged and plugged back in
*(confirmed by the owner)*. Nothing Neap or Windows reads marks the silent
state. It followed days of Neap setting the format again after each
microphone opening, at times four in under a minute, which is the
suspected cause and is not proven.

A leftover of Swarm II's Waves driver stays on the headset's output after
the driver is removed: Waves MaxxAudio named as its effects, pointing at
components no longer registered. Windows offers no Audio enhancements
switch for it, so it is taken to be inert *(measured)*.

### The headset sleeps after seven seconds of quiet

After about seven seconds with nothing to play, the start of the next sound
is lost: the headset side wakes on sound, not on digital silence, and drops
what plays while it wakes. Windows is not what sleeps; a stream held open
playing silence did not help, and only sound did *(measured)*.

Twelve rounds of two plain beeps a second apart, each on a freshly opened
stream, after a quiet spell, with the owner saying which they heard
*(measured)*:

| Quiet before | Rounds | First beep | Second beep |
|---|---|---|---|
| 3.1 to 6.8 s | 4 | heard every time | heard every time |
| 7.5 to 31.8 s | 8 | lost every time | heard every time |

A hiss too quiet to hear before a beep wakes it: 100 ms clipped the start of
the beep, 200 ms and 300 ms did not *(measured)*. Played without a break, the
same hiss could be heard in a quiet room *(confirmed by the owner)*.

### Core Audio

`S_FALSE` (1) is success: Core Audio returns it when a set changes nothing,
such as muting what is already muted. Only a negative HRESULT is a failure.

### A call holds the microphone from start to end

A program recording has an active session on that microphone, which is what
a profile set to switch on the microphone watches *(measured)*. Teams holds
the microphone for the whole of a call: from joining, even joined muted,
through muting and unmuting in Teams, until the call ends, when it lets go
*(confirmed by the owner)*. Discord was seen holding it too *(measured)*;
when it lets go was not checked.

## The firmware layer

Beneath Turtle Beach's protocol, Airoha's own commands read and write the
firmware's stored settings on the same HID channel *(measured)*:

```
read   05 5A 06 00  00 0A  <key:2 LE>  <length:2 LE>
write  05 5A <len:2 LE>  01 0A  <key:2 LE>  <data...>
reply  05 5B <len:2 LE>  <cmd:2 LE>  <status or length...>
```

Frames over 59 bytes split across reports. Swarm II ships the dictionary of
653 headset settings and 361 transmitter settings as plain XML, with factory
defaults, under `Data/Firmware/STEALTH_PRO_II/<vid_pid>/<version>/`
*(research)*. Its `NVID_*` names are firmware storage and are a different
namespace from the runtime keys above.

Two findings make this layer a dead end for changing behaviour:

- **These commands address the USB device you are plugged into.** They do
  not pass through a transmitter to the headset. With only a transmitter
  connected you are reading the transmitter's storage. The same key holds
  507 bytes on the transmitter and 529 on the headset.
- **Storage is an output, not an input.** Changing noise cancellation
  normally updated its stored value within two seconds, both times. Writing
  `NVID_TB_USR_WAKE_ON_MOTION` directly left the device reporting 0,
  including after a power cycle. Writes are accepted, read back and survive
  reboots, and change nothing.

The LED pattern table (`NVID_APP_LED_PATTERN`, `0xF283`) is a header byte and
11-byte records, one per light state: 48 on the headset, 46 on the
transmitter, twenty of them blank. Blanking it changed nothing, which by the
finding above proves nothing either way.

## What is not possible

- **Changing what the lights do.** Only brightness is reachable. Swarm II has
  no behaviour command, no category is lighting-related, a dozen guessed read
  verbs are silent, and the firmware does not consume its stored LED table.
  The decompressed firmware settles it *(research)*: a lighting task drives
  the LEDs from connection state in code, with no settable input feeding it,
  so e.g. the power-button LED's Bluetooth-only flash cannot be turned off by
  any value. The only override is the chip's raw factory GPIO channel, which
  is the same diagnostic back door behind the 2025 RACE vulnerabilities and
  is off-limits here. What remains is modifying the firmware image, which
  this project does not do.
- **Remapping the Bluetooth button.** See above.
- **Hiding the Windows volume overlay** from the headset's wheel, at any
  reasonable cost.
- **Switching Bluetooth from software.** No writable key found.
- **Muting the boom microphone on the headset, with its tone and red ring.**
  With the boom in, the arm is its only mute. Writing the arm's `0x600`
  changes nothing, a write to the built-in microphones' `0x650` is undone,
  and Swarm II's Mute Mic one-press command, AppkeyCode `0x3D`, sent twice
  in SP2Tools' proven appkey frame, did nothing: no tone, no red ring,
  passthrough still on *(measured, 2026-10-22)*. The headset's mute is
  gated on the boom-detect line, GPIO 5, which reads low with the boom in
  *(research, firmware code)*. The two gain writes its mute makes, −96 dB
  on `0x02020200` (NVKey `0x5052`) and `0x03020400`, could be sent through
  RACE `0x000A`, without the tone or the ring, but each is saved to the
  headset's settings. Not tried, by the owner's choice: a mic mute is not
  worth a stored write. So with the boom in, Neap mutes in Windows.

Studying Swarm II's copies is fine; sending anything down the update path is
not.

What a copy shows *(research, 4.107.703)*: each image is an Airoha update
package. The first 4 KB is a plain header — a digest, the version, the chip
and design names, and a table saying where each part loads. The body after
it is **compressed, not encrypted**, and decompresses to the real firmware:
Airoha's Bluetooth-audio SDK for this chip family, the headset and
transmitter built from Airoha's own reference designs. The earlier reading
of this as encrypted was wrong; the high randomness was compression.

This is still a dead end for changing behaviour, for the reasons already
given: the settings the firmware stores are outputs, not inputs, and Neap
writes no flash and never touches the update path. Being able to read the
firmware explains *why* the headset behaves as it does; it does not give a
way to change it. The one new fact worth keeping is that the chip carries a
vendor diagnostic channel that can read and write its memory directly. It is
the same channel behind the 2025 "RACE" Bluetooth headphone vulnerabilities
across many brands. Neap stays away from it, which the no-flash rule already
ensures.

A second pass in Ghidra on the headset's main-chip code *(research,
4.107.703)* checked the lighting claim directly. The decompressed headset image
holds both processors' code. The main chip's part is built to run from
`0x080C9000`, and 1,753 of its pointers line up with its text at that
address. What it showed:

- **The pin map is fixed per model.** At start-up the firmware reads the
  product id, picks a built-in table of 13 or 25 pin descriptors for that
  model, and sets the pins up once. Nothing stored changes it.
- **The lighting task is event-driven.** It is registered as `TB LED Task`
  and waits on internal events; the lighting code logs under `TB_LED_LOG`.
  Which event lights which light was not traced.
- **Inconclusive:** no call reads `NVID_APP_LED_PATTERN` (`0xF283`) with a
  fixed key. That fits "nothing feeds it", but `NVID_TB_USR_TX2_LED_INTENSITY`
  (`0xF403`), which does change brightness, is also never read with a fixed
  key, so the absence does not tell the two apart. Many reads build the key at
  run time. The on-device test above (blanking the table changed nothing)
  remains the stronger evidence.

### How Swarm II updates firmware

Read from Swarm II's shipped programs, statically, with nothing run and
nothing sent to a device *(research, Swarm II as installed 14 October 2026;
the images are 4.107.703)*. The headset's plugin does not update anything
itself. It hands the work to a .NET library, `Turtlebeach.Mosit`, which wraps
Airoha's own native update library, `AirohaHidCoreLib.dll`. The native library
was not opened, so the transfer itself is unread.

The sequence, as the plugin and the .NET library call it:

1. **Pick the files.** The folder's `version.ini` names the headset's main
   package, its second-chip package (`slavefile`) and a settings file for each
   (`nvkey`, `slavenvkey`). The transmitter has one package and one settings
   file. A missing package stops the update with a logged error.
2. **Check the package.** The library asks the native side for the package's
   own description and returns yes or no for the connected device (headset or
   transmitter, over the air or wired). That it refuses a mismatch is
   inferred from the name and the checks around it, not observed.
3. **Check the battery.** A low battery stops the update before it starts, and
   the level is passed to the native side.
4. **Transfer.** The main package path and, for the headset's two chips, the
   second package path are handed to the native library, which sends them and
   reports progress and status through callbacks. Nothing in the .NET code
   sees the bytes.
5. **Apply.** The status handler calls the native "apply new firmware" entry
   point; the log line reads "Start to apply new firmware". What the device
   does at that point is unread.
6. **Restore settings.** The settings files are written to the device with
   the same stored-setting commands as *The firmware layer* (write, then
   validate), on a background thread, and the device is rebooted. That this
   comes after the apply step is inferred from the log lines.
7. **Report.** The screen moves from "required" or "optional" to progress to
   done or failed. Failure says to try again or skip.

Facts that shape any attempt to copy it:

- **Swarm II's own text** says the headset supports over-the-air updates with
  no cable, and that "connecting 2 USB wireless transmitters is only required
  for firmware updates". Whether an update fails with one transmitter was not
  tested.
- **"Required" versus "optional" comes from the shipped files**, compared with
  the version the device reports. How the files arrive on the PC (Swarm II's
  own updater) was not traced.
- **The dangerous part is the native library.** The transfer, apply and any
  checks sit behind about twenty native entry points. Nothing in the .NET
  wrapper looked like a signature or checksum check, but the wrapper's code
  was read as call lists, not decompiled, so that is not established.
  Whether the device refuses a damaged or foreign image is unknown.
- **Unresolved:** what the native library sends on the wire, how it recovers a
  transfer cut halfway, and whether a transmitter or headset left half-updated
  can be revived without Swarm II.

## Pitfalls

- **A transmitter answers for a headset it can no longer reach**, with the
  last values it had, and a command to the wrong device succeeds. Prove which
  device answered before trusting a result.
- **A pattern that only matches the expected shape drops the rest
  silently.** Letters-only category names dropped every `3DT` event;
  non-greedy brace matching cut nested objects in half, so the preset and
  transmitter slots looked unreadable and lighting looked write-only. Count
  braces, and treat anything unparsed as a failure, not an absence.
- **A value that reads back proves the write, not the effect.** The device
  format, the firmware storage and the two Windows mirrors all read back
  perfectly while nothing changed.
- **A missing option is often a gap in the question.** The microphone's
  16 kHz format stayed hidden while the list of rates asked about started at
  44.1 kHz.
- **NAudio caches the session list.** `AudioSessionManager.Sessions` returns
  the same collection until `RefreshSessions()`, so apps started later are
  never seen.
- **Swarm II polls this channel about 2,700 times a second** and takes the
  replies. Only one program can usefully talk to the headset at a time.

## Capturing Swarm II

Everything in the registry was matched by operating a control in Swarm II
while capturing its USB traffic.

1. Install USBPcap (1.5.4.0 was used) and reboot; it attaches only after a
   reboot.
2. Capture the headset's device, as administrator, to a short path (USBPcap
   fails silently past the Windows path limit):
   `USBPcapCMD.exe -d \\.\USBPcap<n> -o out.pcap --devices <address> --inject-descriptors`.
   `tools/capture.ps1 -Interface <n> -Device <address>` wraps this, and its
   help says how to find both.
3. Decode it with the probe: `Neap.Probe decode out.pcap` prints every
   command and reply, naming the keys already in the registry.

Swarm II's own files are worth reading before capturing: its catalogue
(`settings.json`), its profile store (`STEALTH_PRO_II_PROFILE_Mgr.dat`, a Qt
snapshot of every category and key it knows, which is how the preset slot
categories were found) and its device plugin.

Swarm II keeps a settings file per headset, `<MODEL>.ini`, in
`%APPDATA%\Turtle Beach\Swarm II\Setting` *(measured, Swarm II on the owner's
PC)*. Its `m_device_setting` entry is a QSettings-escaped Qt data stream of
every category, key and value Swarm holds for that headset, five times over,
one for each of its profile slots: 477 values for the Stealth Pro II, in the
same categories and keys as `set_kvp`. No Turtle Beach code needs reading to
get them.

Comparing the file before and after a change in Swarm names the key behind
the control. Five changes made in one session with Swarm, then Swarm closed,
gave:

| Change | Keys that moved |
|---|---|
| Mic monitoring slider | `0x620` 0 → 100 |
| Game equaliser preset | `0x1210` 3 → 1, its bands, and the name at `0x12C0` |
| Auto shut-off | `0x280` 2 → 4 |
| The headset's volume wheel | `0x260` and `0x2A0`, both 54 → 36 |
| Superhuman Hearing on, then off again | nothing: the value ended where it began |

Three limits follow. The file is only certainly written when Swarm closes:
one session saved three times in a minute while in use, the next only on
closing, so a change can only be named alone by closing Swarm after it. A
change undone before the save leaves no trace. And the file holds what the
person named things, the headset's name and their profile names, which a
report would leave out.

`0x260` moved with the volume here, the same as `0x2A0`, which does not
match the ten days of Swarm logs under *Transmitter slots*, where it stayed at 100.

## Open questions

1. The 17 unidentified values, listed in the registry as `*_unknown_*`. Each
   needs its control found and operated while watching (`watch` or `diff` in
   the probe).
2. What the frame's tag byte encodes.
3. Whether anything marks the silent state that `0x230` misses. No value in
   `Inf` or `GSI` does (see *No sound, and nothing says so*); the slots and
   Windows' own meters are what remain.
4. Whether `0x250` is charging or cable power; see *General state*.
5. Whether the microphone bank's delete register behaves like the game
   bank's (not exercised: there were no custom microphone presets).
6. Whether the Charging Dock's slot reports the spare battery live while
   the headset is on another transmitter; see *Transmitter slots*.
