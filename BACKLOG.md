# Backlog

Bugs, features and work still to do. When something is done, its line goes,
and the commit that did it says so. Each line says what is wrong or wanted,
not how to fix it. Items needing the headset say so. When the project moves
to GitHub, these become issues.

## Next, after 0.2.0 (released 8 October)

- **Screen reader on both systems.** Narrator on Windows and Orca on Linux
  have not been run on the new app.
- **The Linux AppImage on more than the Steam Deck.** GitHub builds it and the
  tests pass on Linux, but it was last run by hand before the late interface
  changes. Other distributions and a Wayland-only desktop are untested.
- **Seen on screen, not yet:** the drop-down fade, the flyout lists on the
  profile bar and spatial sound after their fix, the low, charging and weaker
  signal colours on Home, and the narrowest layout with the menu folded.
- **Settling time.** After a few more battery swaps, read the app log's
  battery lines and tighten the rule that shows "Settling…". Also see the
  headset's own bolt on its USB-C cable.
- **Interface leftovers.** The New profile button could sit at the right and
  drop under the note in a narrow window; the Default profile row has a few
  pixels more room below its drop-down than above it; Delete on a profile is
  not drawn as destructive.
- **Updating from 0.1.0 and from 0.2.0 on an installed copy** has not been
  run against a real release; the first update will be the test.

## Bugs

- The chat mix does not move when the wheel is turned, on the Windows app,
  with the headset connected. Seen once; the conditions are not yet pinned
  down. Needs the headset.
- After the headset's link dropped and came back several times, the profile
  spinner never finished and the microphone button could not be used. A
  second program asking the same transmitter at the same time may have caused
  the drops; not yet reproduced without one. Needs the headset.

### Known limits, recorded rather than fixed

- With both transmitter lights amber, no sound can still show as connected.
- At power-on, a transmitter's stale answer can show the wrong transmitter
  for about 16 seconds.

## Features

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
- **Superhuman Hearing's type as buttons** rather than a drop-down, as the
  Mode button's jobs are shown.
- **Quick panel from the taskbar.** A small panel for mid-game controls (mix,
  mute, noise control) without opening the window. Eventual; Home is its
  model.
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
- **More ways to get it on Linux.** Flatpak, `.deb` and `.rpm` packages
  (a package would install the headset rule, so the password prompt is never
  needed), an Arm build, and one for musl distributions such as Alpine. On
  request.
- **In-app updates on Linux.** Today Neap says a newer version is out and
  opens the page. An AppImage can be updated in place with a delta update tool
  (AppImageUpdate); worth doing if people ask.
- **Linux checks not yet done.** A screen reader (Orca) pass, a Wayland-only
  desktop, GNOME with its notification-area extension, and a real "headset
  access refused" system, which a Deck that already has the rule cannot show.
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
- **Help add this headset.** When Neap finds a Turtle Beach headset it does
  not support, it offers to read it and save the result, with the headset's
  name and transmitter addresses removed and the contents shown before
  anything is shared, then opens a filled-in GitHub issue to attach it to.
  Later, a guided version that watches while the owner changes settings in
  Swarm II, to match each value to its control.

## Checks with the headset

The checks themselves are in [CONTRIBUTING.md](CONTRIBUTING.md). Owed:

- **The rest of the hardware list**, on the new app. `run.ps1 hardware` has
  covered noise control, the audio format, the mix and its shortcuts,
  recovery after the app is ended, the spare battery's reading and starting
  at sign-in (7 Oct). Still by hand: unplugging and switching to see each
  connection state, the wheel, the Mode button, hearing a mode or a format,
  a game in front for the shortcuts, a real sign-out, saving and deleting
  equaliser presets, and updating from an installed copy.
- **High contrast.** The fixes for it have not been seen on screen.
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

- **Publishing 0.2.0.** GitHub has only 0.1.0. The old local `v0.2.0` tag was
  deleted on 8 Oct; tag 0.2.0 on the final commit of `main`, set the date in
  the changelog to that day, and the workflow builds from the tag.
- High contrast has still not been seen on the update banner, the notes dialog,
  the Settings card or the Profiles page.

- Build, tests and formatting required on every pull request, once on GitHub.
- Sign the download, so Windows does not warn about it. The updater should
  check the signature too.
- Updater, nice to have: a limit on how big a download may be and how long
  it may take, and a way to cancel it.
- Publish on GitHub: the repository named "neap", private vulnerability
  reporting on, main only, and a first real CI run.
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
