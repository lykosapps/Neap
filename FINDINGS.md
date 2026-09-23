# Stealth Pro II — third-party control app

**Feasibility findings — 21 September 2026**

Everything below was measured on one machine and headset unless marked *(research)*.

---

## The short version

It works. A normal Windows app can read the headset's state and change its settings.

- No admin rights, no driver to write, nothing to code-sign.
- The headset takes **plain-text instructions** — no encryption, no password, no handshake.
- Proven end to end: we sent a command and watched the setting change.
- **Game/chat mix works with no Waves driver at all** — confirmed by ear with Waves uninstalled and a free virtual cable in its place. This is the fix for Xbox-edition owners whose chat mix is broken.

The three original objectives:

| Objective | Verdict | What's left |
|---|---|---|
| Game/chat mix without Waves | **Achieved and confirmed on hardware** — our own capture-and-mix engine, no Turtle Beach audio driver present | Done; validated with Waves uninstalled + VB-CABLE |
| Full button remapping | **Achievable, and easier than expected** | Nothing — options are mapped |
| Lighting brightness | **Achievable** | Nothing — both controls captured and confirmed |
| Lighting behaviour/patterns | **Not reachable from the host** | Firmware logic with no setting or command behind it — see *Changing LED behaviour: every host route checked*. Only the signed firmware image could change it |

---

## What the hardware actually is

- **Stealth Pro II Xbox edition.** Released 17 May 2026, $349.99 *(research)*. The Xbox and PC editions are different hardware and their transmitters are not interchangeable *(research)*.
- Ships with **two transmitters**: a dock that also charges a spare battery, and a small USB-A dongle *(research)*. Up to four can be paired, one active at a time.
- The thing that appears on the PC is the **transmitter**, not the headset. The headset is reached over the transmitter's 2.4GHz link.
- Inside is an **Airoha AB1571** chip. Turtle Beach's own firmware files name it directly.

**Identity on this machine:** USB vendor `0x10F5`, product `0x229B`. Firmware packages also exist for `0x229D` and `0x229E` (the headset itself, master and slave earcups).

`0x229B` is the **Charging Hub**, not the USB transmitter — this was recorded the wrong way round for most of the project, guessed from the order of the firmware folders. Confirmed two ways: it is the only Turtle Beach device present while the hub is plugged in and the USB transmitter is not, and it reports *two* LED brightnesses in its control block, which is the battery-eject ring and the dock-status ring. A bare transmitter has no pair of LED rings. `0x229D` is the USB transmitter by elimination; none has been plugged in here.

Worth noting how long that survived: every read was correct, every command went to the right place, and the only thing wrong was the word the interface printed next to it. Nothing failed, so nothing questioned it.

---

## How control works

The transmitter presents two HID collections to Windows. One is standard media keys. The other is the control channel:

| | |
|---|---|
| Usage page | `0xFF13` (vendor-defined) |
| Interface | 3 |
| Outbound | SET_REPORT, **output report ID 6**, 62 bytes |
| Inbound | GET_REPORT of type **Input**, **report ID 7**, 62 bytes |
| Access | Opens read+write from a normal, non-elevated process |
| Exclusivity | None — coexists happily with Swarm II running |

This matches the Airoha **RACE** protocol documented publicly for a sibling Turtle Beach headset *(research)*, so the transport is not unique to this model.

### Frame layout

Turtle Beach layers a text protocol on top of RACE. A real captured command — set ANC level to 100:

```
06                      HID output report ID
2A 00                   total length, little-endian (42)
05                      RACE start byte
5A                      RACE type: 5A = command, 5B = response, 5D = notification
26 00                   inner payload length, little-endian (38)
02 99                   RACE command id (0x9902 — Turtle Beach vendor command)
48                      constant
03                      01 for a bare command, 03 when arguments follow
FB DE 44                message counter (two bytes increment per message)
00 x7                   padding
B7                      tag preceding the command name
73 65 74 5F 6B 76 70    "set_kvp"
FF                      separator
7B 22 30 78 37 36 30    {"0x760":"100"}
22 3A 22 31 30 30 22 7D
00 ...                  zero-padded to 62 bytes
```

Both length fields are derived from the JSON length: total = `len + 27`, inner = `len + 23`.

**The tag byte at offset 21 is not fully understood.** It is `0xB7` for `set_kvp` and `0x61` for `SGSI`. Until that's decoded, build commands by copying a known-good frame and patching the two lengths, the counter and the JSON — which is exactly how we verified the write worked.

### Replies

Responses and notifications arrive on report 7 with the same framing and RACE type `5B` / `5D`.

Two traps here, both found by building the client and both silent if you get them wrong:

- **It is an Input report, not a Feature report.** The collection declares no feature reports at all, so `HidD_GetFeature` fails. Use `HidD_GetInputReport` — which is what Swarm II does.
- **The inbound length is 16-bit little-endian**, exactly like outbound. Reading it as a single byte truncates every fragment by one byte. Short replies still parse, so this only surfaces on replies long enough to span several reports.

Long replies continue in follow-on reports whose payloads are raw continuation bytes with no RACE header, and the split falls anywhere — including mid-string. Unrelated notifications interleave freely, so accumulate payloads and consume only what parses rather than clearing the buffer on each event.

Sample notification: `{"UP":"SAF","KVP":{"750":"1"}}`

### The general-state block

`SGSI` returns fifteen values. Four are identified so far:

```
200=1  210=0  220="My Headset"  230=2  240=29(battery %)  250=0
260=100  270=0  280=4(auto shut-off)  290=3  2a0=20(volume)
2b0=60  2c0=50  2d0=78  2e0=0
```

The rest are readable but unlabelled. Watching them change while operating known controls would name them; none needs a write.

### Bluetooth and charging, named at last *(confirmed both ways, 22 Sep 2026)*

Two values in the general-state block had sat unlabelled since the beginning.
Neither needed a guess in the end: the probe was made to poll **every** value
the headset answers and print only what moved, and then the hardware was
operated while it watched. Nothing was filtered to what was expected, which on
this project is the difference between a finding and a wrong finding.

Watching `0x290` across four actions:

| | `0x290` | what had just happened |
|---|---|---|
| start | 3 | on 2.4GHz, Bluetooth connected |
| | 2 | Bluetooth disconnected |
| | 4 | headset plugged into USB |
| | 5 | Bluetooth reconnected |
| | 3 | USB cable pulled out |

Only one shape fits all five:

```
bit 0      Bluetooth is connected
bits 1-2   how it is attached: 1 = 2.4GHz, 2 = USB
```

`0x250` went 0 -> 1 as the cable went in and 1 -> 0 as it came out, and the
battery percentage climbed only while it read 1 — 90, 91, 93, 95 across the
plugged-in stretch and flat afterwards.

**`0x250` is read as charging, not as "a cable is attached".** Three things
point that way:

- The attachment is already reported, in `0x290`. A second flag that only
  repeated it would be carrying no information.
- USB-C here is not a charging port with audio bolted on. Plugged into the PC
  the headset enumerates as its own playback *and* recording device —
  "Stealth Pro II Xbox Headset", separate from the transmitter's "Stealth Pro
  II Xbox" — so "cable in" and "charging" are genuinely different facts about
  it. Turtle Beach's own listing for the cable says the same: charging and
  wired audio *(research)*.
- The battery percentage moved only while it read 1.

What would settle it is a headset sitting at 100% with the cable still in. If
`0x250` drops to 0 while `0x290` still says USB, the name is right.

**Seen once since** *(2026-09-23)*: at 100% with the cable still in, `0x250`
went to 0 and the app stopped saying "charging". `0x290` was not read at the
time, so that is the first half of the test rather than the whole of it.

**A correction to how this was planned.** The capture was set up expecting the
headset to *dock* on the charging hub. It does not: the hub is a USB device
that charges a spare battery, and charging the headset itself means a USB-C
cable. The original deferred task asked for a capture "while docking and
undocking", which would have found nothing.

### Swarm's own product catalogue names the whole family *(confirmed)*

Swarm II ships its catalogue in `settings.json` — zlib behind a four-byte
header. It maps every product id to a name, and it settles the naming this
project spent a long time guessing at.

| PID | Swarm's name | Colour |
|---|---|---|
| 2283 | STEALTH PRO II XBOX **BASE** | Black |
| 2284 | XBOX BASE | Black |
| 2285 | XBOX TRANSMITTER | Black |
| 2286 | XBOX HEADSET | Black |
| 229F | XBOX TRANSMITTER (GIP) | Black |
| 229B | XBOX **BASE** | White |
| 229C | XBOX BASE | White |
| 229D | XBOX TRANSMITTER | White |
| 229E | XBOX HEADSET | White |
| 22A0 | XBOX TRANSMITTER (GIP) | White |
| 2287 | PC BASE | Black |
| 2288 | PC TRANSMITTER | Black |
| 2289 | PC HEADSET | Black |

