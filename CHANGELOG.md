# Changelog

What Neap does and how that changes, in plain words, newest first. It is
the source for release notes and for the project page. [FINDINGS.md](FINDINGS.md)
holds the evidence behind each claim, and the commit history holds every
change in detail.

Each change a person could notice gets a line under **Unreleased**, in the
section it belongs to. At a release, Unreleased takes the version number
and date.

## Unreleased

### Added

- Preset names on the Equaliser page stay on one line, and the gap between your curve and what you will hear is a calm grey with a legend entry.
- The equaliser curve grows with the window, and both curves show a dB scale down the left edge.
- The Neap mark and name sit at the top of the menu, above the profile switcher. When the menu is icons only, just the mark shows.
- A max loudness boost on the Audio page raises the Stealth Pro II's own level,
  so everything through the dock plays louder — for when the headset is too
  quiet even at full volume. It starts at +0 dB until you move it, is saved on
  the headset, and is for the Stealth Pro II only: other headsets don't show it.
- An app given to a profile can switch to it only while it's using the
  microphone, rather than whenever it's open. A profile for Teams calls comes
  on as a call starts, and the default comes back when it ends.
- Neap recognises the Turtle Beach Atlas Air, finds its speakers and
  microphone, and shows only what it has: no chat wheel, noise cancellation,
  lights, Charging Dock or CrossPlay. Its settings can be changed, in testing
  with an owner of one.
- On the Atlas Air, Superhuman Hearing and voice prompts use the places
  Swarm II keeps them, rather than the Stealth Pro II's. In testing with an
  owner of one.
- The game and chat mix on Home links to its keyboard shortcuts.
- A recording lists each change Neap sends to the headset beside what the
  headset says, and says why when a change wasn't sent.
- What's new is always in Settings, beside Neap's version. With no update
  waiting, it shows what's new in the version you have, without going online.

### Changed

- On the Equaliser page, Discard shows only once the curve has been edited, and a saved preset's delete button shows only on the row you point at, focus or have chosen.
- Home is redesigned: the headset's readings across the top, Game and chat
  as one card with a Keyboard shortcuts link, and tiles that show how they
  work, such as a switch for the microphone, Off, Cancelling and
  Transparency for noise cancellation, and a preset choice on the equaliser.
- Audio holds what you hear and your microphone in two sections, with noise
  cancellation, Superhuman Hearing and spatial sound as choices you can see
  at once, and a link from each section to its equaliser.
- The Equaliser page lists your presets beside a larger curve, with Game and
  Microphone as tabs and the test tone folded away until it is wanted.
- Clearer words: Noise cancellation, Strength, Noise reduction, Loudness
  boost and Microphone monitoring, and every failure says what to do next.
- The profile switcher sits at the top of the left menu, and becomes one
  profile icon when the menu shows icons only.
- The equalisers have a page of their own, Equaliser, with a Game and a
  Microphone tab. Home's equaliser tile opens it.
- The microphone settings are on the Audio page, and Microphone has left the
  menu.
- A stronger look: an ultraviolet accent in both themes, and in the dark
  theme a deeper, single ground behind everything.
- In a small window, such as the Steam Deck's, text, rows and the menu step
  up a size so they are easier to read and touch.
- The app icon is redrawn as a split half moon over one wave, and the icon by
  the clock is its own simpler drawing that follows a light or dark taskbar
  on Windows.
- A calmer, more compact look: neutral grey backgrounds in place of the
  violet-tinted ones, flat panels without gradients or outlines, smaller
  text and tighter rows, so more of each page fits on screen. Neap's violet
  now marks only what is on, chosen or playing.
- Text uses the Inter typeface, carried by the app, so it looks the same on
  Windows and Linux.
- The menu on the left has a softer chosen-place highlight, and shows a ring around
  the place the keyboard is on. Pages sit slightly closer to the edges.
- Cards, rows and tiles are flat, and tiles are shorter. In high contrast,
  cards, rows and tiles have an outline so their edges stay visible.
- Buttons, switches, boxes and sliders are more compact, switches and boxes
  are easier to see when off, and the mute button follows the theme,
  including high contrast.
- The game and chat dial is flatter and a little smaller, and its keyboard
  focus ring is easier to see in the light theme.
- Dialogs and the update banner share the app's spacing, a dialog's main
  button uses the accent colour, and long dialog headings wrap instead of
  being cut off.
