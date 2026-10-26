# Backlog

Bugs, features and work still to do. When something is done, its line goes,
and the commit that did it says so. Each line says what is wrong or wanted,
not how to fix it. Items needing the headset say so. When the project moves
to GitHub, these become issues.

## Next, after 0.3.0 (released 24 October)

- **The Atlas Air is held back until its testers confirm it.** 0.3.0 left it
  out (DECISIONS.md, 23 October); its test build, 0.2.1.9, has every
  function, writes included. Before it is released, set its list of
  functions, and whether its settings may be changed, to what the testers
  confirmed on GitHub issue 1.
  Also before it: unmuting from Neap before the headset's settings are read
  throws away the mic monitoring level kept from the mute, so monitoring
  stays at nothing.

- **The Linux AppImage on more than the Steam Deck.** GitHub builds it and the
  tests pass on Linux, but it was last run by hand before the late interface
  changes. Other distributions, a Wayland-only desktop, GNOME with its
  notification-area extension, an Orca pass, and a real "headset access
  refused" system (a Deck that already has the rule cannot show it) are
  untested.
- **Seen on screen, not yet:** the drop-down fade, the flyout lists on the
  profile bar and spatial sound after their fix, the low and weaker
  signal colours on Home (charging and strong were seen on 26 October), and the narrowest layout with the menu folded.
- **Settling time.** After a few more battery swaps, read the app log's
  battery lines and tighten the rule that shows "Settling…". Also see the
  headset's own bolt on its USB-C cable.
- **Interface leftovers.** The Default profile row has a few pixels more room
  below its drop-down than above it; Delete on a profile is a plain icon,
  not drawn as destructive (it asks for confirmation first).

## Bugs

- The chat mix does not move when the wheel is turned, on the Windows app,
  with the headset connected. Seen once; 0.3.0 fixed a wheel stuck at the
  middle, which may be this. Check on the headset before closing.
- Neap showed the headset as off for 30 hours, over restarts of Neap and the
  PC and a switch off and on, while sound played through the Charging Dock.
  Only unplugging the dock and plugging it back in brought it back. Whether
  the dock stopped answering or answered with something Neap could not read
  is not known; the log now records which, the next time. Needs the headset.
- With the cable in, unplugging the Charging Dock left the headset silent
  until it was switched off and on, while Neap showed it connected. Seen
  once. Needs the headset.
- Switching Windows into high contrast, or between high contrast themes,
  while Neap is open kept the colours Neap read when it started. Neap now
  reads them again on each change (26 October), not yet seen: switch high
  contrast themes with Neap open and check the colours follow. Then add a
  line to the changelog and clear this one.
- The game equaliser's Bands and Parametric choice may show neither as
  chosen after a headset change, as the hidden microphone panel's buttons
  share its group. From the 0.3.0 code review; not reproduced, as the page
  drawn in parametric mode showed it chosen.
- A session recording writes both outputs every second when two of the
  headset's outputs share a name. Whether Windows gives them the same name
  is not confirmed.
- Before the firmware page is released: ticking an edit, or restoring a
  backup, rebuilds the list and drops keyboard focus; and progress is drawn
  from the writer's own thread, which a real writer would not allow.

### Known limits, recorded rather than fixed

- With both transmitter lights amber, no sound can still show as connected.
- At power-on, a transmitter's stale answer can show the wrong transmitter
  for about 16 seconds.

## Features

- **Superhuman Hearing in the mini panel, and a choice of which tiles show**
  (24 October, asked for on Reddit by a 0.3.0 user; the choice added 26
  October). The power button turns it on and off too easily, so it gets left
  in the wrong state; a tile would show and change it without opening the
  window. Which tiles the panel shows is the person's choice in Settings,
  for every tile (microphone, noise cancellation, spatial sound, equaliser
  preset and this one), not only the new one.