Three things fall out of it.

**The corrected guess was right.** 229B is the docking device and 229D the
transmitter — which had been recorded the wrong way round, then fixed by
experiment. The catalogue agrees with the experiment.

**2283 is identified.** It had turned up in Windows' device history and
matched nothing we had: it is a black Xbox base.

**Swarm calls it a "base", the box calls it a "Transmitter Dock".** The box
wins for anything a person reads; the catalogue is what the software uses.

The app now carries the whole family rather than the four ids this machine
happened to show, so it names a PC-edition or black-edition device correctly
for somebody else.

**No sign of transmitter switching anywhere in Swarm.** The only
transmitter-related strings in its binary are an icon path and the phrase
"connect with a dongle." — instructional, not a control. Taken with 0x4xx
being readable slot data whose only writable fields are lighting brightness,
switching looks like the headset's CrossPlay button's job and nothing else's.

### Correction: controls stay where the headset was switched on *(measured, 2026-09-23)*

**The section below is true of the state it measured, and wrong as a rule.**
The USB Transmitter does carry controls — when the headset was switched on
with it. What decides it, on every observation so far, is this:

> The headset keeps its settings and chat wheel on the transmitter it was
> switched on with. CrossPlay moves only its sound.

Each observation, with the state confirmed as it happened:

| how the headset got there | sound on | answers for settings | evidence |
|---|---|---|---|
| switched on with the Charging Dock, CrossPlay to the USB Transmitter | USB Transmitter | Charging Dock | wheel swept the mix 0-100; noise cancellation toggled audibly |
| switched on with the USB Transmitter | USB Transmitter | USB Transmitter | answered GSI with its name and values; pushed a live Bluetooth signal update |
| then CrossPlay to the Charging Dock | Charging Dock | **USB Transmitter** | asked one at a time: USB Transmitter 15 values, dock nothing; wheel moved the mix; noise cancellation toggled audibly |
| Charging Dock unplugged while in use, CrossPlay to the USB Transmitter | USB Transmitter | nothing | silent on both paths — the controls were on the dock, which was gone |
| **Charging Dock unplugged, headset switched off and on with only the USB Transmitter** | USB Transmitter | **USB Transmitter** | answered for everything, battery 74%; the chat wheel swept the mix 0-100 both ways |

The last row is the state the section below measured, and it explains it. How
the headset reached the USB Transmitter in the original measurement was not
recorded; CrossPlay from the dock would account for it.

**The prediction held.** With only the USB Transmitter plugged in, a headset
switched on with it answers for everything — full settings and a working chat
wheel with no dock at all. Still a rule of thumb rather than a mechanism, but
five observations and no exceptions.

**What it means for people.** Somebody who only ever uses the USB Transmitter
switches the headset on with it, so everything works for them. The limited
state needs a headset switched on with the dock and then moved over — and it
ends by switching the headset off and on, not by finding a dock.

**Asking one device at a time is how this was settled.** `who` in the probe
asks every transmitter plugged in, separately. Everything else opens the first
device that answers and stops, which cannot tell you which of two is carrying
the controls.

### The plain transmitter carries sound and no control *(measured)*

With only the USB transmitter plugged in — the dock unplugged — audio plays
through the headset perfectly and **nothing answers the control channel at
all**. Everything on our side was ruled out before that was believed:

| checked | result |
|---|---|
| Parsing | raw dump: 428 reports read, **none carried a payload**. Nothing is arriving to misparse. |
| Feature reports | declares none, exactly like the dock. |
| Write path | `HidD_SetOutputReport` *and* an interrupt `WriteFile` both accepted. Neither drew a reply. |
| Patience | 8 seconds per request. Nothing. |
| Interface shape | 62-byte in and out, usage page `0xFF13` — identical to the dock. |

So it accepts every frame and relays none of them. The dock does the control
work; the transmitter is an audio bridge.

**Not an edition mismatch.** That was the first suspicion and the box settles
it: the Stealth Pro II ships with both a "CrossPlay 2.0 USB Wireless
Transmitter" and a "CrossPlay 2.0 Transmitter Dock". They are a matched pair.
Those are also Turtle Beach's own names, and what the app now calls them —
"charging hub" was ours.

**The chat wheel does not survive it either** *(controlled test)*. Listening on
the interrupt endpoints of both of the transmitter's collections, with the
volume wheel as a control:

- The **volume wheel** produced 84 reports on the consumer-control collection,
  page `0x000C` — `0c 01` while turning up, `0c 02` while turning down. So the
  link was alive, the headset was awake, and the harness worked.
- The **chat wheel**, immediately afterwards, produced **nothing at all** on
  either collection.

A first attempt at this saw nothing from *either* wheel and would have been
read as a result. It was the harness: it polled `HidD_GetInputReport`, and a
momentary control is an interrupt report, not a state you can ask for. The
control is what caught it.

The likely mechanism is that the chat wheel adjusts a mix inside the headset
for console use, where game and chat arrive as separate streams. Over a PC
transmitter there is one stream and nothing for it to act on, so it acts on
nothing and reports nothing.

**Scope: this is true of the transmitter on its own.** Plug the dock back in
and control returns in full — including the chat wheel — *even while the
headset's own slots report the USB transmitter as the active one and the audio
is going through it*. Measured: with both plugged in and the transmitter
active, sweeping the wheel moved the mix 39 times across the full 0-100 range
and settled on the detent.

So the two are not one link. Which transmitter carries the headset's **audio**
and which one answers for its **settings and controls** can be different
devices at the same time, and the app has to ask rather than assume either
from the other.

**Re-measured on 2026-09-23, and it holds for writes too.** Both plugged in,
the headset on the USB Transmitter, sound playing through it:

- the chat wheel moved the mix 30 times across the full 0-100 range, sampled
  every 150ms, and settled on the detent;
- noise cancellation switched off and on from the app, **audibly**, and its
  intensity slider worked — so settings sent through the Charging Dock land
  on a headset whose sound is going through the USB Transmitter.

The app had been presenting this state as sound-only on the theory that the
dock was serving a stale copy of the headset's settings. It was not stale.
The theory was written without reading this section.

That also narrows what a person loses by travelling with the small
transmitter: everything, but only while the dock is not also plugged in.

**What it means for the app.** With the transmitter alone, the person hears
their headset while the app can read nothing from it, and reporting that as
"headset off" is a lie they can hear is false. There is a state for it now, and it is claimed only when no dock is plugged in, because a
dock would have answered.

### 0x150 looks like the audio link, and "active" is not *(one direction only)*

The headset reached a state worth recording: **powered on, control link live,
no audio link to either transmitter.** Both transmitter lights amber; the app
said "Headset connected, 89%" and there was no sound on any output device.
Everything read normally — battery, volume, Bluetooth, and the chat wheel
moved the mix in real time — so the control channel was genuinely live, not
replaying a cache. It was the audio link that was absent.

Pressing the headset's CrossPlay button restored it, and the capture that was
running across the moment shows what moved:

```
  [00:21] 150       1 -> 2
  [00:21] TX1.400   info[0] "2" -> "1"     (USB transmitter, was active)
  [00:21] TX2.420   info[0] "1" -> "2"     (dock, became active)
```

Nothing else changed but the signal drift.

**`0x150` turned out to be narrower than that, and the guess is withdrawn.**
It is not "there is an audio link"; it is **"the headset is on *this*
transmitter"** — 2 when it is, 1 when it is not. `0x230`, the value we already
call connection state, read `2` throughout including while there was no audio,
so it is not this either. (It is the sound link for the cases it covers — see
*0x230 is the sound link*, below — and this is the episode it does not.)

Settled by watching it at ten samples a second while the headset was moved
between transmitters with both plugged in:

```
  [00:19.6] 0x150  2 -> 1
  [00:22.7] TX     Transmitter dock -> USB transmitter
  [00:29.6] 0x150  1 -> 2
  [00:32.6] TX     USB transmitter -> Transmitter dock
```

Both directions, and **audio was working perfectly through the other
transmitter the whole time** — which is exactly what rules out the audio-link
reading. Asked of the dock, the flag answers a question about the dock.

Two things worth keeping from it. It agrees with the transmitter slots' active
flag but costs one value instead of four round trips; and it moved about three
seconds *before* the slots did, on both transitions. So it makes a good
trigger: watch this, read the slots only when it changes.

**The original question is still open.** Nothing identified distinguishes a
healthy connection from one carrying no sound, which is the state this
headset reached with both transmitter lights amber and the app reporting
"connected, 89%". The remaining unlabelled values are the place to look, and
the way to look is to be in that state and sweep, which has not been
reproducible on purpose.

