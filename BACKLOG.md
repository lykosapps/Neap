# Backlog

Bugs, features and work still to do. When something is done, its line goes,
and the commit that did it says so. Each line says what is wrong or wanted,
not how to fix it. Items needing the headset say so. When the project moves
to GitHub, these become issues.

The list is grouped by the sitting that would do the work, so one pass can
clear several lines. Each group says what it needs. A line belongs to one
group only; a line that touches two says so in its text.

## 1. Desk work, no headset and no screen

Needs only the code and the tests. Safe to do in one go.

### Recordings and what Core guarantees
- Volumes are polled rather than followed by event: looked at on 26 October and
  kept, not a small change. The mix's two-second pass (`SessionMix`) is what
  catches an application that starts and a volume changed outside Neap, and it
  also restores volumes after a device switch and looks for chat on the other
  outputs. Windows events would mean session-created notifications on the
  headset device plus a volume callback on every session, each arriving on a
  COM thread and needing registering and unregistering as devices and sessions
  come and go; the Pulse client runs its own loop only when asked, so a
  subscription would need a standing loop. A poll stays as the safety net, so
  the pass would still run, only less often. Revisit only if the idle wakeups
  are measured as a problem.

### Interface text
- **Saving or deleting a preset as the headset goes shows the system's own
  error.** Unplugging the Charging Dock while a preset saved showed "Couldn't
  save" over "SetOutputReport failed (The device is not connected.)", the text
  of the error itself. Saving, saving over and deleting all pass that text to
  the screen. Seen on the headset on 30 October; nothing was saved or left
  half-written.

## 2. A screen sitting on Windows

Needs the screen on, and Windows switched into a high contrast theme and back.
`tools\ui\looks.ps1` covers the pages as drawn by default; the rest is by eye.

- **High contrast, changing while Neap is open.** Neap kept the colours it
  read when it started. It now reads them again on each change (26 October),
  not yet seen: switch high contrast themes with Neap open and check the
  colours follow. Then add a line to the changelog and clear this one. While
  in high contrast, also look at the update banner, the notes dialog, the
  Settings card and the Profiles page, which have not been seen there.
- **Seen on screen, not yet:** the drop-down fade, the flyout lists on the
  profile bar and spatial sound after their fix, the low and weaker signal
  colours on Home (charging and strong were seen on 26 October), and the
  narrowest layout with the menu folded.
- **The chat app list on screen** in light and high contrast, and the screen
  checks' new steps for it, which have not had a full run.
- **The tray icon's menu** in light and dark, on screen.
- **Interface leftovers.** The Default profile row has a few pixels more room
  below its drop-down than above it; Delete on a profile is a plain icon,
  not drawn as destructive (it asks for confirmation first).

## 3. A headset sitting with the owner

Needs the headset on the Windows app, and the owner present. Run
`tools\ui\run.ps1 hardware` first.

### To check
- **Does CrossPlay report its presses?** Unknown.
- **The spare battery** on the Charging Dock's panel: out, in, and gone
  on the USB Transmitter.
- **What the equaliser plays, measured properly.** Nothing about it has a
  trusted measurement: the 28 September one used a desk microphone on an
  unsealed ear cup (FINDINGS.md). Wanted, with a sealed, calibrated setup:
  one band at a time against the filter model, all ten bands raised
  together, the parametric view's fitted curves, and whether the most boost
  the bands allow distorts.
- **Settling time.** After a few more battery swaps, read the app log's
  battery lines and tighten the rule that shows "Settling…". Also see the
  headset's own bolt on its USB-C cable.
- The headset's connection handle has no safety net if it is ever dropped
  without being closed.

### Bugs seen once
- A preset named "1" with flat bands appeared on the headset after a save
  cut short by unplugging the Charging Dock (30 October). Saving is three
  writes, the name last; whether a save stopped part way leaves a preset the
  headset names itself is unproven and needs a save interrupted on purpose.
- Opening the Equaliser page once read every game slot as empty while the
  headset counted one preset, so saving was refused with "Try again" and no
  way to try again but leaving the page. The next read was right.
- The chat mix does not move when the wheel is turned, on the Windows app,
  with the headset connected. Seen once; 0.3.0 fixed a wheel stuck at the
  middle, which may be this. Check on the headset before closing.
