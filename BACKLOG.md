# Backlog

Bugs, features and work still to do. When something is done, its line goes,
and the commit that did it says so. Each line says what is wrong or wanted,
not how to fix it. Items needing the headset say so. When the project moves
to GitHub, these become issues.

## Bugs

- **The mic button fights the boom arm.**
  - With the arm down (unmuted), the app's mute button must still mute.
  - With the arm up (muted), the app cannot unmute, so its button should be
    disabled.
- **Started at login, Neap writes nothing to its log.** On 25 September the
  copy Windows started at login ran normally for almost an hour but logged
  nothing, not even that it started; the same build started by hand logs as
  usual. The log swallows its own failures, so why it cannot write is not
  recorded either.
- **Muting just after opening a page is slow.** A press waits about four
  seconds behind the page reading its presets, because every request to the
  headset queues in one line.
- **Setting option names are English only.** The Mode button's, the dial's
  and Superhuman Hearing's option names are written into the code, not the
  resource file, so they can't be translated.
- **The mix once moved to 76% with nobody touching it.** Not seen since. The
  chat wheel jump fix may have been the cause; check the log if it recurs.
- **A screen-test check failed once and never again.** A "sends nothing back"
  check. The test now names the control, so a repeat will say which.

### Known limits, recorded rather than fixed

- With both transmitter lights amber, no sound can still show as connected.
- At power-on, a transmitter's stale answer can show the wrong transmitter
  for about 16 seconds.

## Features

- **More than one chat app.** The chat side takes one app. Planned: an
  expandable card with a checkbox per app.
- **Profiles.** Save and apply a set of settings, switch automatically when
  an app starts, and switch with hotkeys.
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

## Local housekeeping

Planned for the end of the 25 September session, with Neap closed:

- Gather everything in one `neap` folder: the project, the daily build and
  the archive, named to convention.
- Remove whatever is no longer used, once it is confirmed unused or backed
  up.
- Point start with Windows, and the session notes, at the new places.
- Remove the old branches and worktrees; all are merged or backed up.
- Remove the local git exclude rule that hides new audio folders.
- Clear about 840 MB of old raw captures and engine leftovers from the
  working folder. Git ignores them.

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