**"Active" does not mean connected.** Through the whole broken spell the USB
transmitter's slot claimed `info[0] == "2"` with no link of any kind. The flag
means "the transmitter the headset is assigned to", not "the transmitter that
is carrying it". Anything using it to describe a working connection is wrong;
naming which transmitter the headset belongs to is all it can support.

**This is one direction only.** `0x150` was seen rising as the link came back;
it has not been seen falling. The state is hard to reproduce on purpose, so it
should be caught opportunistically rather than acted on yet. What would settle
it: any future occurrence of no-audio-with-control, read `0x150` before doing
anything about it.

### 0x230 is the sound link, for the cases it covers *(measured, 2026-09-23)*

`0x230` reads **2 while a transmitter is sending the headset sound, and 0
while none is.** It had been taken for a flag that drops as the headset
switches off, and drove "Headset off" in the header. Watched through a day of
changes, with the state confirmed each time:

| what happened | `0x230` | sound |
|---|---|---|
| headset switched on | 0 for about fifteen seconds, then 2 | arrived as it went to 2 |
| the transmitter carrying the sound unplugged, the other carrying the settings | 2 → 0 | none; the remaining transmitter's light amber |
| CrossPlay, from that state | 0 → 2 | back, through the remaining transmitter |
| over the USB-C cable, with the dock connected | 2 | through the cable |
| the dock unplugged, cable still in | 2 → 0 | carried on through the cable |
| headset switched off | never dropped ahead of it | — |

So it is the **wireless** sound link: over the cable it says nothing about
what is heard. And it does not catch every silent state — through the
both-amber episode above it read 2 with no sound on any output.

**The chat wheel is lost with it.** While it reads 0 the wheel's clicks do not
reach the app, though the settings still answer; the turns made in the
meantime arrived together when the sound came back, 50 to 70 in one reading.

### The headset is 0x229E, and it turns up beside its dock

Plugging the headset in by USB puts a **second** Turtle Beach control device
on the machine: `0x229E`, alongside the dock's `0x229B`. It presents the same
vendor collection on interface 3 plus an audio endpoint, which settles what
`0x229E` is — the headset, as the firmware folders suggested.

That is also a hazard. `FindDevice` took the first collection Windows
enumerated, so with both present which device a write reached was luck, and
they are different devices with different storage. There is now an explicit
preference: the headset first, then a transmitter, then the hub.

### Over a cable, the headset plays one source at a time *(measured, 2026-09-23)*

Plugged into the PC by USB-C, the headset is a USB audio device of its own —
"Speakers" and "Microphone (Stealth Pro II Xbox Headset)", `0x229E` — and it
answers for its settings there too. With the Charging Dock connected as well:

| what was done | what happened |
|---|---|
| cable plugged in, music playing through the dock | Windows moved sound **and** microphone, every role, to the cable by itself; the music carried on over the cable |
| cable unplugged | Windows moved everything back to the dock by itself; the music carried on |
| cable in, output set back to the dock in Sound settings | the music played through the dock; Windows left the **communications** output and the microphone on the cable |
| a beep sent to the cable every three seconds, music on the dock | each beep **cut the music out** — the headset switches between the two, it does not mix them |
| spoken into with the cable in, both microphones recorded | the cable's microphone carried the voice; the dock's delivered data, but only silence |
| dock plugged back in, cable in | Windows kept everything on the cable |

So with the cable in, the cable is the only right place for sound, calls and
the microphone. Anything sent to a transmitter is cut out by whatever plays
on the cable, and a microphone set to the dock sends nothing.

Still untried: whether the dock's sound cuts out the cable's in turn, or the
cable always wins; and all of this with the USB Transmitter instead of the
dock.

### Switched off on its cable, the headset keeps a charging connection *(measured, 2026-09-23)*

Switched off with the cable in, the headset's speakers, microphone and
consumer-control device leave Windows and one vendor HID collection stays —
and **it goes on answering**, battery included. Switching off and on each
restart the USB connection: the device is gone for about three seconds and
comes back as the other kind. The sound device being there is what says the
headset is on.

---

## Confirmed commands

Every one of these was captured live from Swarm II and matched to a physical action.

| Key | Setting | Range observed | Category |
|---|---|---|---|
| `0x240` | Battery percentage | read-only | GSI |
| `0x250` | USB power in — 1 while cabled | read-only | GSI |
| `0x280` | Auto shut-off | option index | GSI |
| `0x290` | Attachment and Bluetooth | read-only | GSI |
| `0x2A0` | Master volume | 0–100, moves with the dial | GSI |
| `0x320` | Bluetooth signal strength | read-only, dBm | BT |
| `0x401` | LED brightness (first) | 0–100 | — |
| `0x402` | LED brightness (second) | 0–100 | — |
| `0x510` | Game/chat mix | 0–100 | — |
| `0x610` | Mic volume | 0–100 | Mic |
| `0x620` | Mic monitoring / sidetone | 0–100 | Mic |
| `0x630` | AI noise reduction | 0 / 1 | Mic |
| `0x700` | Noise gate | 0 / 1 | SAF |
| `0x710` | Noise gate threshold | 0–100 | SAF |
| `0x720` | Superhuman Hearing | 0 / 1 | SAF |
| `0x750` | ANC on/off | 0 / 1 | SAF |
| `0x760` | ANC level | 0–100 | SAF |
| `0xA20` | Dial / wheel function | option index | Enc |
| `0xB20` | Button function | option index | Btn |
| `0x1210` | EQ preset selection | preset index | AQG |
| `0x1220`–`0x12B0` | EQ band values, ten of them | −90 to +90, in tenths of a dB | AQG |
| `0x12C0` | EQ preset name | text | AQG |
| `0x200`–`0x2E0` | General state block | read-only | GSI |

Replies are tagged by category — `GSI`, `BT`, `SAF`, `Mic`, `Btn`, `Enc`, `AQG` — which the plugin also uses as its log prefixes.

### The microphone has two formats, and we only found one

