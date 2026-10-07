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
| `0x220` | the headset's name, as set in Swarm II ("My Headset") |
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
| `0x510` | game and chat mix, 0–100. The chat wheel reports its position here |
| `0x600` | microphone muted by the boom arm, 0/1: 1 while it is flipped up. Writing 1 with the arm down is accepted and does not mute; the microphone went on picking up speech. A Windows mute of the microphone does not change it *(both measured)* |
| `0x610` | a **mirror** of the Windows recording level, not a control |
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
  transmitter carrying the headset's settings.
- **The Bluetooth button** drives the headset's own radio and sends nothing
  to the PC, short or long press. It is not remappable.

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
the owner, three times, with Discord and Teams)*. Whether the sound itself
changes while the ring is white is not known.

Only the first recording turns it white. Joining a Discord call while
another application already had the microphone open left the ring purple
*(confirmed by the owner)*.

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

### Core Audio

`S_FALSE` (1) is success: Core Audio returns it when a set changes nothing,
such as muting what is already muted. Only a negative HRESULT is a failure.

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
