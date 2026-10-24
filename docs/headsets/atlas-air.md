# Atlas Air

Not in 0.3.0: Neap does not know this headset while `HeadsetModels.AtlasAirReleased`
is off (DECISIONS.md, 23 October 2026). In testing. On Windows it connects and most settings work. On Linux 0.2.1.8
connects after the transmitter is unplugged and plugged back in, then drops
every 24 seconds; main fixes the drops and asks for the replug. SP2Tools has read
the headset's firmware for every open question (F114 to F126); what it found
is in 0.2.1.7 for the testers to confirm.

- **Hardware:** USB Transmitter `225E`; the headset reports itself as `2260`;
  firmware 6.163.1646.0. No dock.
- **Turtle Beach's feature list:**
  [Atlas Air – Swarm II App (Desktop Version)](https://support.turtlebeach.com/s/?language=en_US&co=en_US#/331/Atlas-Air/1/PC/article/a32UR0000025kijYAA),
  updated 20 March 2026.
- **Testers:** one on Windows, one on Linux (CachyOS), on
  [issue 1](https://github.com/lykosapps/Neap/issues/1).
- **Latest test build:** 0.2.1.9, published 24 October 2026: main with the
  Atlas Air turned on, in one commit under the tag.
- **SP2Tools:** the questions and answers are in its
  `knowledge/iceman-neap-asks.md`, the findings in
  `side-session-notes/iceman-re/`. All are read from the firmware; none has
  been tried on an Atlas Air.

## Connection

| System | Neap connects | How sure | Next |
|---|---|---|---|
| Windows | Yes, through the USB Transmitter; 0.2.1.7 drops to Not connected every 20 seconds to two minutes | Confirmed; the drops are Neap's | Fixed in main for the next test build: ask the tester to leave it running for ten minutes while recording. |
| Linux | 0.2.1.8: when Neap starts with the PC, or after the transmitter is unplugged and plugged back in, then Not connected for about 5 seconds in every 24 | Seen; the drops are Neap's | Fixed in main for the next test build: the drops (see Problems), and Home asks for the replug when Neap's restart brings nothing back. Ask the tester to start it with the headset on and leave it ten minutes while recording. |

## Features

### Audio

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Master volume, on/off | Listed | Yes | Has | Seen | Blocked | Set through the system's output | `0x2A0` has no off flag of its own; a 0 there is the PC's mute echoed (F120, Read). |
| Master volume, level | Listed | Yes | Has | Confirmed | Blocked | Set through the system's output; shown from `0x260` | `0x260` is the wheel, in steps of 6, and the PC's level comes back after it, so a big turn can shake the slider (F120). Rare; left. |
| Superhuman Hearing, on/off | Listed | Yes | Has | Confirmed | Blocked | `0x800` | Heard to work. A change to the equaliser turns it off without saying so (F122); 0.2.1.7 reads it again after every equaliser write. Off after every power cycle. |
| Superhuman Hearing, type (Legacy, Footsteps, Gunshots) | Listed | Yes | Has | Confirmed | Blocked | `0x810`, 0–2 | — |
| Superhuman Hearing, level | Listed | Yes | Has | Confirmed | Blocked | `0x820`, 0–100 | A blend: 0 is unprocessed, 100 all processed (F122, Read). |
| Loudness boost | Not listed | Possibly, by the Stealth Pro II's command (F114, Read) | Hidden | Fails | Blocked | RACE `0x000A`, as on the Stealth Pro II | In 0.2.1.7 Neap set levels from −12 to +6 dB, and the tester heard no difference. Hidden from the next build. |
| Game and chat mix | Listed | Yes | Has | Confirmed | Blocked | Done on the PC | The headset mixes only for an Xbox (F121, Read). Saved in profiles. |
| Chat Boost | Listed | Only on the PC: the headset keeps a flag with no sound stage behind it (F121, Read) | Not planned | — | — | `0x270`, a flag | Not planned. |
| Waves 3D (Games, Music, Movies) | Listed | Only with Neap's own sound processing | Not planned | Read | — | Done on the PC by Turtle Beach's Waves software | Not planned unless Neap does its own sound processing. |
| High Bandwidth Audio | Listed | Yes, through the PC's sound format | Has | Seen | Missing | The PC's sound format: the transmitter offers 48 and 96 kHz | Ask the tester to try 96 kHz. |

### Microphone

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Microphone mute | Listed | Yes | Has | Seen | Blocked | Done on the PC. The headset's own is `0x600`, refused while the boom's switch is low (F120, Read) | No key reports the boom, and which position the switch is low in is not known. 0.2.1.7 also turns monitoring down while Neap has the microphone muted, as the headset's own mute does, and logs the headset's record of its inputs, which shows each contact of the mute switch move (answered on a Stealth Pro II, Seen; the Atlas Air's bits from SP2Tools F126, Read). Ask the tester to flip the boom up and down once while recording. |
| Sensitivity | Listed | Yes | Has | Confirmed | Blocked | Done on the PC; the transmitter copies the PC's level into `0x610` (F120, Read) | The tester says it works (0.2.1.5). |
| Monitoring, on/off | Listed | Yes, at `0x620` (F120, Read) | Hidden | Untried | Blocked | `0x620`, 1 off, 0 on | The level at 0 is off, so no switch is offered. |
| Monitoring, level | Listed | Yes | Has, test | Seen | Blocked | `0x630`, 0–100: −30 dB to 0 dB, 0 off, 20 as shipped (F120, Read) | Every level read back as the gain F120 gives, 100 as 0 dB, the most the headset's own key allows (Seen, 22 October). The tester hears a difference between 0% and 100%, but not his own voice clearly. Ask whether Swarm II at 100% is any louder; see Problems. |
| Noise gate, on/off | Listed | Yes | Has | Confirmed | Blocked | `0x700` | Reported when it changes, but not in the read at start, which asked the Stealth Pro II's group `SAF`, which this headset does not have. The firmware serves it as group `NG` and answers a read of it with the saved values (SP2Tools F125, Read). 0.2.1.7 asks `NG` and no longer asks `SAF`, and the 22 October recording has no `0x700` or `0x710`: `NG` gave no answer. Neap's log missed it, as its check counted the group as answered from Superhuman Hearing's `0x720`; the next build logs each group that gives no answer, with anything sent instead. The firmware serves the read (F125, Read), so the next recording shows whether the request is lost on the way or the reply is something Neap does not take. Until then the switch shows no reading at start; turning it on or off works. |
| Noise gate, level | Listed | Yes | Has | Confirmed | Blocked | `0x710` | Heard clear at 0%, cut at 50%, almost muted at 100%. As the on/off for reading it. |
| High Bandwidth Microphone | Listed | Yes, through the PC's sound format | Has | Seen | Missing | The PC's sound format: the transmitter offers 16 and 48 kHz | As High Bandwidth Audio. |

### Equaliser

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Game equaliser, 4 presets | Listed | Yes | Has | Seen | Blocked | `0x1210` | Encoded as on the Stealth Pro II (F122, Read). Ask the tester to listen. |
| Game equaliser, 10 bands | Listed | Yes | Has | Seen | Blocked | `0x1220`–`0x12B0`, tenths of a dB, ±9 dB | As above. Bands are not kept through a power cycle (F122). |
| Mic equaliser, 4 presets | Listed | Yes | Has | Seen | Blocked | `0x1310` | As above. |
| Mic equaliser, 10 bands | Listed | Yes | Has | Seen | Blocked | `0x1320`–`0x13B0` | As above. |
| Custom presets, save and delete | Listed | Yes | Has | Untried | Blocked | `0x1600`–`0x1630` | Ask the tester to save and delete one. |

### Additional settings

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Battery level | Listed | Yes | Has | Seen | Seen | `0x240` | — |
| Connection and transmitter status | Listed | Yes | Has | Confirmed | Seen | `0x290` | — |
| Drivers status, platform | Listed | — | Not planned | — | — | — | Swarm II's own drivers; nothing for Neap to show. |
| Auto shut-off (never, 5, 10, 20, 30 min) | Listed | Yes | Has | Seen | Blocked | `0x280`, 0 never to 4 thirty minutes, 4 as shipped (F122, Read) | The same values as Neap's. Ask the tester to check one. |
| Battery pop-up warnings | Listed | Yes, on the PC | Missing | — | — | Done on the PC | Decide whether Neap should warn. |
| Transmitter light brightness | Listed | Yes | Has | Confirmed | Blocked | Written at `0x430`; read from the transmitter itself, RACE `0x0006` operation 3 (F116, Read) | Changing it works. 0.2.1.7 reads it at start; ask the tester whether it shows. |
| Voice and tone prompt level | Listed | Yes | Has | Confirmed | Blocked | `0xC00`, 0–15, each step 2 to 3 dB; above 15 is 15 (F120, Read) | Works in 0.2.1.6. |
| Firmware updates | Listed | No | Not planned | — | — | — | Neap never writes this headset's firmware. |

### Swarm II's own settings

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Updates | Listed | Neap's own | Has | Seen | Seen | — | Neap's own; both logs show the check running. |
| Start with the PC | Listed | Neap's own | Has | Untried | Untried | — | Neap's own. |
| Language | Listed | Yes | Not planned | — | — | — | English only, written so it can be translated. |
| Macro manager | Listed | Yes, on the PC | Not planned | — | — | — | Out of scope. |
| Profiles | Listed | Neap's own | Has | Seen | Blocked | — | Neap's own, by the app in use. |

### Physical controls

From the quick start guide. Neap describes them; it doesn't set them.

| Control | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| Volume wheel | Quick start guide | — | Has | Confirmed | Blocked | `0x260` | — |
| Flip-to-mute microphone | Quick start guide | — | Has | Seen | Blocked | `0x600` | See Microphone mute. |
| QuickSwitch (transmitter or Bluetooth) | Quick start guide | — | Has | Seen | Blocked | `0x290` | — |
| Bluetooth button (pairing, calls) | Quick start guide | — | Has | Confirmed | Blocked | `0x290`, signal `0x310` | The tester says Bluetooth shows properly. |

### Shown by Neap, not on the Atlas Air

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|
| AI noise reduction | Not listed | No: the headset doesn't have it | Hidden | Fails | — | `0x630` is mic monitoring here | Not offered. |
| Dial functions | Not listed on PC; the quick start guide has no second wheel | No: its one wheel is the volume, which cannot be given another job | Hidden | — | — | `0xA00`–`0xA30`: reported (Seen), but for a second wheel it does not have; a write is kept and does nothing, `0xA30` is refused (SP2Tools F126, Read) | Not offered. |
| Button function | Not listed on PC; the mobile article lists it | Possibly: one button's short press, as the next game preset, the noise gate or chat boost | Hidden | — | — | `0xB00`–`0xB20`, one for each of PlayStation, Xbox and PC (`0xB20`); 0, 1 or 2 (SP2Tools F126, Read). Which button it is, and whether it is a button at all, needs a test on the headset; 0.2.1.7 logs the headset's record of its inputs, which shows it | Would be a choice of three on the Controls page, writing `0xB20`. Needs the tester's recording to name the button first, then a tester to try the write. |
| Headset name | — | Yes | Has | Untried | Untried | The USB product string of its transmitter, not yet seen; `0x220` holds "My Headset" and is not shown | The transmitter's own name, else "Atlas Air", and "Headset" with nothing connected. A tester's app log names it. |

## Problems

| Problem | Found in | Status | Next |
|---|---|---|---|
| Windows: Not connected every 20 seconds to two minutes, with the headset on and nothing touched | 0.2.1.7 | Fixed in main, not yet seen on the headset. 0.2.1.7 read the headset's inputs after every report it sent, several times a second, and while each read waited the transmitter seems to have handed back its last report again (Seen in the recording; the cause is inferred): one signal report came back 4,998 times in under a second, where 0.2.1.2's recording has one every ten seconds. The headset then missed two heartbeats in a row, and Neap let go of it. The inputs are now read once at connect, a repeated report is taken once, the heartbeat waits its turn after other requests, and the log says when two heartbeats go unanswered. | Ask the tester to leave it running for ten minutes while recording. |
| Linux: Not connected for about 5 seconds in every 24 | 0.2.1.8 | Fixed in main, not yet seen on the headset. The transmitter hands back its last report until it has a new one. The first part of the long Prm reply at connect came back again and again, and each copy was taken in as the start of another reply that never closed. Every reply after it was then read as part of that one: TX and NG, which come next, gave "no answer", and so did every heartbeat, so Neap let go 16.4 seconds after each connect (Seen, 23 October: both groups "sent" Prm's first part, byte for byte, and six drops came 16.4 seconds after a connect). A copy of the report before is now taken in once on every read, not only during the RACE requests 0.2.1.8 covered, and a reply left unfinished is dropped when the next request goes out. Neap also no longer asks for `SAF`, which this headset does not have, about once a second while connected. Tests repeat the tester's case. When the headset sent its signal strength every few seconds, the heartbeat was never needed, which is why the session after the replug stayed connected. | Ask the Linux tester to leave it ten minutes while recording. |
| Linux: the transmitter stops answering | 0.2.1.2 | Open. It happens when Neap starts long after the transmitter was plugged in: on every start by hand since 20 October, not on 23 October's start with the PC. Neap's one restart brought nothing back under the new rule (23 October, 0 reports); unplugging it and plugging it back in did, at once. That matches the Stealth Pro II's dock, which answers only when something has read it since it was plugged in (SP2Tools, proven on the dock). The ~445 reports earlier restarts "read" were one old report handed back again, not answers. Fixed in main for the next test build: a transmitter restarted once and still silent is asked whether the headset is linked again, and with no answer, Home asks the person to unplug it and plug it back in. | Ask the Linux tester to start Neap by hand with the headset on, follow what Home asks, and record. |
| Linux: Neap's restart switches the headset off | 0.2.1.5 | It happened on every start of Neap. 0.2.1.7 restarts only a transmitter that gives no answer at all, not one that says no headset is linked. Not the transmitter's doing (F118); probably the headset reacting to the link going. | Ask the Linux tester whether it still happens. |
| Linux: the restart's log said the transmitter answered when it hadn't | 0.2.1.5 | Fixed for 0.2.1.7: a report with a length of 0 is empty, whatever is left in it | — |
| Mic monitoring too quiet: heard only when speaking very loudly | 0.2.1.6 | Open. On 22 October every level read back as the gain the firmware gives it, with 100% at 0 dB, the most its own key allows; the tester hears 0% and 100% differ, but not his voice clearly. Left: the noise gate, which Neap still cannot read (it was never confirmed off), and whether 0 dB is simply this headset's most. A gain above 0 dB is possible only by writing the sound chip's saved sidetone setting directly (F120: its block allows up to +12 dB), never tried, and not offered. | Ask whether Swarm II at 100% is any louder than Neap at 100%. If not, Neap matches the headset. |
| Mic monitoring goes on while muted | 0.2.1.6 | Fixed for 0.2.1.7: Neap mutes in the system, which the headset never hears of, so it now turns monitoring down too, as the headset's own mute does (F120) | Ask the tester. |
| Master volume shakes on big dial turns | 0.2.1.2 | Rare; explained by F120 | Left. |
| Transmitter light has no reading at start | 0.2.1.5 | Fixed for 0.2.1.7: read from the transmitter itself (F116) | Ask the tester. |
| Superhuman Hearing shows on after the equaliser has turned it off | 0.2.1.6 | Fixed for 0.2.1.7 (F122) | Ask the tester to change a preset with it on. |
| Master volume's mute button is highlighted when muted | 0.2.1.2 | By design: the mute button is on while muted | — |
| Noise gate shows no reading at start | 0.2.1.2 | Fixed in main, not yet seen: the firmware serves it as group `NG` (SP2Tools F125), which 0.2.1.7 asks, but the reply was lost behind the copies of Prm's first part (see the drops above) | Check the next recording for `0x700` and `0x710`. |
| Neap marked Superhuman Hearing and voice prompts as unread, so profiles left them out | — | Fixed for 0.2.1.7: the headset reports both, seen in the recordings since 0.2.1.3 | — |

## Next

- [x] Publish test build 0.2.1.7 and ask both testers for one recording each.
- [x] Publish test build 0.2.1.8: the Windows drops, the Linux restart order
      and its notice, the loudness boost hidden; ask the Windows tester for ten
      minutes untouched and Swarm II's monitoring at 100%, the Linux tester for
      one start with the headset on.
- [ ] Windows tester: with the noise gate off, monitoring at 100% while
      speaking normally, recorded, flipping the boom and pressing each button
      once in the same recording; the loudness boost; the light at start; which
      boom position mutes; and the checks never answered (equalisers, a custom
      preset, auto shut-off, the volume's mute, profiles with different mixes).
- [x] Publish test build 0.2.1.9: the Linux drops, the replug asked for on
      Home, and `SAF` no longer watched.
- [ ] Linux tester, 0.2.1.9: start Neap by hand with the headset on,
      follow what Home asks, then leave it ten minutes while recording.
- [ ] Ask the Windows tester to check the equalisers, a custom preset and
      auto shut-off.
- [ ] Decide on battery warnings and the High Bandwidth switches.
- [ ] Find which button `0xB00`–`0xB20` sets from the tester's recording,
      which logs the headset's record of its inputs, before offering the
      button's job.
- [ ] Add what Swarm II's Atlas Air plugin showed to SP2Tools.
- [x] Before 0.3.0: hide on the Atlas Air whatever the testers have not
      confirmed. Done by leaving the Atlas Air out of 0.3.0 altogether.

## Evidence

Newest first.

- 2026-10-23 — Linux tester, 0.2.1.8. Started by hand with the headset on:
  Quiet; the transmitter gave no answer to the link question, was restarted,
  read back nothing, and stayed silent. Unplugged and plugged back in: it
  connected at once and stayed for two and a half minutes, with the signal
  reported every few seconds. Started with the PC: the transmitter answered
  that no headset was linked, connected when the headset came on, then went
  Quiet 16.4 seconds after every connect, six times. At each connect `TX` and
  `NG` "sent" the same first part of the Prm reply. Read for the fix in main.
- 2026-10-22 — Windows tester, 0.2.1.7 (7 minutes): Not connected every 20
  seconds to two minutes, with no line in the log to say why; the recording
  filled in 14 seconds with one signal report repeated 4,998 times. Mic
  monitoring makes a difference between 0% and 100%, but sounds muffled
  rather than like hearing his own voice. The loudness boost made no
  difference he could hear. The inputs record showed the mute switch, the
  source switch and the media button. Linux tester, 0.2.1.7: Neap restarted
  the transmitter before offering the new access rule, the headset stayed
  on, and after the rule was allowed Neap stayed Quiet; the transmitter
  probably needs unplugging and plugging back in once the new rule is in.

- 2026-10-22 — SP2Tools answered Neap's questions from the Atlas Air's
  firmware (F114 to F122), all read, none tried on a headset. The Linux
  silence is the transmitter's USB state, with a suspend the likely cause.
  The light is read from the transmitter by RACE `0x0006`. `0x620` is
  monitoring's on/off and `0x630` its level, −30 to 0 dB. A mute from the PC
  or the headset's button silences monitoring too. `0x610` is a gain the
  transmitter sets from the PC's level. `0xC00` has 16 steps. The loudness
  boost's command is the Stealth Pro II's. An equaliser change turns
  Superhuman Hearing off. Built into 0.2.1.7.
- 2026-10-21 — Windows tester, 0.2.1.6: mic monitoring works, but only when
  speaking very loudly, and it goes on while the microphone is muted. The
  rest works. Sent Swarm II's firmware folder for the headset. Also: the new
  look is confusing in places, and the game and chat dial moved as the chat
  apps list opened, since fixed in main with a dropdown.
- 2026-10-21 — Linux tester, 0.2.1.6, a second start of Neap: the headset
  showed not connected while its sound played. The log shows Neap restarting
  the transmitter on every start, and every report after the restart empty
  (a length of 0, with old bytes after it), so the restarts of 20 and 21
  October did not bring it back.
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

## Changelog lines held back from 0.3.0

Taken out of `CHANGELOG.md`'s Unreleased section; put them back, under Added and
Fixed, when the Atlas Air is released.

- Neap no longer asks the Atlas Air for two groups of settings it does not
  have, when it connects or while it is connected.
- On Linux, the Atlas Air no longer drops to "Not connected" every 24
  seconds or so. Neap took in the same report again and again as the
  transmitter handed it back, and then could not read the headset's replies.
- Neap recognises the Turtle Beach Atlas Air, finds its speakers and
  microphone, and shows only what it has: no chat wheel, noise cancellation,
  lights, Charging Dock or CrossPlay. Its settings can be changed, in testing
  with an owner of one.
- On the Atlas Air, Superhuman Hearing and voice prompts use the places
  Swarm II keeps them, rather than the Stealth Pro II's. In testing with an
  owner of one.
- On the Atlas Air, mic monitoring and the transmitter's light use the places
  Swarm II keeps them. In testing with an owner of one.
- On a headset with no chat wheel, such as the Atlas Air, a profile saves the
  game and chat mix and puts it back when it's switched to.
- The Atlas Air's voice prompts read as a percentage, like the levels beside
  them, and the slider keeps one scale from the start.
- The Atlas Air no longer drops to "Not connected" every minute or so while
  it is in use. Neap was asking it for its buttons and switches several times
  a second, and the headset then missed Neap's regular check. They are now
  read once, when it connects, and the log says when the headset stops
  answering that check.
- On the Atlas Air, muting the microphone from Neap also turns mic monitoring
  down, as the headset's own mute does, and puts it back when you unmute.
  Before, you went on hearing yourself while muted.
- On the Atlas Air, the transmitter light shows its level as soon as Neap
  connects.
- On the Atlas Air, Superhuman Hearing shows as off when a change to the
  equaliser turns it off.
- On the Atlas Air, profiles keep Superhuman Hearing and the voice prompt
  level. Neap had taken them for settings it could not read, though the
  headset reports both.
- On the Atlas Air, moving mic monitoring above 1% changes it. Every
  higher level was refused, because its place is where the Stealth Pro II
  keeps an on and off switch.
- On the Atlas Air, the master volume no longer jumps to 100% and back as it
  is moved, from the slider or the headset's wheel.
- On the Atlas Air, Bluetooth says whether the headset is listening over it.
  It used to say "Connected" on the transmitter and "Not connected" on
  Bluetooth.
- On the Atlas Air, profiles no longer stay on "Reading the headset's
  settings…". They wait only for settings the headset has.
- On the Atlas Air, AI noise reduction is no longer offered. The headset
  doesn't have it, and the switch was changing its mic monitoring level.
- On the Atlas Air, voice prompts go from 0 to 15, as in Swarm II, so every
  step changes how loud they are.