- **Fewer words in the messages the state audit left long** (23 October):
  the routing notices, up to 26 words; the Linux access messages, about 45;
  and the folded sections' titles, such as "The audio format, power,
  firmware, serial number and everything the headset reports".
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
  unsafe, so each experiment needs the owner's go-ahead. Needs the headset
  and two transmitters. Until then the note for a headset that is not
  connecting says to switch it off and on after moving it.
- **Profile hotkeys.** Switch profiles from the keyboard, as the mix can be.
- **Dolby Access's own profiles from Neap.** Offer Dolby Access's profiles
  (Game, Movie, Music, Voice) from Neap, and turn each one's own equaliser
  off when it's chosen, so it never doubles up with the headset's own.
  Confirmed each profile keeps its own equaliser value, so this is worth
  building. Parked for now: no public way in from an unpackaged app, and
  the one working method needs a Windows permission this machine currently
  refuses even to an administrator. Neap currently only asks the person to
  turn Dolby Access's equaliser off themselves, wherever Dolby Atmos is
  offered.
- **Renaming the headset**, as Swarm II can. Neap neither shows nor changes
  the headset's name. Needs the headset: that the new name is kept, and
  where else it appears.
- **An installer**, beyond the zip. Per-user, so the in-app updater keeps
  working: Windows keeps Program Files locked, and the updater never runs
  elevated.
- **Microsoft Store distribution.**
- **Gaming Mode on the Steam Deck.** Neap runs in desktop mode only. Wanted:
  the mix, the chat wheel and the headset's controls working while a game
  runs in Gaming Mode, with no window to open. Needs the Deck.
- **Linux packages and updates, on request.** Flatpak, `.deb` and `.rpm`
  packages (a package would install the headset rule, so the password
  prompt is never needed), an Arm build, and one for musl distributions such
  as Alpine. Also in-app updates: today Neap says a newer version is out and
  opens the page, and an AppImage can be updated in place with a delta
  update tool (AppImageUpdate).
- **A shared connection to the sound server on Linux.** Neap opens a fresh
  connection to the sound server for every question, about every five seconds
  while idle. One long-lived connection would do, with the caution that calls
  then queue if the server stalls. Measure on the Deck first (processor and
  battery); not yet measured.
- **Give a hidden app's memory back on Linux.** Windows hands back what a
  hidden app is not using; the Linux build keeps what its runtime holds.
  Measure on the Deck before building anything.
- **Faster start-up.** The new app starts in about 1.5 seconds against the old
  app's 0.8 on the same PC. Compiling it ahead of time would cut that at the
  cost of a bigger download; measure before and after.
- **Global shortcuts that reach native Wayland programs.** The keys work while
  an X program (most games) is in front; the desktop portal for global
  shortcuts would reach the rest, when the desktops people use support it.
- **Other Swarm II headsets.** The Stealth 600 Gen 3, 700 Gen 3, 500 and
  Atlas Air share the Stealth Pro II's platform. Each needs an owner to
  capture its settings and run the checks, so ask for help once public.
  The 600 Gen 3 first. Screens need to show only what a headset has.
- **Help add this headset, guided.** Neap already records a headset it
  does not support, shows what it found and opens a filled-in GitHub issue
  (0.3.0). Still wanted: a version that watches while the owner changes
  settings in Swarm II, to match each value to its control.

## Checks with the headset

The checks themselves are in [CONTRIBUTING.md](CONTRIBUTING.md). Owed:

- **The rest of the hardware list**, on the new app. `run.ps1 hardware` has
  covered noise control, the audio format, the mix and its shortcuts,
  recovery after the app is ended, the spare battery's reading and starting
  at sign-in (7 Oct). Still by hand: unplugging and switching to see each
  connection state, the wheel, the Mode button, hearing a mode or a format,
  a game in front for the shortcuts, a real sign-out, saving and deleting
  equaliser presets, and updating from an installed copy.
- **The tray icon's menu** in light and dark, on screen.
- **Does CrossPlay report its presses?** Unknown.
- **The lights:** the USB Transmitter's second light, and the lights while
  sound and settings are on different transmitters.