- Neap showed the headset as off for 30 hours, over restarts of Neap and the
  PC and a switch off and on, while sound played through the Charging Dock.
  Only unplugging the dock and plugging it back in brought it back. Whether
  the dock stopped answering or answered with something Neap could not read
  is not known; the log now records which, the next time.
- With the cable in, unplugging the Charging Dock left the headset silent
  until it was switched off and on, while Neap showed it connected. Seen
  once.

### Known limits, recorded rather than fixed
- With both transmitter lights amber, no sound can still show as connected.
- At power-on, a transmitter's stale answer can show the wrong transmitter
  for about 16 seconds.

## 4. Research on the headset (go-ahead needed for each experiment)

Needs the headset, and for the first line two transmitters. Look in SP2Tools
first and say how sure each finding is.

- **Move the headset between PCs without switching it off and on.**
  *High priority.* The headset keeps its settings on the transmitter it was
  switched on with, and CrossPlay moves only its sound, so after moving
  between a Charging Dock on one PC and a USB Transmitter on another, the
  second PC plays sound but cannot read or change the settings until the
  headset is switched off and on there. Wanted: moving the headset to a
  transmitter, from either side, takes its settings with it, with nothing
  unplugged or switched off. Research first: whether the headset has any way
  to be told to move its settings, and how Swarm II behaves in the same
  situation. The transmitter slots' selection writes are known to be
  unsafe, so each experiment needs the owner's go-ahead. Until then the note
  for a headset that is not connecting says to switch it off and on after
  moving it.
- **Renaming the headset**, as Swarm II can. Neap neither shows nor changes
  the headset's name. Needs the headset: that the new name is kept, and
  where else it appears.
- **Protocol unknowns.** Seventeen values the headset reports are not yet
  matched to anything (see [FINDINGS.md](FINDINGS.md)); whether the charging
  flag means charging, or only powered by the cable; 32-bit audio formats
  are assumed to be floating point.
- **A fully parametric equaliser, live.** On for the Stealth Pro II from
  0.4.0 (DECISIONS.md, 31 October; and 27 and 28 October). Each of the game bank's ten bands has a
  filter, frequency, width and gain, set live over Pre-gain's route,
  after the headset's stock gain correction is cleared, which choosing
  Parametric on the Equaliser page does, through the dock or the cable.
  Neap keeps its own presets, imports Equalizer APO and
  AutoEq filters, lets a profile hold a shape, gives way to another app's ten-band
  changes, and turns parametric off when Swarm II resets the headset. Its
  Undo, Redo and Compare come in the same order as with the ten bands.
  Left, before 0.4.0 is tagged: the headset checks (HEADSET-CHECKS.md, 31 to 36),
  the record write on Linux, and whether the Mode button's preset step
  moves the bands (if it does, Neap must block it or follow it).
- **Equaliser boosts with Pre-gain raised.** SP2Tools reads an
  active compressor after the equaliser (threshold −10 dB, ratio 100:1,
  make-up +7 dB, live over the same route) that squashes a 0 dBFS tone by
  about 4, 7 and 10 dB at +6, +9 and +12 dB of boost, so a boost mostly
  raises compression, not level (read from the code, not proven), and the
  Pre-gain raises the level going into it. On a headset with
  Pre-gain raised, measure a bass boost and all ten bands at
  +9 dB for compression or clipping, against the same at stock, with the
  sealed setup above. If it squashes, Pre-gain or the equaliser may need to
  say so. Needs the headset and a recording microphone.
- **Game and chat mix in the dock, natively.** Neap mixes in software today
  (per-app volumes). The dock's firmware has a second stereo playback
  function, off in every mode it runs, that would show Windows separate Game
  and Chat endpoints, so any chat app could play to Chat and the wheel would
  set a mix ratio, with no app list and no polling. Not known: whether the
  dock sends that second stream to the headset and mixes it (its chat-mix
  code is gated to Xbox), and whether the dock can keep its USB identity
  while doing it, which Neap needs to find it. The live switch changes the
  identity and undoes itself at a power cycle; a lasting one is a 2-byte dock
  flash patch. SP2Tools has both parked (`W1` the live test, `W2` the patch).
  The brief is `knowledge
eap-asks-dock-chat-mix.md` there: desk questions
  first, then the one experiment when the owner chooses it. Needs the
  headset and dock, with Neap closed for the test.
