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
- **Superhuman Hearing's type as buttons** rather than a drop-down, as the
  Mode button's jobs are shown.
- **Quick panel from the taskbar.** A small panel for mid-game controls (mix,
  mute, noise control) without opening the window. Eventual; Home is its
  model.
- **Renaming the headset**, as Swarm II can. Neap neither shows nor changes
  the headset's name. Needs the headset: that the new name is kept, and
  where else it appears.
- **Spatial sound.** Choose Windows' spatial sound for the headset (off,
  Windows Sonic, Dolby Atmos for Headphones, DTS) from the Audio page,
  offering only what works on this PC. When Dolby is installed but not
  licensed, say so and point to the Dolby Access app. Needs the headset:
  games using Dolby's positional sound still follow the mix.
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
- **Light theme.** Includes faint window buttons when Windows is forced to
  light.
- **High contrast.** The fixes for it have not been seen on screen.
- **Neap's colours on drop-downs, menus, tooltips and dialogs**, on screen.
- **Starting with Windows** on the current build.
- **Does CrossPlay report its presses?** Unknown.
- **The lights:** the USB Transmitter's second light, and the lights while
  sound and settings are on different transmitters.
- **The parametric equaliser.** A tone sweep against the curve it shows,
  especially between bands; a parametric preset still sounding the same
  with Neap closed; and its plot in light and high contrast themes. The
  test tone: it plays on the headset, starts and stops without a click,
  and stops on leaving the page or hiding the window.
- **Keeping the dock's ring purple.** Joining a call turns it purple again
  within a few seconds, the gap is short, the call carries on, and it
  happens once per call; with the switch off, the ring stays white. Passing
  quickly through the Microphone page leaves it purple too.
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
- The band view draws a smooth line through the ten gains, not what plays.
  Neighbouring bands overlap, so ten bands at +9 dB play about +13.5 dB.
  The parametric view already draws what plays.

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
