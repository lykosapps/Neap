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
- Volumes are polled rather than followed by event.
- The mix dial's size is fixed.

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
- **Equaliser boosts with the loudness boost raised.** SP2Tools reads a
  compressor and a level limit after the equaliser, and the loudness boost
  raises the level going into it, which the firmware seems to keep 12 dB
  down as room for equaliser boosts (read from the code, not proven). On a
  headset with the boost raised, measure a bass boost and all ten bands at
  +9 dB for compression or clipping, against the same at stock, with the
  sealed setup above. If it squashes, the loudness boost or the equaliser may need to
  say so. Needs the headset and a recording microphone.
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

- **The Atlas Air is held back until its testers confirm it.** 0.3.0 left it
  out (DECISIONS.md, 23 October); its test build, 0.2.1.9, has every
  function, writes included. Before it is released, set its list of
  functions, and whether its settings may be changed, to what the testers
  confirmed on GitHub issue 1. Also before it: unmuting from Neap before the
  headset's settings are read throws away the mic monitoring level kept from
  the mute, so monitoring stays at nothing.
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