- **Bass and treble boost levels**, if the headset turns out to have them.
- **Dolby Access's own profiles from Neap** (parked). Offer Dolby Access's
  profiles (Game, Movie, Music, Voice) from Neap, and turn each one's own
  equaliser off when it's chosen, so it never doubles up with the headset's
  own. Confirmed each profile keeps its own equaliser value, so this is worth
  building. Parked for now: no public way in from an unpackaged app, and
  the one working method needs a Windows permission this machine currently
  refuses even to an administrator. Neap currently only asks the person to
  turn Dolby Access's equaliser off themselves, wherever Dolby Atmos is
  offered.

## 5. A Linux sitting

Needs a Linux machine or the WSL distros, and for the Deck lines the Steam Deck.

- **The Linux AppImage on more than the Steam Deck.** GitHub builds it and the
  tests pass on Linux, but it was last run by hand before the late interface
  changes. Other distributions, a Wayland-only desktop, GNOME with its
  notification-area extension, an Orca pass, and a real "headset access
  refused" system (a Deck that already has the rule cannot show it) are
  untested.
- **Gaming Mode on the Steam Deck.** Neap runs in desktop mode only. Wanted:
  the mix, the chat wheel and the headset's controls working while a game
  runs in Gaming Mode, with no window to open. Needs the Deck.
- **Linux packages and updates, on request.** Flatpak, `.deb` and `.rpm`
  packages (a package would install the headset rule, so the password
  prompt is never needed), an Arm build, and one for musl distributions such
  as Alpine. Also in-app updates: today Neap says a newer version is out and
  opens the page, and an AppImage can be updated in place with a delta
  update tool (AppImageUpdate).

## 6. Another headset: the Atlas Air and the rest

Needs the testers' replies on GitHub issue 1, not the owner's headset.

- **The Atlas Air's open questions.** It ships with every function
  (DECISIONS.md, 31 October). Left, on GitHub issue 1: the noise gate's
  reading at start, which the headset refuses; sound stopping while
  connected; and the replug on Linux after Neap is started by hand.
- **Other Swarm II headsets.** The Stealth 600 Gen 3, 700 Gen 3 and 500
  share the Stealth Pro II's platform (the Atlas Air has its own line above).
  Each needs an owner to capture its settings and run the checks, so ask for
  help once public. The 600 Gen 3 first. Screens need to show only what a
  headset has.
- **Help add this headset, guided.** Neap already records a headset it
  does not support, shows what it found and opens a filled-in GitHub issue
  (0.3.0). Still wanted: a version that watches while the owner changes
  settings in Swarm II, to match each value to its control.

## 7. Release and distribution

- Sign the download, so Windows does not warn about it. The updater should
  check the signature too.
- Updater, nice to have: a limit on how big a download may be and how long
  it may take, and a way to cancel it.
- **An installer**, beyond the zip. Per-user, so the in-app updater keeps
  working: Windows keeps Program Files locked, and the updater never runs
  elevated.
- **Microsoft Store distribution.**
- A read-through by an experienced .NET developer before release.

## 8. New features, not yet decided to build

- **Profile hotkeys.** Switch profiles from the keyboard, as the mix can be.
- A chat mix overlay: a small on-screen display when the mix moves.
- Mode button presses that trigger actions on the PC.
- Spot Swarm II running and offer to close it.
- **Import filters into the parametric equaliser.** Paste or open an Equalizer APO or AutoEq text file ("Filter 1: ON PK Fc 105 Hz Gain -2.5 dB Q 0.7") and put it on the ten bands, with where it came from shown beside the preset. Supported types only (bell, shelves, cuts, notch), at most ten bands, gains held to the headset's ±12 dB; what does not fit is listed, not dropped quietly. A preamp line is not used (Pre-gain, set on its own, is the headset's preamp), and the page says so. The way to a curve tuned to this headset rather than a generic starting shape, once someone has measured the Stealth Pro II properly; Neap's own measurement (28 September) is not trusted. Built on the parametric screen: "Import filters…" at the foot of Start from opens a dialog to paste into or open a .txt file, says before applying how many bands it sets, each line not used and why, gains held, and that a preamp is not used, and applies as one step Undo takes back. Still to do: showing where the bands came from beside the preset, once parametric presets are saved.