- **Settings while presets load.** Open the Microphone page and change a
  setting at once: it takes effect within about a second. Every saved
  preset is still listed on both equaliser pages, many times over: a read
  now waits a moment after a write, and a lost read would hide a preset.
- **The mix's edge cases:** an app with several audio sessions, the chat app
  not running yet, and Discord restarting mid-call.
- **A profile saved straight after opening Neap.** Open Neap, save a
  profile from Home at once, switch to another and back: does the
  equaliser come back, and does the bar stay un-Edited?
- **The spare battery** on the Charging Dock's panel: out, in, and gone
  on the USB Transmitter.
- **Two chat apps at once**, Discord and Teams say: both follow the chat
  side, and clearing the last puts every volume back.
- **The chat app list on screen** in light and high contrast, and the screen
  checks' new steps for it, which have not had a full run.
- **96 kHz through calls.** The Charging Dock went silent at 96 kHz until
  unplugged, after many format resets. With one reset per call, use 96 kHz
  for a few days of calls: does it stay audible? If it goes silent again,
  the log names the apps behind each reset.
- **The parametric curve heard, at the ends.** Measured from 60 Hz to
  6 kHz on 28 Sep (see FINDINGS.md); below and above that the recording
  microphone's own processing cut the signal. Still owed: the same
  measurement with that processing off, to cover the lowest and top bands,
  and whether the most boost the bands allow together distorts.

## Protocol unknowns

- Seventeen values the headset reports are not yet matched to anything; see
  [FINDINGS.md](FINDINGS.md).
- Whether the charging flag means charging, or only powered by the cable.
- 32-bit audio formats are assumed to be floating point.

## Tooling and release

- High contrast has still not been seen on the update banner, the notes dialog,
  the Settings card or the Profiles page.

- Sign the download, so Windows does not warn about it. The updater should
  check the signature too.
- Updater, nice to have: a limit on how big a download may be and how long
  it may take, and a way to cancel it.
- A package update pass before each release. The new app's packages were
  set on 7 Oct (Avalonia 12.1.3, H.NotifyIcon 2.4.1).
- An ARM build.
- A read-through by an experienced .NET developer before release.
- A trademark search for the name.
- Screen checks pull Neap to the front, so they can't run while someone uses
  the machine.
- Pretend mode can't show spatial sound, a missing Dolby licence or a second
  transmitter, so a change to any of those can only be checked on the headset.
- Pretend mode has no high-contrast option, and the screen check never uses
  the spatial sound tile.

## Smaller code items

- The mix dial's size is fixed.
- Volumes are polled rather than followed by event.
- The band view draws a smooth line through the ten gains, not what plays.
  Neighbouring bands overlap, so ten bands at +9 dB play about +13.5 dB.
  The parametric view already draws what plays.
- The headset's connection handle has no safety net if it is ever dropped
  without being closed. Needs the headset.
- Typing "20" in the parametric frequency field gives 20 Hz, even over
  "20 kHz", and "1,500 Hz" gives 20 Hz.
- The parametric "can't match this exactly" note ignores the range above
  18 kHz, where a boost at 20 kHz misses by about 8 dB on the plot.
- A loudness boost write is missing from a session recording's list of
  writes, and nothing in Core keeps it to headsets where the command is
  known; only the screen hides the control.
- The chat app list names an app by its program file, such as "ms-teams",
  when Windows gives no display name.

## Last in line

- **Other languages.** Lowest priority. The settings' option names are in
  the resource file now. The transmitters' names (Charging Dock, USB
  Transmitter, Headset) are still written into Core, where the rules for
  what a screen says pick them, so they are next.

## Ideas, not yet decided

- Neap's own volume overlay. Windows' own cannot be hidden.
- Mode button presses that trigger actions on the PC.
- Spot Swarm II running and offer to close it.
- Charging reminders.
- Bass and treble boost levels, if the headset turns out to have them.
- A virtual-cable fallback, for older Windows or calls in a browser.
- The Controls page drawing of the headset.
- Unreachable states in the pretend headset.
- The equaliser as one tab stop.
