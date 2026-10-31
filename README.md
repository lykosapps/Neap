<img src="docs/neap.png" alt="" width="96" align="right">

# Neap

Quiet control for the Turtle Beach Stealth Pro II and Atlas Air on Windows
and Linux, in place of Swarm II. No driver, no Turtle Beach software, no
admin rights.

**[Download the latest release](https://github.com/lykosapps/Neap/releases/latest)**
· Windows 10 version 2004 or later, 64-bit · Linux, 64-bit, with PulseAudio
or PipeWire

**Headsets.** The Stealth Pro II is tested on the Xbox edition; the
PlayStation edition should work, and reports are welcome. The Atlas Air is
tested by two owners.

> **Own another Turtle Beach headset?** A ten-minute recording made in Neap
> is where support for it starts. No technical knowledge needed.
> **[Here's how](docs/MAPPING.md).**

Not affiliated with or endorsed by Turtle Beach. Turtle Beach, Stealth Pro II
and Swarm II are trademarks of Turtle Beach Corporation.

## What it does

### A true parametric equaliser

On the Stealth Pro II, shape the sound yourself: choose where each of the
headset's ten bands sits, how wide it is and how much it boosts or cuts. The
headset plays the shape as drawn, through the Charging Dock or the cable.

- **Start from a measurement.** See the headset's measured sound against a
  target, and let Auto-EQ fit one to the other.
- **Bring your own.** Import AutoEq and Equalizer APO filters.
- **Keep it.** Save presets, and put a shape in a profile.

![The Equaliser page in Parametric, with the curve, the measured sound and a target, and a band's dials.](docs/screenshots/parametric.png)

### A game and chat mix, with nothing to install

Pick your chat apps, such as Discord or Teams, and Neap balances them
against everything else. Move it with the headset's chat wheel, the dial on
Home, or a keyboard shortcut in game. No virtual cable or driver, and it
works on the Xbox edition.

![Home, with the game and chat dial, a chat app chosen, and cards for the microphone, noise cancellation and equaliser.](docs/screenshots/mix.png)

### A mini panel for mid-game

Click Neap's icon by the clock for the mix, the microphone, noise
cancellation, spatial sound, Superhuman Hearing and the equaliser preset.
You choose which ones it shows.

![The mini panel, with the game and chat mix, tiles for the microphone, noise cancellation and spatial sound, and the equaliser preset.](docs/screenshots/mini.png)

### Profiles for each game

Save your whole setup under a name. A profile comes on when its game
starts, and goes back to your default when it closes. A call app can switch
only while it's on a call.

![The Profiles page, with a default profile and a second profile for a game.](docs/screenshots/profiles.png)

### Everything else

- **Every setting the headset has:** noise cancellation and transparency,
  the microphone, Superhuman Hearing, lights, power and the Charging Dock's
  spare battery.
- **The microphone follows the headset's mute.**
- **Pre-gain** makes a headset that's too quiet louder, and leaves room for
  equaliser boosts.
- **Catches the wrong device** when sound or calls go somewhere the headset
  isn't, and on Windows fixes it in one press.
- **Updates itself,** on Windows and from the Linux AppImage. Checking for
  updates is the only time Neap goes online, and it can be turned off.
- **Works from the keyboard and with screen readers,** in light, dark and
  high contrast.

## Install on Windows

1. Download `Neap-<version>-win-x64.zip` from the
   [latest release](https://github.com/lykosapps/Neap/releases/latest).
2. Unzip it to a folder of its own and run `Neap.exe`. Windows warns the
   first time, as the download isn't signed yet: choose **More info**, then
   **Run anyway**.
3. Close Swarm II and remove the Waves audio driver; see
   [Alongside Swarm II](#alongside-swarm-ii-windows).

**To update,** choose **Update and restart** in Settings when Neap says a
newer version is out. **To remove it,** delete its folder and
`%LOCALAPPDATA%\Neap`.

## Install on Linux

1. Download `Neap-<version>-x86_64.AppImage` from the
   [latest release](https://github.com/lykosapps/Neap/releases/latest).
2. Make it runnable (`chmod +x`, or **Allow executing as a program** in your
   file manager) and run it.
3. If Neap can't reach the headset, choose **Allow access** and enter your
   password once.

If it asks for FUSE, install `libfuse2`. **To update,** choose **Update and
restart** when Neap says a newer version is out. **To remove it,** delete
the file and `~/.local/share/Neap`, and `/etc/udev/rules.d/70-neap.rules` if
you allowed access.

**Not on Linux yet:** the Steam Deck's Gaming Mode (use desktop mode),
Windows' spatial sound and audio format, and the mix's live meters. On
Wayland, keyboard shortcuts work while a game, or another X11 program, is in
front.

## Alongside Swarm II (Windows)

1. **Keep Swarm II for firmware updates and pairing.** Neap does neither.
2. **Stop it starting with Windows.** The two apps fight over the headset.
   In Swarm II: Settings → App Settings → turn off **Autostart Swarm II**,
   then close it.
3. **Remove the Waves audio driver.** In Windows Settings → Apps, uninstall
   **Turtle Beach Audio Driver**.

## Status

Working and in daily use, but young. It is tested with real headsets on
Windows and on a Steam Deck, and on other Linux desktops only in pretend
mode. It was written with AI assistance.

**Found a problem?** In **Settings → Problems and other headsets**, record
while it happens, then choose **Report on GitHub**, or
[open an issue](https://github.com/lykosapps/Neap/issues). For a security
problem, see [SECURITY.md](SECURITY.md).

The [changelog](CHANGELOG.md) says what's new in each release.

## For developers

- [CONTRIBUTING.md](CONTRIBUTING.md): building, testing, the probe, and the
  checks that need a headset
- [FINDINGS.md](FINDINGS.md): the headset's protocol and behaviour, as
  measured
- [ARCHITECTURE.md](ARCHITECTURE.md): how the app is put together
- [BACKLOG.md](BACKLOG.md): bugs, features and work still to do

## Licence

Copyright (C) 2026 lykosapps

This program is free software: you can redistribute it and/or modify it
under the terms of the GNU General Public License as published by the Free
Software Foundation, either version 3 of the License, or (at your option)
any later version. It is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
FITNESS FOR A PARTICULAR PURPOSE. See [LICENSE](LICENSE) for the full text.

The third-party components it uses keep their own licences; see
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
