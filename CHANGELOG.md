# Changelog

What Neap does and how that changes, in plain words, newest first. It is
the source for release notes and for the project page. [FINDINGS.md](FINDINGS.md)
holds the evidence behind each claim, and the commit history holds every
change in detail.

Each change a person could notice gets a line under **Unreleased**, in the
section it belongs to. At a release, Unreleased takes the version number
and date.

## 0.1.0 — 2026-10-04, the first release

### Beyond Swarm II

- **Settings Swarm II's desktop app does not have:** wake on motion and
  voice prompt volume. Both were found through the phone app.
- **The spare battery's charge.** While the headset is on the Charging
  Dock, the dock's panel shows the spare battery charging in it, or that
  its slot is empty, and follows a swap straight away. Swarm II shows this
  only in its phone app.
- **Transparency.** The headset has no transparency mode, but noise
  cancellation at zero lets the room in. Neap offers noise cancellation,
  transparency and off as three modes, and the Mode button can step
  through all three while Neap is running.
- **A game and chat mix with nothing to install.** Pick the apps that
  carry chat, such as Discord or Teams, and Neap turns them against
  everything else. No virtual audio cable, driver or reboot. Swarm II's mix
  relies on the Waves driver, and is not officially supported on the Xbox
  edition: it works for some people, but most don't get it. Move the
  mix with the headset's chat wheel, the dial on Home, or keyboard shortcuts
  that work inside games.
- **More than one chat app at once.**
- **A parametric equaliser** for the game bank. Up to four adjustments,
  each a frequency, a boost or cut and a width, fitted to the headset's ten
  bands. The plot shows the curve you drew and what you will actually hear.
- **A test tone** for finding frequencies that stand out. Sweep or hold a
  frequency, then cut or boost right there.
- **Preset tools:** start a new preset from flat, duplicate one, and Neap
  remembers which preset an edited curve came from, across restarts.
- **High-resolution audio, kept.** Whenever an app starts using the
  microphone, the Charging Dock drops out of high resolution and its status
  ring turns white. It stays that way after the call, even though Windows
  still says 24-bit/96 kHz. Neap restores it a moment later, and the ring
  turns purple again, including after something only touches the
  microphone for a second, such as passing through Neap's own microphone
  meter. While a game is full-screen it waits, so the game's sound is never
  restarted under it, and turns the ring purple once the game has gone.
  There is a switch for it on the Device page.
- **Says what is actually happening.** Connected, no sound, settings out of
  reach, switched off, out of range: each is its own state, with what still
  works and the one thing to do.
- **Catches Windows sending sound to the wrong place.** When sound, calls or
  the microphone are on a device the headset is not listening to, Neap
  names the right one. The Charging Dock, the USB Transmitter and the
  headset's USB-C cable all work, alone or together.
- **A microphone meter** that shows what the other person hears. It uses
  the microphone only while its page is on screen and Neap is in front,
  and lets go of it as soon as you move on or switch to another window,
  however quickly.
- **The audio format** for the headset and the microphone, set from inside
  the app.

### Also in Neap

- **Profiles.** Save the headset's current setup — noise control,
  Superhuman Hearing, the noise gate, AI noise reduction, mic monitoring,
  both equaliser presets, spatial sound, and the Mode button, dial and
  auto-off settings — under a name, and switch back to it in one move from
  the bar under the title bar. A profile that no longer matches what's
  applied shows as edited until it's saved or you switch away. A preset a
  profile remembers, deleted since it was saved, is named rather than
  silently skipped. A profile can also be given one or more apps, from its
  row in Settings, chosen from what is making sound or by browsing for a
  program's file. It comes on when one of them starts, and a default
  profile, set in Settings, comes back when it closes; tabbing out of a
  game doesn't switch anything. A switch that would lose unsaved changes
  waits until they are saved or discarded, and says so in the bar. Switching away from a profile with unsaved changes asks
  first, and while the headset is off the bar says so instead of failing.
- **Spatial sound**, as a tile on Home and on the Audio page: off, Windows
  Sonic, and Dolby Atmos where it's installed. The Stealth Pro II carries a
  Dolby Atmos
  licence; Neap says what activating it takes, which is the headset's Dolby
  Atmos driver (installed by Swarm II) and the Dolby Access app. While
  Dolby Atmos is on, Neap says to turn off the equaliser in the Dolby
  Access app, so it doesn't double up with the headset's own. Choosing a
  format follows the headset from one transmitter to the other, since
  Windows otherwise keeps it on the transmitter it was chosen on. Changing
  it from Sound settings itself, while Home is open, is picked up straight
  away, and a format Neap does not recognise is named as such rather than
  shown as nothing chosen.
- Every setting the headset exposes: noise cancellation, both ten-band
  equalisers, microphone, noise gate, Superhuman Hearing, button and dial
  assignment, transmitter lighting, battery, power.
- Equaliser presets stored on the headset, for both banks, so they also
  appear in the phone app and survive uninstalling everything.
- Microphone mute that follows the boom arm and mutes in Windows at once.
- The paired transmitters: which is which, its firmware, and which is in
  use.
- Runs from the notification area with the mix still working, shows the
  headset's state and battery on the tray icon, and can start with Windows
  without opening a window.
- No driver, no Turtle Beach software, no admin rights.
- Works from the keyboard and with screen readers, in light, dark and high
  contrast themes.

### Left out on purpose

- **Firmware updates and pairing transmitters.** Keep Swarm II installed for
  these, but stop it starting with Windows. The two apps fight over the
  headset when both are open.
- **The Waves audio driver.** Neap's mix does not need it.
- **A parametric microphone equaliser.** The microphone keeps its ten bands.
- **Calls in a web browser** can't be part of the mix, because the browser
  also carries game and media sound. That would need a virtual audio cable,
  which Neap does not provide.
- **Other Turtle Beach headsets**, for now. The Stealth 600 Gen 3, 700 Gen 3,
  500 and Atlas Air share the Stealth Pro II's platform. Each needs an owner
  to test with.

### Not possible

- Changing what the lights do. Only their brightness can be set.
- Remapping the Bluetooth button, or switching Bluetooth from the PC.
- Hiding Windows' volume pop-up when the headset's volume wheel turns.