- Every page uses the same even spacing. On Home the headset summary and the
  quick settings sit on cards like the mix, the loudness boost on Audio takes
  half the height, and the Profiles page puts New profile beside its title.

### Fixed

- A headset with no name of its own now shows its own model name. An Atlas Air
  without a given name used to show as "Stealth Pro II"; a headset Neap doesn't
  recognise now shows simply as "Headset".
- On the Atlas Air, the master volume no longer jumps to 100% and back as it
  is moved, from the slider or the headset's wheel.
- The volume and microphone sliders no longer jump back while they are moved.
  The headset reports each level a moment late, and a late report pulled the
  slider back, then forward again. They now leave the headset's reports alone
  for a moment after a change.
- On the Atlas Air, Bluetooth says whether the headset is listening over it.
  It used to say "Connected" on the transmitter and "Not connected" on
  Bluetooth.
- On the Atlas Air, profiles no longer stay on "Reading the headset's
  settings…". They wait only for settings the headset has.
- A profile saved on one headset no longer shows as changed on another that
  lacks some of its settings, and saving over it keeps them.
- Mic monitoring is hidden on the Atlas Air until a tester finds where it is
  kept. The setting Neap was changing turns on and off with the mute instead.
- What the headset reports, under Device, can be selected and copied, and
  a Copy button puts all of it on the clipboard.
- Settings and recordings show a test build's full version, such as
  0.2.1.1, rather than the release it follows.
- When a transmitter opens but the headset gives no answer, the log and any
  recording say so, rather than leaving "Quiet" unexplained.
- When a transmitter opens and the headset gives no answer, the log says what
  was tried, and the first ask after Neap starts waits longer, in case the
  answer is only slow.
- Neap no longer changes the settings of a headset it doesn't know, as it
  always said it wouldn't. It only reads them.
- A recording of another headset now says which functions the headset didn't
  answer about, rather than counting them as not found, and asks it again
  before giving up.
- A link in What's new sits in line with the words around it, rather than
  above them.
- A recording keeps going for well over an hour. It notes a sound level when
  it changes rather than every second, which filled it after about 37
  minutes.
- A recording made on Linux names Linux and its version, rather than calling
  it Windows, and says once rather than every second that it can't yet tell
  where sound is going.

## 0.2.1 — 2026-10-08

**A small fix.**

### Fixed

- The beep when the game and chat mix reaches the centre plays again on Windows.
  In 0.2.0 it went silent.
- The beep is no longer missed when the chat wheel is turned quickly. Turned
  through the centre at any speed, the mix now stops there for half a second
  and beeps; keep turning and it carries on.
- Turning the chat wheel on past the centre no longer sends the mix racing
  ahead of the wheel.
- On Windows, the beep is no longer lost after the headset has been quiet for
  a few seconds. It plays at once while anything else is playing, and a third
  of a second late after a quiet spell.
- What's new wraps its text, so no line is cut off at the edge, and leaves
  out the install steps, which are for someone downloading Neap by hand.

## 0.2.0 — 2026-10-08

**Neap now runs on Linux, and updates itself.**

### Highlights

- **Linux.** The same app, with the same pages, colours and words, on Windows
  and on Linux. Used with the real headset on a Steam Deck in desktop mode;
  other desktops have been run only against a pretend headset.
