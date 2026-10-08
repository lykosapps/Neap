# Changelog

What Neap does and how that changes, in plain words, newest first. It is
the source for release notes and for the project page. [FINDINGS.md](FINDINGS.md)
holds the evidence behind each claim, and the commit history holds every
change in detail.

Each change a person could notice gets a line under **Unreleased**, in the
section it belongs to. At a release, Unreleased takes the version number
and date.

## Unreleased

### Fixed

- The beep when the game and chat mix reaches the centre plays again on Windows.
  In 0.2.0 it went silent.

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
