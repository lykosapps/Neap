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

- **The saved parametric presets check fails on the hidden desktop.**
  `tools\ui\run.ps1 parametric-presets` passes on the screen, every step (12
  November), so the app is right. Through `tools\pretend-quiet.ps1` its
  Rename step fails at "it asks for a name": Rename is chosen from the More
  menu straight after the picker's list is opened and closed and Save is
  pressed. Opening More and choosing Rename by themselves work there. Tried
  without effect: choosing the menu item inside the practice run, and
  waiting for the list to read as closed. Until it is found, run this check
  on the screen for a release.

### From the review of 0.5.0's new features (13 November)

Found by reading the code, the confirmed ones followed end to end; the rest
say so. The seven worst were fixed before the tag.

- **Two headsets on one PC lose Parametric.** Parametric's on, firmware and
  seen gains are kept per PC, not per headset, so connecting the other
  headset says Swarm II reset the equaliser and turns Parametric off
  *(confirmed)*. Presets, the shape and equaliser backups are shared too,
  and the backups don't say which headset they came from.
- **A Parametric edit made while the headset is away is lost without a
  word,** and the screen keeps showing a shape the headset doesn't have
  *(confirmed; also so in 0.4.0)*.
- **Mix shortcuts with no chat app chosen** are taken from every other
  program and do nothing, with nothing saying why *(confirmed)*.
- **Two mix shortcuts on one combination:** the message blames another
  program, and pressing a shortcut in use while setting a key moves the mix
  rather than recording it *(confirmed)*.
- **The wheel's notice names shortcut keys that were refused** *(confirmed)*.
- **Firmware edits, an edit whose state is unknown:** nothing is ticked, so
  Apply would remove an installed edit, and the question before it doesn't
  say which edits go on or off *(not yet verified)*.
- **Firmware edits, every failure ends the same:** a run that wrote nothing
  and one whose put-back also failed both say "The change didn't finish".
- **Firmware edits, the Settings switch turned off with edits on the
  headset:** the page goes, Controls loses its way there, and nothing says
  the edits are still on.
- **Firmware edits, wording when the headset is only off:** "Couldn't read
  the headset's firmware", and "Charge the headset" when the battery is
  unknown rather than low.
- **Firmware edits, the run card at the smallest window** may push Close out
  of view *(not measured)*.
- **Atlas Air, no measurement:** Auto-EQ is hidden with no word, a target
  is drawn alone, and a measurement loaded for the Stealth Pro II stays
  chosen. The screen checks cover Parametric's turn-on on the Atlas Air,
  not editing, Compare or presets.
- **Saving over a Parametric preset** asks nothing, unlike the ten bands.
- **The test tone** can play in the other headset when both are plugged in
  *(not verified)*.
- **The mix bar shows on the main screen only,** so a game on another
  screen sees nothing (DECISIONS.md already says when to revisit).
- **Screen readers and the mix:** nothing is announced when the mix moves,
  and a refused shortcut's warning stays in a key's help text after it is
  fixed *(the second confirmed)*.
- **Two Stealth Pro IIs, one edited:** connecting the edited one turns
  Neap's own Mode button cycle off for the other for good.
- **Some screen checks still wait 2 s per action.** On the desktop
  `tools\pretend-quiet.ps1` makes, an action carried into the app by UI
  Automation lands exactly 2.00 s later (on the visible desktop, under
  0.05 s). The walk, `Press`, `Open-Page` and the menus act from inside the
  app over the practice pipe instead, but about 25 direct calls in the
  `tools\ui` checks (sliders, text and lists in `firmware`, `parametric-*`,
  `align` and others) still go through UI Automation.

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
- **Alignment, not re-measured since the 30 October fix:** the mini panel,
  dialogs and open lists in light and high contrast (`run.ps1 align`).
- **Why pictures sometimes come out black.** Not settled: the screen turns
  off after five idle minutes, but some black pictures came while the PC was
  in use. The deciding test: log lit or dark against idle time for six
  minutes with no input.
- **Interface leftovers.** The Default profile row has a few pixels more room
  below its drop-down than above it; Delete on a profile is a plain icon,
  not drawn as destructive (it asks for confirmation first).

## 3. A headset sitting with the owner

Needs the headset on the Windows app, and the owner present. Run
`tools\ui\run.ps1 hardware` first.

### To check
- **A firmware read that comes back wrong, read again.** Once, straight
  after the headset reconnected, an edit that was intact read as Changed (13
  November). A read that fits neither the factory's bytes nor the edit's is
  now asked a second time; not yet seen to catch one. Watch the app log for
  "so it is read again", and for a Changed that outlasts it.
- **Atlas Air after a power cycle, in Parametric.** Its ten-band gains are
  not kept through a power cycle; if they come back changed, Neap would take
  it for another app and leave Parametric each session *(read from the
  code, not seen)*.
- **Atlas Air, Superhuman Hearing after a Parametric change.** Its firmware
  turns Superhuman Hearing off after an equaliser change; Neap reads it
  again after a ten-band change only, so it may show On when it is off.
- **The mix bar over a game in full screen** that Windows reports as busy
  rather than full screen, as its full-screen optimisations can: the bar
  may show over it *(not seen)*.
