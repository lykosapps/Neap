# Changelog

What Neap does and how that changes, in plain words, newest first. It is
the source for release notes and for the project page. [FINDINGS.md](FINDINGS.md)
holds the evidence behind each claim, and the commit history holds every
change in detail.

Each change a person could notice gets a line under **Unreleased**, in the
section it belongs to. At a release, Unreleased takes the version number
and date.

## Unreleased — 0.1.0, the first release

### Beyond Swarm II

- **Settings Swarm II's desktop app does not have:** wake on motion and
  voice prompt volume. Both were found through the phone app.
- **Transparency.** The headset has no transparency mode, but noise
  cancellation at zero lets the room in. Neap offers noise cancellation,
  transparency and off as three modes, and the Mode button can step
  through all three while Neap is running.
- **A game and chat mix with nothing to install.** Pick the apps that
  carry chat, such as Discord or Teams, and Neap turns them against
  everything else. No virtual audio cable, driver or reboot. Swarm II's mix
  relies on the Waves driver, which is broken on the Xbox edition. Move the
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
  meter. There is a switch for it on the Device page.
- **Says what is actually happening.** Connected, no sound, settings out of
  reach, switched off, out of range: each is its own state, with what still
  works and the one thing to do.
- **Catches Windows sending sound to the wrong place.** When sound, calls or
  the microphone are on a device the headset is not listening to, Neap
  names the right one. The Charging Dock, the USB Transmitter and the
  headset's USB-C cable all work, alone or together.
- **A microphone meter** that shows what the other person hears. It uses
  the microphone only while its page is on screen, and lets go of it as
  soon as you move on, however quickly.
- **The audio format** for the headset and the microphone, set from inside
  the app.

### Also in Neap

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

### Fixed before release

- The Charging Dock's two light sliders had each other's names.
- Equaliser bands are sent in the headset's half-decibel steps, so what
  is shown is what is set.
- The preset list names the preset you chose, not the last one the headset
  reported.
- A chat wheel reading far from the last one is held until the next reading
  confirms it, so the mix no longer jumps.
- Open and Quit on the tray menu work.
- A failure to list recording devices is reported instead of closing the
  app.
- The equaliser's plots follow a switch between light and dark while Neap
  is open, rather than keeping the colours of the theme it started in.
- A tile that can't be changed, such as the microphone while the boom arm
  mutes it, keeps its state readable in the light theme instead of fading
  to faint grey.
- A greyed-out Save in the preset naming dialog shows its label in the light
  theme.
- A band's menu offers to revert the band, from the keyboard (Shift+F10) or
  a right-click, instead of only cut, copy and paste.
- A lit tile's state, such as "On" under Microphone, is easier to read: the
  tile's purple is a shade deeper in the light theme and a shade lighter at
  its foot in the dark one.
- A setting changed while the equaliser's presets are loading takes effect
  in under a second, rather than waiting up to four.
- Tighter, more consistent wording: keyboard shortcuts, assignable and
  fixed controls, slot counts, refreshing the chat app list, and one way of
  saying couldn't, isn't and won't.
- Spacing and type follow Windows' own grid and type sizes throughout, and
  changing numbers (levels, decibels, frequencies, battery) keep their
  width, so nothing beside them shifts as they change.
- Surfaces match: the tiles take the panels' lavender tint and 16px corners
  in the light theme, and cards, controls and dividers share one border
  colour each.
- Settings sit where you'd look for them: auto shut-off and wake on motion
  on the Device page, voice prompts beside the volume on Audio, and the
  mix's keyboard shortcuts with the headset's controls on Controls.
- Superhuman Hearing has a tile on Home, so it can be switched on mid-game
  in one click.
- Home's equaliser tile says when the curve is parametric, marks
  parametric presets in its list, and ends the list with a way to the
  equaliser.
- The equaliser uses the whole width of its panel. Its presets open from
  the preset's name, as they do on Home, so at the usual window size the
  parametric plot, adjustments and sliders fit without scrolling, the test
  tone's Cut here and Boost here sit on its first row, and nothing is cut
  off in a narrow window. The ten values sent to the headset fold away
  under the sliders.
