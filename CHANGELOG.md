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
- A profile saved before the equalisers had been read no longer shows
  "Edited" straight after switching to it, and the first switch after
  opening Neap takes a second, not about eight.
- The profile list no longer closes by itself while it is open.
- Choosing the headset's format on the Device page takes effect even with
  nothing playing. Before, Windows only noted the choice, and the page showed
  a change that hadn't happened.
- A format Windows won't switch to, such as 16 kHz for the microphone while
  apps are using it, now says so, and the page goes back to the format the
  device is really running at.
- The Device page picks up a format changed in Sound settings once Neap is
  back in front.
- The Profiles list in Settings is no longer rebuilt every time the headset
  reports something, which moved the keyboard off whatever row it was on.
- Switching to a profile, or discarding a change back to one, no longer
  re-reads both equaliser banks first if they were already read: a couple
  of seconds' wait, gone.
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
- Options that appear when something is switched on slide out from under
  it like a drawer, so they read as part of that switch: Superhuman
  Hearing's type and intensity, noise cancellation's blocking, the noise
  gate's threshold and the mix's keyboard shortcuts.
- Master volume sits beside voice prompts, and sensitivity beside mic
  monitoring, instead of each taking a wide, mostly empty row. They stack
  again in a narrow window.
- The menu has a solid background when a narrow window turns it into a
  pop-out, instead of showing the page through it.
- The chat apps a game's mix can draw on were ticked with the same round
  mark as an exclusive choice like noise control, so picking a second app
  looked like it should replace the first. They now tick with a checkbox's
  square mark.
- A slider's reading could run off the edge of a narrow card and disappear
  entirely, such as noise cancellation's blocking strength beside
  Superhuman Hearing on Audio. The reading always stays in view now, and
  the slider is shorter only when its card is too narrow for it.
- The drawer that slides out from under a switch, such as Superhuman
  Hearing's type and intensity or noise cancellation's blocking, now sits
  flush against it and shares its corners, so the two read as one card
  split by a line rather than two stacked boxes.
- Audio puts noise control, Superhuman Hearing and the volume first and
  the equaliser last, as Microphone does, so on a laptop the settings
  changed most are in view without scrolling. Home's way to the equaliser
  scrolls down to it.
- On Audio, master volume has an icon like the rows beside it, and
  spatial sound is a row with its list on the right, like the other
  levels, rather than a full-width button with its list opening far from
  its name. Noise control's title is the size of the other cards' titles.
- In a narrow window the page starts below the menu button, which no
  longer sits against the page's title.
- Choosing Parametric asks first when it would change what you hear,
  instead of flattening the curve straight away.
- In a narrow window the equaliser's ten values move onto two rows of
  five instead of being cut off.
- The equaliser's filled button is Save as new, once there is an edit to
  keep; before that, Duplicate is a plain button. Save is left out for the
  headset's own presets, which can't be overwritten, and on a wide window
  the curve lines up with the preset's name.
- Spatial sound's tile lights up with the accent while it's on, the same
  as every other tile that can be on. It read as off with Windows Sonic or
  Dolby Atmos playing, since it borrowed the equaliser's tile, which is
  never lit because the equaliser is never off.
- Spatial sound on Audio sits properly clear of the volume row above it,
  instead of 4 px beneath it.
- Fixed: asking whether to switch to Parametric could leave both Bands and
  Parametric showing unchosen until the question was answered.
- Superhuman Hearing's type is three buttons, Legacy, Footsteps and
  Gunshots, all in view, instead of a drop-down.
- The profile bar shows a chevron, so "No profile" reads as a menu to open
  rather than a status label, especially before any profile is saved.
- After Save or Discard on the profile bar, the keyboard stays with the
  profile button instead of jumping to the menu toggle in the corner.
- Save and Discard have moved into the profile menu, off the bar itself.
  Before, both sat beside the profile name on every page the moment
  anything it holds changed, which meant being asked to keep or drop a
  change on every tweak, even while just trying things. The "Edited" mark
  alone still says a change hasn't been kept.
- In the profile menu, Save and Discard now share their row evenly instead
  of leaving empty space beside them, Save is the filled button, and
  starting a new profile is a short "New profile" above the list of saved
  ones, not a full sentence below it that sank further down as the list
  grew.
- On the parametric plot, each point sits at its own adjustment's gain, as
  in other parametric equalisers, so stacked adjustments no longer push
  their points off the plot and over the test tone's controls.
