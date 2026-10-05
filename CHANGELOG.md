# Changelog

What Neap does and how that changes, in plain words, newest first. It is
the source for release notes and for the project page. [FINDINGS.md](FINDINGS.md)
holds the evidence behind each claim, and the commit history holds every
change in detail.

Each change a person could notice gets a line under **Unreleased**, in the
section it belongs to. At a release, Unreleased takes the version number
and date.

## 0.2.0 — 2026-10-05

**Own another Turtle Beach headset? Help bring Neap to it.** The Stealth
600 Gen 3, 700 Gen 3, 500 and Atlas Air share the Stealth Pro II's
platform, and support for them, and for others, can start from a
ten-minute recording made in Neap. Nothing else to install, and no
technical knowledge needed.
[Here's how](https://github.com/lykosapps/Neap/blob/main/docs/MAPPING.md).

Reporting a problem gets easier too: record what your headset and Windows
do while it happens, and send it to GitHub from Neap.

To update from 0.1.0, quit Neap from its icon by the clock, then unzip
this version over your Neap folder. Your settings and profiles are kept.
From this version on, Neap updates itself.

### Help support another headset

- **Record it in Neap.** In Settings, under Problems and other headsets,
  choose Start next to Record headset activity. The ? button beside it lists the steps: a
  few buttons and dials to work through, counting to five between each.
- **See what Neap found.** Before you send it, Neap shows which of its
  functions it found on your headset, which it didn't, and which changed
  while you recorded.
- **Send it from Neap.** Report on GitHub opens a "Support my headset" form
  with the details filled in, and the folder with your recording ready to
  drag in. You'll need a free GitHub account.
- **Going further.** For settings only Swarm II can change, such as the
  equaliser, the guide shows how to record Swarm II as well. Optional, and
  more technical.

### Updates

- **Neap updates itself.** It asks GitHub once a day for a newer version
  and says so with a banner across the top of the window, and with a
  notice by the clock if the window is closed; selecting the notice opens
  Settings. Update and restart downloads the new version, checks it is the
  published file, puts it in place and starts it again. Your settings and
  profiles are kept.
- **Read what's new first.** What's new, in the banner and at the top of
  Settings, shows the release's notes in Neap, without opening a web page.
- **Put the banner away.** Not now in the banner holds Remind me tomorrow,
  which hides it for a day, and Skip this version, which hides it until a
  newer version is out. Updating is still there in Settings either way.
- **The banner fits the window.** In a narrow window it stays one row, with
  What's new and Not now in a menu beside Update and restart. While an
  update downloads, a progress bar shows how far it has got and Update and
  restart is off. A thin coloured edge on its left tells it from the
  profile selector below.
- **Only a version check.** Neap sends GitHub the request and nothing
  else, nothing from your PC or your headset. GitHub sees the request, as
  any website does. Turn off Check for updates automatically to keep Neap
  offline, and use Check now whenever you like.
- **Where Neap can't update itself,** such as a folder you can't write to
  without being an administrator, Settings says so and links to the
  download.

### Profiles and Settings

- **Profiles have their own page.** Between Controls and Device in the
  list on the left: save the headset's settings as a new profile, rename or
  delete one, choose which apps bring it on, and pick the default. Switching
  is still in the bar at the top of every window.
- **Settings is tidier.** Neap's version and whether it is up to date come
  first, with What's new and Update and restart beside them. Then Start with
  Windows, then Problems and other headsets, which was called Diagnostics.
- **The update banner is at the very top,** above the profile selector,
  with a coloured edge so it reads as news.

### Reporting a problem

- **Record headset activity.** Start a recording, make the problem happen,
  then stop. Neap saves a text file to Downloads with what the headset and
  Windows reported meanwhile. It only listens, and leaves out the
  headset's serial number and radio addresses.
- **Report on GitHub.** One button opens a GitHub form with Neap's version,
  Windows' version and the headset hardware filled in, and opens the
  folder with the recording ready to drag in.

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