- **Help bring Neap to your headset.** Own another Turtle Beach headset? The
  Stealth 600 Gen 3, 700 Gen 3, 500 and Atlas Air share the Stealth Pro II's
  platform, and support for them can start from a ten-minute recording made in
  Neap. No technical knowledge needed.
  [Here's how](https://github.com/lykosapps/Neap/blob/main/docs/MAPPING.md).
- **Updates itself on Windows.** A banner tells you when a newer version is
  out, and one button installs it and keeps your settings.

### Updating

- **From 0.1.0:** quit Neap from its icon by the clock, then unzip this
  version over your Neap folder. Your settings and profiles are kept.
- **From 0.2.0 on, Windows:** choose **Update and restart** in the banner or in
  Settings.
- **From 0.2.0 on, Linux:** Neap opens the download page; download the new
  file in place of the old one.

### New on Linux

- **One file.** An AppImage: make it runnable, and run it. It works on any
  64-bit distribution with PulseAudio or PipeWire, and each release has a
  `.sha256` to check it with.
- **Allow the headset once.** If Linux keeps the headset from Neap, every page
  says so and **Allow access** asks for your password once.
- **Keyboard shortcuts for the mix** and **Start when you sign in**, the same
  as Windows. Shortcuts work in X programs, which is most games.
- **Not yet:** Gaming Mode on the Steam Deck (use desktop mode), spatial
  sound, and the audio format.

### Help support another headset

- **Record it in Neap.** In Settings, under Problems and other headsets,
  choose Start next to Record headset activity, then work through the steps
  the ? button lists.
- **See what Neap found,** before you send it: what it recognised on your
  headset, what it didn't, and what changed while you recorded.
- **Send it from Neap.** Report on GitHub opens a form with the details filled
  in and the recording ready to drag in. You'll need a free GitHub account.

### Updates

- **Neap updates itself** on Windows. A banner says a newer version is out,
  with **What's new** to read the notes in Neap, **Update and restart**, and
  **Not now** to put it off for a day or skip that version. On Linux it
  opens the download page.
- **If the new version doesn't open,** Neap says so and offers the download
  again, instead of leaving you with no Neap and no word why.
- **Only a version check.** Neap asks GitHub once a day for the latest
  release and sends nothing from your PC or your headset. Turn it off in
  Settings to keep Neap offline.

### Profiles and Settings

- **Profiles have their own page.** Save the headset's settings under a name,
  choose which apps bring a profile on, and pick the default. Switching is
  still in the bar at the top of every window.
- **Settings is tidier.** Neap's version and whether it is up to date come
  first, then Start when you sign in, then Problems and other headsets, which
  was called Diagnostics.

### Fixed

- **Choosing Off for noise control stays off** when the Mode button is set to
  cycle through all three modes.

## 0.1.0 — 2026-10-04, the first release

A Windows app for the Turtle Beach Stealth Pro II, in place of Swarm II.
No driver, no Turtle Beach software, no admin rights.

### Game and chat mix

- **Nothing to install.** Pick the apps that carry chat, such as Discord
  or Teams, and Neap turns them against everything else. No virtual audio
  cable, driver or reboot.
- **Works on the Xbox edition.** Swarm II's mix relies on the Waves driver
  and isn't officially supported there.
- **Three ways to move it:** the headset's chat wheel, the dial on Home, or
  keyboard shortcuts that work inside games.
- **More than one chat app at once.**

### Noise control

- **Transparency.** Choose noise cancellation, transparency or off.
  Transparency is noise cancellation at zero, which lets the room in.
- **The Mode button steps through all three** while Neap is running.

### Sound

- **Parametric equaliser** for game audio: up to four adjustments, fitted
  to the headset's ten bands, with a plot of what you'll actually hear.
- **Test tone.** Find a frequency that stands out, then cut or boost it on
  the spot.
- **Presets stored on the headset**, so they also appear in the phone app.
  Start one from flat, or duplicate one.
- **Spatial sound:** Windows Sonic, or Dolby Atmos where it's installed.
- **High-resolution audio, kept.** The Charging Dock drops out of high
  resolution whenever an app uses the microphone. Neap puts it back
  afterwards, waiting until a full-screen game has closed.

### Every day

- **Profiles** save your whole setup under a name, and can switch on by
  themselves when a game starts.
- **Clear status.** Connected, no sound, switched off or out of range, each
  with what still works and the one thing to do.
- **Catches the wrong device.** When Windows sends sound or the microphone
  somewhere the headset isn't listening, Neap names the right one.
- **The spare battery's charge** while the headset is on the Charging Dock.
- **Settings Swarm II's desktop app doesn't have:** wake on motion and voice
  prompt volume.
- **Every other setting** the headset has: microphone, noise gate,
  Superhuman Hearing, buttons and dial, lighting, power and audio format.
- **A microphone meter** that shows what other people hear.
- **Microphone mute follows the boom arm**, and mutes in Windows at once.
- **Runs from the notification area**, with the battery on the tray icon,
  and can start with Windows.
- **Works from the keyboard and with screen readers**, in light, dark and
  high contrast themes.

### Not included

- **Firmware updates and pairing.** Keep Swarm II for these, but don't run
  both at once.
- **Other Turtle Beach headsets**, for now. Tested on the Xbox edition; the
  PlayStation edition should work but hasn't been tried.
- **Calls in a web browser** can't join the mix, because the browser also
  carries game sound.
- **A parametric microphone equaliser.** The microphone keeps its ten bands.
- **Not possible on this headset:** changing what the lights do, remapping
  the Bluetooth button, or hiding Windows' volume pop-up.
