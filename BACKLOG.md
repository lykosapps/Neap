# Backlog

Bugs, features and work still to do. When something is done, its line goes,
and the commit that did it says so. Each line says what is wrong or wanted,
not how to fix it. Items needing the headset say so. When the project moves
to GitHub, these become issues.

## Bugs

- **A headset setting changed just after start can wait.** Every request to
  the headset queues in one line, so a change made while the presets are
  being read waits behind them, about four seconds. The microphone no longer
  does: it mutes in Windows, measured at a tenth of a second mid-read.

### Known limits, recorded rather than fixed

- With both transmitter lights amber, no sound can still show as connected.
- At power-on, a transmitter's stale answer can show the wrong transmitter
  for about 16 seconds.

## Features

- **Profiles.** Save and apply a set of settings, switch automatically when
  an app starts, and switch with hotkeys.
- **A parametric equaliser.** Pick a frequency, gain and width, and Neap sets
  the ten bands to match as closely as they can. The headset's band
  frequencies and widths are fixed in its firmware, so only broad changes
  come close; the width stops at the narrowest the bands can make. The plot
  shows the curve asked for and the curve the headset plays, with the gap
  between them, and says so only when the miss is audible (over about
  2 dB): "The headset can't match this exactly. The solid line is what
  you'll hear." Its presets keep their parametric settings in Neap, and the
  headset slot holds the ten gains, so a preset still sounds right without
  Neap. Needs the headset to confirm the modelled curve.
- **Superhuman Hearing's type as buttons** rather than a drop-down, as the
  Mode button's jobs are shown.
- **Quick panel from the taskbar.** A small panel for mid-game controls (mix,
  mute, noise control) without opening the window. Eventual; Home is its
  model.
- **An installer**, beyond the zip.
- **Microsoft Store distribution.**

## Checks with the headset

The checks themselves are in [CONTRIBUTING.md](CONTRIBUTING.md). Owed:

- **The full hardware list**, on the current build.
- **Light theme.** Includes faint window buttons when Windows is forced to
  light.
- **High contrast.** The fixes for it have not been seen on screen.
- **Neap's colours on drop-downs, menus, tooltips and dialogs**, on screen.
- **Starting with Windows** on the current build.
- **Does CrossPlay report its presses?** Unknown.
- **The lights:** the USB Transmitter's second light, and the lights while
  sound and settings are on different transmitters.
- **The mix's edge cases:** an app with several audio sessions, the chat app
  not running yet, and Discord restarting mid-call.
- **Two chat apps at once**, Discord and Teams say: both follow the chat
  side, and clearing the last puts every volume back.
- **The chat app list on screen** in light and high contrast, and the screen
  checks' new steps for it, which have not had a full run.

## Protocol unknowns

- Seventeen values the headset reports are not yet matched to anything; see
  [FINDINGS.md](FINDINGS.md).
- Whether the charging flag means charging, or only powered by the cable.
- Two of the microphone equaliser's band frequencies are inferred, not read.
- 32-bit audio formats are assumed to be floating point.

## Tooling and release

- A fast formatting check before each commit.
- Build, tests and formatting required on every pull request, once on GitHub.
- An automated contrast and theme check of every screen.
- Sign the download, so Windows does not warn about it.
- Publish on GitHub: the repository named "neap", private vulnerability
  reporting on, main only, and a first real CI run.
- A package update pass.
- An ARM build.
- A read-through by an experienced .NET developer before release.
- A trademark search for the name.
- Screen checks pull Neap to the front, so they can't run while someone uses
  the machine.

## Smaller code items

- The mix dial's size is fixed.
- The microphone meter reads its Large setting once.
- Home rebuilds its readings on every change.
- Volumes are polled rather than followed by event.

## Last in line

- **Other languages.** Lowest priority. When it comes, start with the Mode
  button's, the dial's and Superhuman Hearing's option names, which are
  written into the code rather than the resource file, so they can't be
  translated.

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
