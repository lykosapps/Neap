---
name: screen-checks
description: Check a Neap change on screen, sized to what changed, without taking the owner's screen. Use before running any screen check, pretend run, tools/ui/run.ps1, looks.ps1 or tools/pretend.ps1, when a change alters what a page shows, and before changing any on-screen text.
---

# Screen checks

The one place for how Neap's screen checks are chosen and run. The `headset-checks` and `release` skills use it.

## 1. Size it first

- **List what changed:** write down which screens the change touched, then run only their checks and only the parts
  that cover the change. Never a check for a screen that didn't change.
- **Cheapest first:** `dotnet test`, then `dotnet format`, then the one check part, then pictures. Notes and docs need
  only the build. A rule in Core needs its tests, and its page's check only if what the page shows changes.
- **`all` only** for a release or a change to what every screen shares (startup, navigation, themes, `Ui.ps1`,
  `run.ps1`).
- **A failure is fixed and rerun alone,** never the whole check or batch. Read each failure before calling it a bug:
  `tools/pretend.ps1` sometimes trips on timing, looking for a control once instead of waiting for it.
- **Say the time** before a batch of more than two checks. Rough costs: build about 15 s, tests 20 s, publish 40 to
  60 s, `all` a few minutes, `tools/pretend.ps1` about 3 minutes, a release's checks about half an hour.
- **Before changing on-screen text,** search `tools/` for the exact old sentence and update the checks in the same
  change, or a whole walk fails on one line.

## 2. Run it on the hidden desktop

Every check runs through `tools\pretend-quiet.ps1` on a Windows desktop of its own, so it never takes the owner's
screen or keyboard and needs no ask. It stops at once if a practice window ever shows on the owner's desktop. Give it
a command of its own, with nothing before it:

```
powershell -ExecutionPolicy Bypass -File tools\pretend-quiet.ps1 -Run 'tools\ui\run.ps1 notes -Inside'
```

Its Windows steps still change the PC's volume, mute and audio format for a moment.

## 3. What needs the owner

- **Parts that send real keys or must be seen** refuse on the hidden desktop: part B and E of `banner` (the system
  reports no focus there) and part K of the headset checks (the mix's keyboard shortcuts), plus the sign-in check's
  "opens no window" step (no tray there).
- **Ask first, every time,** saying how long it takes; one yes covers one run. Batch these asks to the end of a block
  of work. Until then, verify with build, tests and format, and say plainly that the screen check has not run.

## 4. The tools

- **`tools/ui/run.ps1 <check>`:** the check is the name of a script in `tools/ui`, such as `banner`, `notes`,
  `settings`, `profiles`, `lists`, `mini` (the mini panel's tiles, a choice from each, Open Neap), `overlay`, the
  `parametric-*` checks, `firmware`, `atlas` (the pretend Atlas Air) or `hardware`; `-Parts` picks parts; `all` runs
  every screen check. It publishes if a source file is newer than the build, then runs from outside the package. Logs
  and pictures land in `TestResults/ui`. A new check is a script beside it that uses `Ui.ps1`.
- **`run.ps1 access`:** every page in both themes for names, target sizes, Tab order, focus mark and text contrast,
  in about a minute and a half; `-Headset atlas` uses the pretend Atlas Air. It moves focus over the practice run's
  pipe, so it can run while the owner works. Colour contrast is also a test (`ThemeContrastTests`).
- **`run.ps1 align`:** the alignment audit. It misreads Home's microphone tile (icon above its title) as 2 px off.
- **`looks.ps1`:** draws every page in dark, light and high contrast for a person to look over. It does not publish
  when a source is newer, unlike `run.ps1`. Never use it for one page.
- **One page, one picture:** launch the published app once per theme with that page's flags, for example
  `Neap.Desktop.exe --pretend --behind --page equaliser --parametric --wired --wait 15 --snapshot <file> --dark`, then
  `--light` and `--highcontrast`. Under a minute.
- **`tools/pretend.ps1`:** the full walk of every page and setting. Once per finished change that is wide, or a
  release; never to iterate on one screenshot.
- **Menu items that answer only Enter** are pressed over the practice run's pipe (`press NAME`), so no key is sent.

## 5. Known traps

- **One practice run at a time:** it holds the `Neap.Pretend` pipe. Wait for another run to end; never stop it.
  `NEAP_PRETEND_FOLDER` gives a run its own data folder and log.
- **Black pictures:** a screen that is off captures as pure black, and it turns itself off after five idle minutes.
  The check judges this from the app's own window; rerun with the screen on.
- **Stale compiler server:** if a clean build reports errors that make no sense, run `dotnet build-server shutdown`.
- **Watchers print, they don't write a file:** on Windows a reader following a log holds the file, later writes fail
  and the run goes unrecorded. Make the watcher print each event, and read one event before asking the owner to start.