Windows offers the headset microphone 16-bit/16000Hz ("Tape Recorder
Quality") and 16-bit/48000Hz ("DVD Quality"). Our probe reported one, and
the app told the user, in as many words, that there was no choice to make.

The probe walks a list of sample rates and asks the device about each.
The list started at 44100, because that is where music rates start. 16000
was never asked about, so it could never be found — and the absence was
reported as a fact about the hardware rather than a fact about the list.

This is the same shape as the parser bug recorded under *Risks*: a lookup
that only matches what you expected turns "I cannot see it" into "it does
not exist", and the answer arrives with full confidence either way. The
rate list now runs from 8000, and the quality names match the Sound
dialog's, which is also a way of noticing a mismatch early — if our
wording and Windows' wording disagree, one of us is wrong about the rate.

### A format must be byte-identical to one the driver offers

Writing a format that Windows accepts is not the same as writing the
*same* format Windows has. Ours differed by one field on mono, and the
Sound dialog grew a second, unlabelled entry:

```
1 channel, 16 bit, 16000 Hz (Tape Recorder Quality)
1 channel, 16 bit, 48000 Hz (DVD Quality)
1 channel, 16 bit, 48000 Hz              <- ours, and selected
```

The field was `dwChannelMask`. For one channel we were writing `0x1`
(SPEAKER_FRONT_LEFT) from a generic "first N speakers" expression;
Windows uses `0x4` (SPEAKER_FRONT_CENTER) for mono. Everything worked —
the format applied, the device ran at it — and the list quietly gained a
duplicate each time. Stereo was already right at `0x3`, which is why only
the microphone showed it.

Confirmed fixed on hardware: the duplicate is gone and the driver's own
"(DVD Quality)" entry is selected.

**Worth generalising.** Windows matches the stored format against the ones
its driver advertises by value, not by meaning. Any field that is
plausible but not what the driver uses — channel mask, valid bits,
container size, subtype — produces a format that works and is nonetheless
not theirs. The tell is the vendor UI showing an entry you did not expect,
which is only visible if you go and look at it.

The blobs Windows stores on this machine, for reference:

| device | ch | bits | valid | align | mask | subtype |
|---|---|---|---|---|---|---|
| headset mic | 1 | 16 | 16 | 2 | 0x4 | PCM |
| headset out | 2 | 24 | 24 | 6 | 0x3 | PCM |
| Realtek out | 2 | 32 | 24 | 8 | 0x3 | PCM |

Note the Realtek row: 32-bit there is PCM with 24 valid bits, not float.
Our builder assumes float for 32-bit, which no device here offers, so it
is untested and flagged in the code.

### The game side only attenuated applications that were already playing

Symptom: mix hard over to chat, and game audio still at full volume. The
engine reported `mix=100 chatGain=1.00 gameGain=0.00 held=5` — it believed
it had done the job.

Listing the endpoint's sessions directly showed why. Everything that had
been running when the engine started sat at volume 0. Spotify, opened
later, sat at 1.0 and had never been touched.

**NAudio caches the session list.** `AudioSessionManager.Sessions`
enumerates once and then returns the same collection until
`RefreshSessions()` is called. The engine re-read `.Sessions` on a two
second timer, which had been added for exactly this case — the comment
above it says "applications that start playing later were never turned
down until the slider next moved; sweep for them" — and it re-read the
same stale snapshot every time. A fix that could not work, sitting under a
comment explaining the problem it was meant to solve.

One line at each sweep site. Confirmed: with the mix hard over to chat,
every session except our own is now at 0 including ones started after the
engine, and everything returns to its own level as the mix comes back.

**What made this survive so long:** the engine's own `status` was
consistent with the bug. `held=5` is a count of what it had turned down,
not a count of what was playing, so it read as success. Any state a
component reports about itself is a claim, not a measurement — the only
thing that settled it was enumerating the sessions from outside and
looking at their actual volumes.

### Hold nothing down on a device nobody is listening to

The game side of the mix works by turning other applications down on the
headset endpoint. That is correct while the headset is where audio is
going, and wrong the moment it is not: switch Windows to speakers and the
levels stay pinned on a device nobody is using, to be discovered turned
down later with no explanation.

Per-endpoint volumes are separate — measured: with the mix hard over to
chat, an application sits at 0 on the headset and 1.0 on the speakers at
the same moment — so nothing is being copied across. The problem is simply
that we never let go.

The engine now checks on each sweep whether the headset is still the
default render endpoint. If it is not, it restores what it was holding and
stops; when the headset is the output again, it takes them back. If the
default cannot be read it carries on as before, because a mix that keeps
working beats one that releases on a transient error.

Verified: full chat with the headset as output, everything at 0; switch to
speakers, everything back to 1.0 within a sweep; switch back, attenuated
again.

### Editing a band clears the headset's selected preset

Write any EQ band and `0x1210` (game) goes to **0**. Confirmed by a full
read, not inferred from notifications. The headset is being reasonable
— the curve is no longer that preset, so it names none — but it destroys
the one thing an interface needs in order to offer a way back.

So "which preset is this a variation of" has to be the app's own state.
It remembers the last preset applied, keeps it while you edit, and drops
it only when another is chosen; the server holds it too, so reloading the
page mid-edit does not lose it. On a cold start with nothing remembered,
a live curve that exactly matches a stored preset is taken to be that
preset, and a curve that matches nothing is honestly shown as belonging
to nothing.

The same clearing takes the preset *name* away (`0x12c0` empties), which
is why the name shown beside the equaliser has to fall back to the
remembered preset — otherwise the label disappears exactly when you most
want to know what you are editing.

### Band centre frequencies

Read out of Swarm's own device plugin rather than guessed — the game set
sits at 0x204e40 in `STEALTH_PRO_II.dll`, the microphone's immediately
after it, and the `+9 dB`..`-9 dB` scale labels alongside confirm the
range our tenths-of-a-dB values cover.

| Bank | Bands |
|---|---|
| Game | 32 Hz, 63 Hz, 125 Hz, 250 Hz, 500 Hz, 1 kHz, 2 kHz, 4 kHz, 8 kHz, 16 kHz |
| Microphone | 100 Hz, 160 Hz, *250 Hz*, 400 Hz, 630 Hz, *1 kHz*, 1.6 kHz, 2.5 kHz, 4 kHz, 6.3 kHz |

Eight of the microphone labels appear literally and the bank has ten
bands. Qt pools identical string literals, so `250 Hz` and `1 kHz`
— already present in the game list — would appear only once for both
banks. Filling the two gaps with those gives the right count and a
standard voice series, but those two placements are inferred and are
marked as such in the code.

### Two settings the headset reports but does not control

`0x2A0` (master volume) and `0x610` (mic sensitivity) both look like
headset settings and are not. Each is a **mirror of a Windows endpoint
level** — the output endpoint's volume and the microphone endpoint's
recording level respectively.

Measured in both directions, for both:

- Move the Windows level and the headset reports the new number within a
  second, unprompted. That is why reading them looks convincing.
- Write the headset key and only the reported number moves. Nothing sounds
  different, and Windows puts its own value back at the next opportunity.

So a slider bound to either key is a control that appears to work and does
nothing — which is exactly what the app shipped for mic sensitivity:
changing it in Windows updated our slider, and moving our slider did
nothing at all. Both are now routed to the Windows endpoint instead, which
is the only thing that actually changes what you hear or what others hear.

The lesson generalises past these two: when a device reports a value that
tracks a PC-side setting, the tracking says nothing about which end owns
it. Write it and check for an audible effect before believing it is a
control.

### Reading settings back

Swarm II only ever sends two verbs: `set_kvp` to write and `SGSI` to read the general-state block. The rest were found by guessing that a request verb is `"S"` plus a category name, and confirming against the device:

| Verb | Returns | Range |
|---|---|---|
| `SGSI` | general state | `0x2xx` |
| `SSAF` | ANC, noise gate, Superhuman Hearing | `0x7xx` |
| `SMic` | microphone | `0x6xx` |
| `SAQG` | EQ bands and preset | `0x12xx` |
| `SBtn` | button assignments | `0xbxx` |
| `SEnc` | dial assignments | `0xaxx` |

**A request verb is always four characters.** `SBT`, `SEQ` and other three-letter forms get no reply at all, which is why Bluetooth has no reader despite being a real category. Twenty further guesses (`SLED`, `SMIX`, `SVOL`, `SBLE`, `SPWR` and others) all returned nothing.

**Corrected later in the session.** Two of the three ranges once listed here as unreadable do have readers, and the reason we missed them was our own parser, not the device:

| Verb | Returns | Range |
|---|---|---|
| `RBT` | Bluetooth | `0x3xx` |
| `S3DT` | game/chat mix — the chat wheel reports here | `0x5xx` |
| `SAQM` | microphone equaliser | `0x13xx` |
| `SCEC` | preset counts and delete registers | `0x16xx` |
| `SVer` | protocol version | — |
| `SInf` | identity and firmware | `0x1xx` |

Note `RBT`, which breaks the "S" + category rule and is why every `SBT*` guess drew a blank. And `3DT` contains a digit: an event pattern here that only matched letters silently discarded every chat-mix event, and we reported the silence as a finding. See *Risks*.

**Only lighting `0x4xx` is genuinely write-only.** The headset never reports brightness back, so an app can only show what it last set.

Reading every category back is how the settings list grew from 27 to 40. Twelve of the new ones are confirmed present and readable but not yet matched to a control.

**Not present in Swarm II for this headset:** voice-prompt volume, and any lighting control beyond the two brightness values. The firmware supports more than the app exposes.

The general-state read returns a block including the device name — currently `My Headset`.

The EQ read returns full band values *and* the preset name, e.g. `Vocal Boost`, `De Mud`.

### Proven write

We replayed a byte-exact ANC-level command. The device accepted it and Swarm II's own slider jumped to match. **Read and write are both confirmed.**

### Physical controls — measured

This is better news than expected for remapping.

**The buttons send nothing to Windows.** Pressing the ANC button produces no HID keystroke at all — only a state-change notification on the vendor channel. There is no default Windows action to intercept or suppress. An app simply sees the notification and can do whatever it likes.

**The dial is the exception.** It sends standard media keys on the consumer-control collection: `0C 01` turn up, `0C 02` turn down, `0C 00` release. Windows acts on these itself, so repurposing the dial means either suppressing them with a low-level keyboard hook (routine) or changing its on-device function via `0xA20`.

The dial also reports its resulting volume back as `0x2A0`, so absolute position is readable.

### The volume wheel, the overlay, and Swarm's "Master Volume" toggle

Three questions that turned out to be one answer.

**The volume wheel is hardware, not software.** It sends native Windows
volume keys on the consumer-control collection. With Swarm II *and* this
app closed, turning it still pops the Windows volume overlay. There is no
headset command that suppresses it, and none could be: nothing on the PC
side is in the loop. A global input hook could swallow the keys, but it
cannot tell the headset's volume keys from a keyboard's, so it would
swallow all of them. Per-device filtering would need a signed HID filter
driver. **So: overlay suppression is not available, at any reasonable
cost.**

**Swarm's "Audio Master Volume" toggle is a mute.** Not a setting about
who may change the volume — a mute. Turtle Beach's own documentation says
so outright: toggling it off "will mute all incoming audio to the headset
from the transmitter". Two USB captures show it sending *nothing* to the
headset, and Swarm's own binaries confirm where it does land: its system
media component drives the Windows endpoint (`MMDeviceEnumerator`,
`GetDefaultAudioEndpoint`, `SetMasterVolume`), and a build-time note inside
the device plugin reads "Master volume and mic use system data". Swarm's
master volume slider and its toggle are the Windows endpoint's volume and
mute, nothing more.

**Which explains the apparent bug.** Toggle Master Volume off in Swarm, use
the volume wheel, and the toggle springs back on by itself. That is not
Swarm misbehaving. The wheel sends a native volume-up; **Windows clears
mute whenever it sees a volume-up**; Swarm redraws the toggle to match the
endpoint it is showing. Reproduced here directly: mute the headset
endpoint, synthesise one `VK_VOLUME_UP`, and the endpoint comes back
unmuted with the level two points higher.

The consequence for this app was a toggle of our own — "Let this app change
Windows volume" — written on the assumption that Swarm gated the same
behaviour. It never did. That preference is gone; the master slider always
drives the Windows endpoint, as Swarm's always did, and a mute switch sits
beside it doing what Swarm's toggle actually does.

One bug fell out of building it. Core Audio answers **S_FALSE** (1, a
success code) when a set would change nothing — muting what is already
muted, or writing the level it already holds. The volume code treated any
non-zero result as a failure, so a harmless no-op raised "write failed
(0x1)". Only a negative HRESULT is an error.

### On-device function options

Swarm exposes these assignment lists. Indices are zero-based, confirmed for the Mode button: before it was remapped, pressing it toggled ANC, and its value at that time was `0`.

**Mode button — `0xB20`**

| Value | Function |
|---|---|
| 0 | Active Noise Cancellation on/off *(default)* |
| 1 | Cycle game presets |
| 2 | Noise gate on/off |

**Dial — `0xA20`** (this is the dial's assignable function; volume remains its primary role)

| Value | Function |
|---|---|
| 0 | Mic monitoring volume |
| 1 | Game and chat mix |
| 2 | Bass boost level |
| 3 | Treble boost level |
| 4 | Noise gate volume |

Values 1 and 2 were observed directly; the rest follow the list order Swarm displays.

Note that bass boost, treble boost and game/chat mix appear here as *hardware* functions while also existing in the Waves layer. These effects exist in both places — the on-device versions serve console and Bluetooth use, where no PC software is running.

### Equaliser presets

Captured while saving a preset by hand in Swarm II, both "to headset" and "to software".

**Saving to the headset is three writes, not a command.** There is no save verb. Swarm writes a slot id to `0x1210`, then the ten band values `0x1220`–`0x12B0`, then the name to `0x12C0`. Writing the name is what commits it. The microphone equaliser is identical one block along: `0x1310`, `0x1320`–`0x13B0`, `0x13C0`.

Slot ids are **not** a sequential index. Factory presets occupy the low numbers and custom slots start at **16**. Confirmed by reading every slot back:

| Bank | Factory | Custom |
|---|---|---|
| Game | 1 Signature Sound · 2 Bass Boost · 3 Bass & Treble Boost · 4 Vocal Boost | from 16 |
| Microphone | 1 Signature Sound · 2 Full · 3 Clarity · 4 Smooth | from 16 |

**Saving "to software" sends nothing to the headset at all.** A preset saved that way appears nowhere in the USB capture, which is why those presets never show up in the phone app.

**Listing them takes ten reads and makes no sound.** Each custom slot is its own category with its own reader: `SCG1`–`SCG5` for the game bank at keys 0x1700, 0x1720 … 0x1780, and `SCM1`–`SCM5` for the microphone at 0x1800 … 0x1880. Each answers with that slot's name and all ten bands in a single reply:

```
{"OR":"CG1","KVP":{"1700":{"name":"De Mud","bands":["30","20","-30", ...]}}}
```

Note the value is an **object**, the only place in this protocol where a KVP value is not a string.

There are exactly **five custom slots per bank**, ids 16–20. Swarm II sends precisely these ten reads and keeps no list of its own; the only presets it stores locally are the ones saved "to software", in `%APPDATA%\Turtle Beach\Swarm II\Setting\STEALTH_PRO_II.ini`.

**This was wrong for most of a day.** These verbs were recorded as "sent five times, never answered". They answer every time. Our event parser matched the end of an event with a non-greedy `\{.*?\}`, which cut the nested object in half; the JSON then failed to parse and the reply was dropped on the floor. The strict pattern had *matched*, so the unrecognised-fragment guard counted it as understood and said nothing.

Three separate findings have now been manufactured by this one class of bug — a pattern that only matches what we already expect, applied to data whose shape we are still learning. First a letters-only category name hid every `3DT` chat-mix event; then it truncated `SCG1`..`SCG5` to five identical `SCG`s; then this. The parser now finds the end of an event by counting braces, and the guard only counts a span as recognised once it has actually produced an event.

What broke the deadlock was reading Swarm's own profile store rather than more captures. `STEALTH_PRO_II_PROFILE_Mgr.dat` is a Qt-serialised snapshot of every category and key Swarm knows about, and it lists `CG1`–`CG5` and `CM1`–`CM5` as categories in their own right. That is what said the slots were readable at all.

**The factory presets are not in those slots** and nothing reads them; they are fixed in firmware and Swarm has them built in too. Ours are measured from the headset rather than copied, and are corrected from the device whenever one is selected.

| Bank | Factory presets |
|---|---|
| Game | 1 Signature Sound, 2 Bass Boost, 3 Bass & Treble Boost, 4 Vocal Boost |
| Microphone | 1 Signature Sound, 2 Full, 3 Clarity, 4 Smooth |

**Saving always creates a new preset.** The slot id written to 0x1210 is only a hint: ask for slot 19 and the headset puts it in the lowest free slot instead, and asking for an occupied slot gets you a second preset rather than an overwrite. Replacing a preset therefore means delete, then save.

**Deleting is by name, and it lives in a different block.** Writing a preset's *name* to `0x1610` removes it. There is no delete-by-slot and no confirmation step. Confirmed on hardware: the count at `0x1600` drops by one, the slot stops answering when selected, and the freed slot is reused by the next save.

Reading `0x1610` back gives the name of the last preset deleted, which persists — that is why the capture first looked like a name mirror of `0x12c0`.

This resolves the whole CEC block, previously six unidentified values. It is three matching pairs of *count* and *delete register*:

| Count | Delete | Bank |
|---|---|---|
| `0x1600` | `0x1610` | game equaliser — confirmed both ways |
| `0x1620` | `0x1630` | microphone equaliser — same shape, no custom presets existed to delete |
| `0x1640` | `0x1650` | unidentified third bank, reads 1; the Bluetooth equaliser is the likely candidate |

The count is useful beyond deleting: comparing it to our remembered list is how the app can tell a preset was added or removed behind its back, by Swarm II or the phone app, and offer a rescan rather than showing a list it knows is wrong.

Deleting the preset you are currently listening to works, but leaves the headset pointing at an empty slot. Swarm selects a factory preset first; so do we.

### The transmitters, and why lighting is not write-only

The headset keeps a slot for each of the four transmitters it can pair
with, one category each: `STX1`–`STX4` at 0x400, 0x420, 0x440, 0x460. Each
answers with two arrays:

```
{"OR":"TX1","KVP":{"400":{"info":["2","17","1","100","1","10F5","229B",
                                  "4.107.703.0","AA:BB:CC:DD:EE:FF"],
                          "control":["0","100","100"]}}}
```

Confirmed in `info`: the first field is presence (2 on the live one, 0 on
an empty slot), then the USB vendor and product ids, the firmware version
and the MAC address. Empty slots answer with zeros and a blank address.
The remaining fields are unidentified and left alone.

Confirmed in `control` by experiment: the second and third entries are the
two lighting brightnesses.

**And they are per slot, which matters more than it sounds.** `0x401` and
`0x402` are not "the LEDs" — they are *slot one's* LEDs. Each slot is a block
at 0x400, 0x420, 0x440, 0x460, and a slot's two brightnesses sit at +1 and +2
inside its own block. Measured with the dongle in slot one and the dock in
slot two:

| write | what moved |
|---|---|
| `0x401`, `0x402` | the **dongle's** control values; the dock untouched |
| `0x421`, `0x422` | the **dock's** control values; the dongle untouched |

The dock's status ring visibly changed on the second one, confirmed by eye —
the slot data agreeing is not by itself proof that anything lit up.

So anything reading the *active* transmitter's brightnesses and writing
`0x401`/`0x402` is reading one device and writing another, which is exactly
what the app did until this was found.

**That overturns an earlier finding.** Lighting was recorded as write-only,
because 0x4xx had no reader. It does — the whole range is the transmitter
slots, and the brightnesses sit inside the first one. Same cause as the
preset slots: the reply nests an object, and the parser was dropping it.

That is now three findings produced by one parser bug — "the chat wheel
emits nothing", "the preset slots never answer", and "lighting is
write-only" — all of them confident, all of them wrong. See *Risks*.

---

## Headset vs PC: where each setting lives

**On the headset** — reachable through the channel above, and persists with Swarm II closed or uninstalled *(research confirms settings survive uninstall)*:

ANC on/off and level · all EQ (five custom game slots, five Bluetooth slots, two mic EQ banks) · Superhuman Hearing · mic monitoring, mic volume, noise gate, AI noise reduction · voice-prompt volume · auto shut-off · wake-on-motion · dial and button remapping · LED brightness · five on-board profile slots

**On the PC**, handled by the bundled Waves component:

Waves 3D spatial · Dolby Atmos (a separate paid Microsoft Store product) · and, per research, game/chat mix and chat boost.

### Game/chat mix: settled

Both earlier readings were partly wrong. The resolution comes from the transmitter's own USB descriptor, which is definitive:

**The transmitter accepts exactly one stereo audio stream.** One playback endpoint, two channels, no second channel pair and no alternate multichannel mode.

So on PC the headset receives audio already mixed. It physically cannot separate game from chat, and the `0x510` mix value has nothing to act on. The split you get today exists only because the Waves component creates a second audio device on the PC and mixes the two before sending one stream down.

`0x510` is a real on-device setting, but it only means something where two streams genuinely arrive: on Xbox, and when Bluetooth audio runs alongside the 2.4GHz link. **If chat is on a phone over Bluetooth, the headset does the mixing and `0x510` controls it directly.**

**Consequence for the app.** A PC game/chat mix needs a second audio endpoint on the PC. Three ways:

1. Call the bundled Waves component — already installed, exposes the mix directly. Zero setup, but requires the Turtle Beach audio driver.
2. Require a free third-party virtual audio cable and mix yourself.
3. Write and sign your own virtual audio driver. Expensive and slow.

This is the one objective that cannot be met purely by talking to the headset — it needs a second PC endpoint. But it does **not** need Waves.

### Confirmed: a full game/chat crossfade with no Waves at all *(validated on hardware, 22 Sep 2026)*

Option 2 is built and proven end to end in the real end-user configuration: **the Turtle Beach / Waves audio driver fully uninstalled**, a free virtual cable (VB-CABLE) providing the second endpoint, and our own engine doing the mix.

- Chat plays to the virtual cable; the engine loopback-captures it and renders it into the headset as its own audio session, with the chat half of the crossfade as its gain.
- Game audio plays natively to the headset and is attenuated on the game side.
- Moving the slider (or the physical chat wheel) trades between them — a true crossfade, confirmed by ear with Waves absent.

This is the answer for the Xbox-edition owners whose Waves chat mix does not work: they do not need Waves at all. Uninstall it, install a virtual cable, run this.

**On packaging the virtual cable.** VB-CABLE is donationware and its licence requires a separate agreement to bundle or redistribute (VB-Audio's threshold is >10 units). So the app should **link** to the official download, not ship it. Better still, the engine captures *any* non-headset output, so the setup guide offers two paths: use a spare output you already have (zero install), or install VB-CABLE for a dedicated silent chat sink (recommended). Either keeps the distribution driver-free and clean.

### 24-bit is packed, and getting that wrong looked like four bugs

Corrects an earlier entry in this file, which blamed the headset.

**What Windows stores** for an endpoint's default format is a
WAVEFORMATEXTENSIBLE, and for 24-bit it is *packed*: `wBitsPerSample` 24,
`nBlockAlign` 6 for stereo. Read straight off this headset at
24-bit/96kHz, which is the only reason we finally saw it. The app was
building 24-bit the other common way — three bytes padded into a
four-byte container, 32/8 — which the device rejects.

One wrong field, four symptoms that looked unrelated:

1. **The list of choices never offered 24-bit.** `IsFormatSupported` was
   being asked about the padded form and said no every time — on a device
   that was running at 24-bit while being asked. This was written up here
   as "the probe omits formats that are plainly in use". It does not.
2. **Writing 24-bit installed a format the device would not open.** The
   mix engine then failed with "the audio format is not supported by the
   audio endpoint device" — which names no device and no format, so it
   read as the headset being broken.
3. **The failure looked intermittent.** Setting 16-bit worked, because for
   16-bit the container and the depth are the same number, so the app got
   it right by accident. Every recovery that happened to pass through a
   16-bit format "fixed" it.
4. **Reading hid all of it.** A padded format still reports 24 valid bits,
   so the app agreed with Windows about what the device was set to while
   being unable to set it.

Asked in exclusive mode with the packed container, the probe now returns
exactly the four entries Windows itself offers for this headset: 16- and
24-bit at 48kHz and 96kHz.

**The lesson worth keeping** is not about audio. A value that reads back
correctly is not proof that you wrote it correctly, and a bug in a shared
helper shows up as several unrelated faults in everything that uses it —
here, a probe that under-reported, a write that broke the device, and an
intermittency that was really just which code path had been taken.

Two things were built on the wrong diagnosis and kept anyway, because they
earn their place either way: the app can now find a working format by
trying real starts rather than trusting any probe, and it says which of
the two it is doing rather than failing silently.

### High-bandwidth audio is the Windows format, and nothing else

The transmitter's purple LED means the 2.4GHz link is running high
bandwidth. On the Charging Hub it is **LED 2**, the dock-status ring —
not LED 1, the battery-eject ring, which is what gets watched first and
never changes. Confirmed on hardware: set 24-bit/96kHz with the mix engine
running on it and the ring goes purple and stays purple.

**Nothing is sent to the hardware.** Setting the endpoint to 24-bit/96kHz
in Windows is the whole mechanism: all 101 values read back off the
headset and transmitter were compared before and after a change and only
the link RSSI moved, which is noise. Swarm stores no high-bandwidth
setting either — its saved state has no such key in any category. There
is no command we have failed to find; the app already does everything
there is to do.

**Writing the property store is not how you change a device format.**
This is the finding, and it took three wrong answers to get to.

`PKEY_AudioEngine_DeviceFormat` is where Windows *stores* the setting.
Write it and commit it and everything looks right: the value persists, the
Sound dialog reads it back, our app reads it back. Nothing happens. The
endpoint carries on at the old rate, no stream is interrupted, and the
transmitter never switches into high bandwidth. The setting is changed and
the device is not.

What the Sound control panel actually does is ask the audio service to
adopt the format, through **IPolicyConfig::SetDeviceFormat** — undocumented,
stable since Windows 7, CLSID `{870AF99C-...}`, interface
`{F8679F50-...}`, vtable slot 6, addressed by endpoint id from
`IMMDevice::GetId`. That call is what stops the streams, moves the engine
and makes the change real.

**And it only works on a live endpoint.** With nothing playing there is
nothing to reconfigure, so it records the new default and stops there; the
next client to open the device then pins whatever the service was last
actually running. An earlier attempt at this closed the mix engine first,
on the reasonable-sounding principle that you let go of a device before
reconfiguring it. That is precisely what broke it. Leave the stream
playing and the service moves the endpoint for real.

The check that settles it is comparing the stored default against
`IAudioClient::GetMixFormat`. When those disagree, the setting is a
decoration. Both now follow every change, in both directions, with the mix
engine staying up — confirmed on hardware: the transmitter ring goes purple
on 96kHz and audio cuts and returns on every switch, the same as using the
Sound dialog.

**Three wrong diagnoses preceded this**, all of them blaming the headset:
that it intermittently refused its own format (it was 24-bit written
padded instead of packed), that it would not drop out of high bandwidth
(it was reopening the device mid-change), and that closing the engine
first was the fix (it was the opposite). Every one looked intermittent,
and every time the intermittency was which of our code paths had run.
All three were caught from the outside, by noticing that Windows' own
dialog did the same job without trouble.

The recovery path built while the diagnosis was still wrong is kept. An
endpoint left on a format nothing can open is a real state — we produced
it ourselves for half an hour, and a crash mid-change could produce it
again — but it is no longer the explanation for anything we have seen.

### The chat mix may not need a virtual cable at all

Two measurements, and the second one only happened because the first said no.

**Process loopback taps the stream after session volume.** The idea was to
capture the chat application by process id, silence its own session so it is
not heard twice, and render our copy with the chat gain — which removes any
need for a virtual cable. Measured with NAudio's `WithProcessLoopback` against
a process that was playing:

```
session at its own level     peak 0.4450
session volume 0             peak 0.0000
```

So silencing the application silences our capture with it. Capture-and-silence
is dead.

**But that answer contains a better design.** If session volume attenuates
what an application produces, session volume *is* a working per-app gain, and
a crossfade needs nothing more than that. Name the chat application, give its
sessions the chat half of the crossfade and everything else the game half, and
both keep playing natively to the headset. No cable, no capture, nothing in
the audio path.

Measured at the endpoint, which is what actually reaches the headset:

```
mix 50  (game x1.00)    peak 0.6471
mix 100 (game x0.00)    peak 0.0000
```

**Confirmed on hardware, twice, with a real Discord call.** The second run is
the better evidence because both sides were producing audio at once, so the
sweep shows them genuinely trading rather than one merely muting:

```
mix  50   chat x1.00  game x1.00   endpoint peak 1.0000   both
mix 100   chat x1.00  game x0.00   endpoint peak 0.8529   Discord alone
mix   0   chat x0.00  game x1.00   endpoint peak 0.6876   Spotify alone
mix  50   chat x1.00  game x1.00   endpoint peak 1.0000   both back
```

At each extreme one source is gone and the other is still clearly there at
its own level. Confirmed by ear both times.

**And it needs no configuration at all.** Discord was on its Windows default
output, which is the headset — the state a person is in before they touch
anything. Checked directly: Discord's active session sits on the headset
endpoint, with only stale inactive registrations left on the cable. Against
the old design, which required pointing the chat application at a cable the
person first had to install, that is the whole difference. The mechanism is not new — it is exactly how the
game side already works, and had already been confirmed by ear there. The only change is identifying chat by application
rather than by device, which is also a better question to ask somebody: "which
app is your chat?" beats "install this driver and reconfigure Discord".

**What it would delete:** the loopback capture, the render path, the two-clocks
drift trimming, the restart supervision, and the whole class of "the engine
died so chat went silent" failures. **What it must keep:** the volume journal.
We would still be holding other applications down, and that still needs undoing
if we die.

**The prototype proved that last point the hard way.** It re-read the current
volumes as "the originals" on every invocation, so the second run recorded 0 as
an application's original and multiplied from there — leaving it silent. That
is the ratcheting the journal exists to prevent, reproduced in three commands.
Losing the cable does not let us lose the state ownership.

**The one real loss against the cable.** Routing by application means the chat
application has to be its own process. Discord, Teams and Steam desktop clients
are; a call taken *in a browser* is not, because the browser is also carrying
game and media audio and there is no way to tell those sessions apart. The
cable had no such limit — anything could be pointed at it. So VB-CABLE stays
as the answer for browser-based chat rather than being retired outright.

Still open: an application with several sessions where not all of them are
chat (Discord has two and both are chat, so this is untested), what happens
when the chat application is not running yet and so has no session to
attenuate, and sessions that appear after the mix is set — already solved
once in the engine, where the session list must be refreshed rather than
cached.

**The prototype owns its originals now** and journals them before touching
anything. Verified by the sequence that previously left an application
silent: apply 100, apply 100 again, return to 50 — endpoint peak 0.3888,
0.0000, then 0.6535. It comes back.

### No open-source virtual cable avoids the signing problem

Worth recording so it is not re-litigated. Open source solves the *licence*
problem, not the *signing* problem, and only the second one is expensive.

Any virtual audio device on Windows is a kernel driver, and a kernel driver
needs a real signature to load on 64-bit Windows. MIT licensing the source
changes nothing: forking Virtual-Audio-Driver or virtual-audio-wire still means
an EV certificate and Microsoft attestation signing to ship it.

The USB/IP route is sometimes suggested as a way around this, on the grounds
that Windows attaches its own signed `usbaudio.sys` to a virtual USB device.
It does — but presenting the device at all needs a virtual bus driver, and
usbip-win's own install instructions require importing a test certificate and
running `bcdedit /set TESTSIGNING ON` with a reboot. Test signing breaks Easy
Anti-Cheat and Vanguard, which on a gaming headset application is the worst
possible failure.

VB-CABLE's licence only restricts *bundling*. We link to it, so it never
applied. It stays as the fallback.

### If you do want the Waves layer

It is not a black box. It installs as its own component that ships with the headset, separate from Swarm II, and exposes a plain callable interface covering: game/chat mix, chat boost, Superhuman Hearing, Waves 3D, output EQ, mic EQ, bass and treble boost, noise gate. Swarm II is a thin wrapper over it.

Dependency if you use it: that audio component must stay installed. Swarm II itself can go.

Unadvertised bonus: the same component ships webcam-based head tracking for spatial audio that Swarm never exposes.

---

## Why the interface felt slow

Two causes, neither of them the headset, and the larger one was not doing
work at all.

**Nagle and delayed ACK, on every other request.** Requests alternated
between about 5ms and about 300ms. 300ms is not a number that comes from
doing work — it is a delayed-acknowledgement timer. Python's HTTP server
writes the headers and the body as separate unbuffered writes, so with
Nagle enabled the second write waits for the first to be acknowledged.
Every endpoint measured under a millisecond from a raw socket and the
interface still crawled, which is what pointed at the transport rather
than the code. `disable_nagle_algorithm = True` and
`protocol_version = "HTTP/1.1"` on the handler: everything dropped to
1-4ms. The HTTP/1.1 half matters too — without it the connection closed
after each response and the interface opened a fresh socket four times a
second.

**A full read after every action.** A full settings read is twelve
categories, each about 100ms of the headset's own reply latency, so 1.2
seconds. The interface was doing one after every action, which is why
switching presets in particular felt broken.

Two things were tried and rejected, both measured first:

- *Asking for all twelve categories at once* and listening in a single
  window. The headset drops requests sent back to back; the read came
  home with roughly half the values missing, and no faster. Reverted.
- *Reading the ten preset slots and four transmitter slots as part of it.*
  That was already happening and was pure waste: they are inventory with
  their own readers and caches, and an empty slot never answers so it
  costs the full window. Removing them took a full read from 2.6s to
  1.2s, which is the floor.

So a full read is now for startup and for an explicit Refresh only.
Actions paint what they already know — applying a preset means the stored
curve, its name and its id are all in hand — and the standing 250ms poll
confirms it from the device afterwards. Preset switching went from over a
second to painting inside 60ms.

## A design opportunity

Swarm II polls the headset roughly **2,700 times per second**, almost always getting an empty reply. That is the busy-wait of a poorly written client and very likely why people find Swarm heavy.

A replacement can poll a few times a second and be invisible by comparison. That is a real, demonstrable user benefit, not just a nicer interface.

---

## Prior art *(research)*

- **HeadsetControl**, the main open-source headset project, supports **zero** Turtle Beach devices. Nothing to extend there — but it does ship a raw-HID developer mode and documented steps for adding a device.
- A single-author repo published in August 2026 documents the same transport for the **Stealth 600 Gen 3** — same vendor ID, same usage page, same report IDs, same framing. Independent confirmation that what we captured is the general mechanism.
- Two open-source toolkits implement the underlying Airoha RACE protocol generically.
- Turtle Beach ships the firmware parameter dictionary as **plain XML** — 653 named parameters for the headset, 361 for the transmitter, including every ANC, EQ, sidetone and lighting setting. Most reverse-engineering projects never get documentation this good.

Note: the parameter dictionary (`NVID_*` names) and the runtime keys (`0x750` etc.) are **two different namespaces**. The dictionary describes firmware storage; the runtime keys are Turtle Beach's own abstraction on top. Don't assume the numbers correspond.

---

## Risks

| Risk | Severity | Notes |
|---|---|---|
| Bricking via firmware writes | High if touched | Firmware images are signed and encrypted — stay out of the update path entirely |
| Bricking via the LED behaviour table | Moderate | ~500 bytes of undocumented structure per device; decode by observation, never by guessing |
| Vendor update breaks the app | Moderate | Industry precedent exists |
| Warranty | Low | Excludes damage from unauthorised modification |

---

## Open questions

1. What are the remaining unidentified values? They read back cleanly but have not been matched to controls. Naming them means capturing Swarm while the matching control is operated — no writes needed. Four of the CEC block's six resolved as the preset count and delete registers; nineteen values are still unnamed.
2. What is the structure of the ~500-byte LED behaviour table? Swarm never touches it and lighting has no read verb, so neither observation nor reading will get at it.
3. What is the tag byte at frame offset 21?
4. Can Bluetooth be switched from software at all? There is no reader for the Bluetooth range and no writable key found, so this is currently unanswered.

**Answered:** read verbs exist for six of the nine ranges — see *Reading settings back*.

## Firmware settings, underneath Turtle Beach's protocol *(confirmed)*

Below Turtle Beach's key/value layer sits the chip vendor's own protocol,
on the same HID channel, and it reaches the firmware's stored settings:

```
read   05 5A 06 00  00 0A  <key:2 LE>  <length:2 LE>
write  05 5A <len:2 LE>  01 0A  <key:2 LE>  <data...>
reply  05 5B <len:2 LE>  <cmd:2 LE>  <status or length...>
```

Frames over 59 bytes split across HID reports, each with its own length.
Confirmed on hardware: reads return real data, writes return status 0,
read back byte-for-byte, and survive a power cycle.

The full dictionary of 653 firmware settings ships with Swarm II, as
plain XML under `Data/Firmware/STEALTH_PRO_II/<vid_pid>/<version>/`, with
every factory default — which is what makes writing recoverable.

### These commands do NOT reach the headset through the transmitter

**This cost us most of a session.** Turtle Beach's own commands tunnel
through the transmitter to the headset. These do not: they address the
USB device you are plugged into. With only the dongle connected you are
reading and writing *the dongle's* settings, and it answers happily, so
nothing looks wrong.

Connect the headset itself by USB-C, powered on, and it becomes the
control device — then the commands reach the headset. There is no relay
command; the published toolkits for this chip family implement none.

Tell them apart by size: the same key holds 507 bytes on the transmitter
and 529 on the headset. `tools/ledtable.py` refuses to write unless you
name the target and it matches.

### Firmware storage is an output, not an input *(confirmed — and it invalidates the LED result below)*

This is the important finding, and it was arrived at last, after a day
of drawing conclusions from writes that could never have worked.

**The firmware writes its settings out to storage. It does not read them
back to decide how to behave.**

Measured both directions on the headset's master earcup:

| Test | Result |
|---|---|
| Change ANC intensity the normal way (0 → 55 → 20) | storage followed within two seconds, exactly, both times |
| Write `NVID_TB_USR_WAKE_ON_MOTION` directly to 1 | device kept reporting 0, **including after a power cycle** |

So storage is a mirror the firmware publishes, not configuration it
consumes. Reads of it are trustworthy and useful — they agree with the
live device for every setting cross-checked. Writes to it persist, read
back cleanly, survive reboots, and change nothing.

That also explains the published Stealth 600 fix, which writes one key
and works: the value it sets is read by *Swarm II*, not acted on by the
firmware.

**What this means for anyone trying to change device behaviour this
way: it will not work, and it will look like it should.** The write is
accepted, the value reads back, it survives a power cycle. Every signal
says success except the device's actual behaviour.

### The LED behaviour table — result withdrawn

`NVID_APP_LED_PATTERN` (0xf283) is a header byte then 11-byte records,
one per LED state — 48 on the headset, 46 on the transmitter — with
plausible timing and brightness fields, twenty of them blank. Searching
all 653 firmware settings turns up nothing else lighting-related.

Two tests were run on it, and **both are void** in light of the finding
above:

| Test | Result | Why it proves nothing |
|---|---|---|
| All 48 records blanked | every light normal | writes to storage do not reach the firmware |
| Bytes 2 and 3 set to 1 everywhere | every light normal | same |

An earlier version of this document reported these as evidence the table
is unused. That conclusion was not supported: we were measuring whether
writes take effect, and they never do for anything.

**So whether the lighting can be changed is unknown, not settled.** The
table may well be live. There is simply no route to changing it from the
host with what is known: the firmware does not consume this storage, and
the only other path is the signed firmware image.

### Changing LED behaviour: every host route checked *(closed)*

The specific goal was to stop the power light blinking when the headset
is on but cannot reach its transmitter (e.g. PC off, headset on
Bluetooth). After the storage finding above, four further avenues were
checked — all of them read-only or the same runtime channel we use for
everything else, none touching the firmware image. Every one came back
empty.

| Avenue | Method | Result |
|---|---|---|
| Does Swarm expose any LED *behaviour* command? | captured Swarm's USB traffic while every lighting control was operated | **only** `set_kvp` for `0x401`/`0x402` — brightness. No behaviour command in Swarm's vocabulary at all |
| Does Swarm control the *headset's* lighting? | inspected Swarm's lighting UI | no — its only lighting section is the Charging Hub's two LED brightnesses (labelled LED 1 / LED 2). There is no headset-lighting control to capture |
| Is there a readable LED-behaviour category? | full category sweep, both via the transmitter and against the headset directly over USB-C | identical maps, 26 categories, none LED-related. `0x4xx` is the transmitter slots, not headset LEDs |
| Is there an unadvertised behaviour verb? | probed a dozen LED/pattern/effect/mode read verbs | all silent |

Two things worth recording from that work:

- **The headset addressed directly over USB-C (AB1577) exposes exactly
  the same categories as the transmitter relay.** Connecting it directly
  buys access to the raw firmware layer (NV / flash / RAM), but nothing
  new at the Turtle Beach key/value layer. There is no headset-only LED
  control hiding behind the relay.
- **The two Charging Hub sliders are just "LED 1" and "LED 2"** on the
  dock; the "Status" and "Voice Prompt" rows beside them are a live
  indicator and a (here non-editable) dropdown, not the sliders. The
  app's earlier "Charging hub / Transmitter" labels were wrong and are
  corrected.

### Re-examined after the per-slot discovery *(conclusion unchanged)*

Finding that lighting is addressed per transmitter slot raised a fair question:
was the LED-behaviour work defeated by the same wrong-device trap? Two checks
say no.

**Nothing is being dropped from any reply.** Every readable category was
printed as raw JSON rather than as the flat keys we normally take. The only
structured replies in the whole device are the ten preset slots and the four
transmitter slots, both of which we already unpack. That matters because three
findings on this project were once wrong for exactly that reason.

**`control[0]` is not lighting — and writing it is not safe.** It was the one
unidentified field in a transmitter slot and the obvious candidate for a mode.
It is writable: set to 1, then 2, it read back as 1 and 2, and no light changed
at either value. That was written up as "no effect". **That write-up was
wrong, and it cost a working headset.**

Minutes later the headset was selected onto a transmitter nobody had chosen,
and then talking to neither. `control[0]` takes exactly the alphabet `info[0]`
uses — 0 empty, 1 paired, 2 selected — and `control[0] = 2` was written to the
slot that then became selected. Restoring it to 0 did not put the selection
back.

Not proven, and it should not be proven by writing it again on the only
headset here. **Treat any slot base address (0x400, 0x420, 0x440, 0x460) as
unwritable.** The two brightnesses at +1 and +2 remain safe and are confirmed.

The lesson is the one this project keeps paying for, one turn later than it
should have been learned: watching one output and seeing nothing is not the
same as nothing happening. "No light changed" was the honest observation.
"No effect" was not.

**What has not changed:** the per-slot discovery lives entirely inside 0x4xx,
which is the transmitter slots. The headset's own power LED is not in that
range, and the blocker was never addressing — the firmware does not read back
what it writes out.

**One limit worth stating plainly.** Every sweep only finds keys that appear in
replies. A write-only key with no reader is invisible to all of this, and this
project once wrongly concluded lighting *was* write-only, so that cuts both
ways. But with no Swarm command to capture and nothing documented, finding one
means blind-probing a 16-bit space. "Not changeable by any means found" is the
honest claim; "impossible" is not.

**Conclusion.** The blink is firmware logic reacting to a lost 2.4GHz
link, with no setting it consults and no command that reaches it. It is
not changeable from the host by any means found. The only remaining
route is modifying the signed firmware image, which this project does not
do — it defeats the code-signing and the flash path can brick the headset
through the transmitter.

**Practical alternatives for an owner:** a physical cover over the LED,
or a low auto-shut-off so the headset powers down instead of sitting and
searching (auto-shut-off *is* host-controllable). Or ask Turtle Beach —
the twenty already-blank records in the LED table suggest the firmware
was built for configurability that was never wired to a control.

### Not possible

- **Changing what the lights do.** Not achievable from the host: the firmware does not read the settings storage that holds the LED table, so writes to it are accepted and ignored. Brightness is adjustable through the normal protocol; behaviour is not reachable at all.
- **Repurposing the Bluetooth button.** It drives the headset's own Bluetooth radio, which the transmitter never sees, and it is not in Swarm's remap list. Pressing it produces nothing on the PC at all, short or long. Only the Mode button and the lower left dial are remappable.

None of these blocks a first working version.

---

## Next steps

1. Build a minimal command library and a first interface over the settings already mapped — that is most of the app.
2. Decide the game/chat mix approach: call the bundled Waves layer, or leave the feature out of v1.
3. Probe for a read verb, then read the LED table back.

Build order by cost: buttons and audio settings first, lighting brightness second, LED behaviour patterns last.

---

## Reproducing the evidence

Capture used USBPcap 1.5.4.0 (installed via winget from the official release, hash-verified). The capture driver attaches to USB only after a reboot.

```
USBPcapCMD.exe -d \\.\USBPcap<n> -o out.pcap --devices <addr> --inject-descriptors
```

Requires admin. Output path must be short — the tool fails silently past the Windows path limit.

Tooling kept in `tools/`:

- `analyze.py` — decodes a capture into the command and notification tables
- `send.ps1` — sends a raw frame to the headset's control channel
- `hidprobe.ps1` — lists the headset's HID collections and checks access rights

Decoded capture output is in `evidence/`.