- **Neap's own cycle in its new order, by ear.** It now steps off,
  transparency, cancelling, and goes to transparency by turning the level
  down after the headset has turned noise cancellation on. Tests and
  practice runs only: it needs a headset on factory firmware, which the
  owner's is not while the edit is on. Listen for how long noise
  cancellation is heard on the way into transparency.
- **The firmware's cycle comes back at full strength.** The edit sets noise
  cancellation to 100 on the step from transparency, whatever Strength was
  set; Neap's own cycle puts the Strength back. Decide whether Neap puts it
  back while it runs.
- **Does CrossPlay report its presses?** Unknown.
- **The spare battery** on the Charging Dock's panel: out, in, and gone
  on the USB Transmitter.
- **What the equaliser plays, measured properly.** Nothing about it has a
  trusted measurement: the 28 September one used a desk microphone on an
  unsealed ear cup (FINDINGS.md). Wanted, with a sealed, calibrated setup:
  one band at a time against the filter model, all ten bands raised
  together, Parametric's bands against its drawn curve, and whether the most boost
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

- **Is Strength under 50 transparency?** SP2Tools reads in the firmware that
  a noise cancellation level under 50 lets the room in at a varying gain,
  and only 50 and over cancels *(read from the code, not heard)*. Neap calls
  1 to 100 Strength and only 0 transparency. Listen at 49 and 50; if so, the
  slider and the three modes need another look.
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
  The brief is `knowledge/neap-asks-dock-chat-mix.md` there: desk questions
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
  offered. Dead end, not worth retrying: its two halves talk over a private
  app-service channel (`SyncProfile` in its log) that only a packaged app can
  join. The working method is its settings file (an offline registry hive in
  its `LocalState\Settings`). Before retrying the write, sign out and in once,
  or look for security software blocking the "back up / restore files"
  privileges; then confirm in Dolby Access itself that the profile shows Off,
  since a value that reads back is not proof.
- **Volume stops following Windows after sleep** (issue #6, one of five
  complaint groups). After the PC sleeps, the Windows slider moves and the
  headset's volume doesn't, until the transmitter is replugged. Swarm II has
  no sleep or resume handling at all, so Neap detecting the mismatch and
  re-asserting the volume, possibly over the HID button-volume path
  (`AppkeyCode` `0x0A`/`0x0B`, which Neap doesn't speak yet), would be new.
  Reproduce first: sleep, wake, then compare the Windows slider with a raw
  `0x0A`. The other groups are out of reach: "too quiet even at maximum" is
  the hardware ceiling (both apps use the system volume; only pre-gain helps),
  and call volume and phone spikes are on the Bluetooth path to the phone.

## 5. A Linux sitting

Needs a Linux machine or the WSL distros, and for the Deck lines the Steam Deck.

- **The Linux AppImage on more than the Steam Deck.** GitHub builds it and the
  tests pass on Linux, but it was last run by hand before the late interface
  changes. Other distributions, a Wayland-only desktop, GNOME with its
  notification-area extension, an Orca pass, and a real "headset access
  refused" system (a Deck that already has the rule cannot show it) are
  untested.
- **Parametric on Linux with a real headset.** Turning it on writes the
  headset's stored equaliser record, proven through the dock on Windows (28
  October) and not yet tried on Linux. Sharing the dock with the WSL distro
  needs one administrator prompt at the PC.
- **The mix overlay on Linux.** On Windows the mix shows on screen as the
  wheel or the keys move it; on Linux it is not made yet. Needs a window that
  never takes the keyboard, lets clicks through and stays away from a game
  that has the screen, under X11 and Wayland.
- **Gaming Mode on the Steam Deck.** Neap runs in desktop mode only. Wanted:
  the mix, the chat wheel and the headset's controls working while a game
  runs in Gaming Mode, with no window to open. Needs the Deck.
- **Linux packages and updates, on request.** Flatpak, `.deb` and `.rpm`
  packages (a package would install the headset rule, so the password
  prompt is never needed), an Arm build, and one for musl distributions such
  as Alpine. The AppImage updates itself from 0.4.0; a package would update
  through the system's own tools instead.

## 6. Another headset: the Atlas Air and the rest

Needs the testers' replies on GitHub issue 1, not the owner's headset.

- **The Atlas Air's open questions.** It ships with every function
  (DECISIONS.md, 31 October). Left, on GitHub issue 1: the noise gate's
  reading at start, which the headset refuses; sound stopping while
  connected; and the replug on Linux after Neap is started by hand.
- **Parametric and Superhuman Hearing on the Atlas Air.** Parametric is on
  from 0.5.0. Say on the Equaliser page that Superhuman Hearing silences
  it, and ask a tester whether a band change turns Superhuman Hearing off.
- **Firmware edits on Linux.** Test build 0.5.0.1 turns them on for Linux
  testers. A Stealth Pro II owner on Linux runs headset check 44; once it
  passes, turn them on for Linux in a release.
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
- Mode button presses that trigger actions on the PC.
- Spot Swarm II running and offer to close it.
- **Where an imported parametric preset came from,** shown beside the preset
  (Equalizer APO or AutoEq, and the file).
