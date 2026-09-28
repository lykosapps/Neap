# Backlog

Bugs, features and work still to do. When something is done, its line goes,
and the commit that did it says so. Each line says what is wrong or wanted,
not how to fix it. Items needing the headset say so. When the project moves
to GitHub, these become issues.

## Bugs

- **A profile saved before the equaliser has been read has no equaliser.**
  Open Neap and save a profile from Home straight away: it keeps no
  equaliser preset. Switching to it later leaves whatever equaliser was on,
  and the bar shows it as Edited at once, and stays so. The first profile
  most people make is saved this way. Seen in pretend mode on the shipped
  build; needs the headset to confirm. S3, P1: fix before Profiles ships.
- **Switching profile with the headset off looks done but isn't.** The bar
  shows the chosen profile and nothing says otherwise, but nothing is
  applied. When the headset comes back it still has the old settings, and
  the bar shows the profile as Edited. Seen in pretend mode on the shipped
  build. S3, P1: fix before Profiles ships. What should happen instead
  (apply on reconnect, or refuse while off) is the owner's call.

### Known limits, recorded rather than fixed

- With both transmitter lights amber, no sound can still show as connected.
- At power-on, a transmitter's stale answer can show the wrong transmitter
  for about 16 seconds.

## Features

- **Profiles.** Save and apply a set of settings, switch automatically when
  an app starts, and switch with hotkeys.
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
- **An installer**, beyond the zip.
- **Microsoft Store distribution.**
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

- **The full hardware list**, on the current build.
- **High contrast.** The fixes for it have not been seen on screen.
- **The tray icon's menu** in light and dark, on screen.
- **Starting with Windows** on the current build.
- **Does CrossPlay report its presses?** Unknown.
- **The lights:** the USB Transmitter's second light, and the lights while
  sound and settings are on different transmitters.
- **Settings while presets load.** Open the Microphone page and change a
  setting at once: it takes effect within about a second. Every saved
  preset is still listed on both equaliser pages, many times over: a read
  now waits a moment after a write, and a lost read would hide a preset.
- **Spatial sound, the rest of it.** Choosing Dolby Atmos from Home and
  hearing it on the headset is confirmed. Still owed: an unlicensed choice
  opening the Dolby Access dialog, Windows Sonic and off by ear, and, with
  both transmitters plugged in, a format carrying from one to the other on
  CrossPlay rather than silently reverting to off; a pretend run cannot
  plug in a second transmitter.
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

- Build, tests and formatting required on every pull request, once on GitHub.
- Sign the download, so Windows does not warn about it.
- Publish on GitHub: the repository named "neap", private vulnerability
  reporting on, main only, and a first real CI run.
- A package update pass. On 25 Sep: the Windows App SDK 1.8 to 2.5, a new
  major version to read up on first; H.NotifyIcon 2.3.0 to 2.4.1, which
  needs the tray menu checked by hand; and the SDK build tools. The rest
  are current.
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
