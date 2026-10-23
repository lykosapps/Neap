# Working on Neap

Neap is an app for Windows and Linux (Avalonia, .NET 10, C#) that controls the
Turtle Beach Stealth Pro II headset in place of Swarm II. It is open source, GPL-3.0-or-later.
Every change is held to the standard below: work as a senior .NET engineer
preparing code for experienced reviewers.

## Project profile

- **Team:** solo. Small commits straight to main, no pull requests.
- **Autonomy:** commit and update the owner's try-it copy without asking; never push, publish or delete without asking.
- **Accessibility target:** WCAG 2.2 level AA.
- **Languages:** English only, written so it can be translated.
- **Data and privacy:** no personal data kept, no analytics or tracking, nothing from the PC or the headset is sent anywhere. The one network call is the update check: a plain request to GitHub for the latest release, which sees the PC's internet address as any website does, and the download when asked. It can be turned off.
- **Voice and brand:** Tenon's defaults: plain and restrained, no help text by default.

## The standard

- **Conventions.** Microsoft C#/.NET and Avalonia practice. Analyzers at
  latest-recommended, warnings as errors, `dotnet format` clean. No code that
  reads as generated: no dead code, no duplicated logic, nothing clever where
  plain will do.
- **Simplicity first.** Choose the simplest implementation that correctly
  solves the problem. No abstractions, helpers, design patterns or extra
  configuration unless strictly necessary. Use a built-in feature or library
  before writing custom logic. No clever tricks, no premature optimisation.
  If an extra abstraction or feature is truly needed, say why before adding it.
- **Decisions live in Core.** Anything with a rule in it (connection state,
  routing, what a screen shows for a state, curve or dial maths) goes in
  `Neap.Core` with xUnit tests. The app's screens only draw what Core
  decides. `StatusLook`, `RoutingCheck`, `TransmitterList` and `LinkTracker`
  are the pattern.
- **Every write goes through the registry.** `Settings/Registry.cs` is the
  list of confirmed settings; the client refuses anything else. Never write a
  transmitter slot's base address.
- **A control the headset reports is a control Neap offers.** When a change
  starts reading a new key so a button or switch on the headset shows in
  Neap, test the write for Neap's own control on the headset in the same
  change and ship them together. It becomes writable only once that test
  passes.
- **On screen means in a window.** Work that finishes after a wait (opening
  a microphone or a tone, reading the headset) checks `InWindow()` before it
  uses the result, never `IsLoaded`, which can read false for a moment as a
  page opens while the control is showing.
- **Text lives in the resource file.** All interface text is in
  `Strings/en-US/Resources.resw`, through `loc:Uid.Value` in AXAML and `Strings`
  in code. Whole sentences, never fragments joined in code. The tests check that
  every key exists and is used.
- **Comments.** `<summary>`: one or two sentences on what. `<remarks>`: why,
  in the present tense. No history ("used to", "the first version"), no `<b>`
  tags, nothing that restates the code.
- **Accessibility as built.** Every control has a name for UI Automation,
  everything works from the keyboard, text meets 4.5:1, and light, dark and
  high contrast themes all work, including a theme change while a page is
  open.
- **Every headset has a tracker.** Support for each headset is tracked in
  `docs/headsets/`, one file per model, in the format its `README.md` sets
  out: Turtle Beach's feature list, what Neap does with each feature, and how
  sure we are on Windows and on Linux. Update it in the same commit as any
  recording, tester reply, finding or test build for that headset.
- **Nothing fails silently.** A refused write, a missing resource or an
  unavailable device either leaves a line in the app log or says so on
  screen. A parser that only matches what it expects turns "cannot read" into
  "does not exist"; treat anything unparsed as a failure.

## Verifying

- **Test the build that ships.** Build in Release, run the tests, publish,
  relaunch the published copy. A Debug build proves nothing about Release.
- **Check by running, not by reading.** Drive the app through UI Automation
  (injected clicks do not reach the window's content) and take screenshots. When
  reporting, say which findings were confirmed by running and which were not.
- **Screen checks go through `tools/ui/run.ps1`.** `run.ps1 banner`, `notes`,
  `settings` or `profiles` publishes if a source file is newer than the build,
  then runs the check from outside the package; `all` runs the four in about
  a minute and a half.
  Logs and pictures land in `TestResults/ui`. Only one practice run of the app
  can be open at a time, so run one check at a time. A screen that is off
  captures as pure black, and the screen turns itself off after five idle
  minutes, so a check that stops saying the screen is off needs rerunning
  with the screen on. The check judges this from the app's own window, not
  the desktop, which can be black with the screen on. A new check is a script
  beside it that uses `Ui.ps1`. `run.ps1 access` walks every page in both
  themes for names, target sizes, Tab order, a visible focus mark and text
  contrast in about a minute and a half. It moves focus by asking the
  practice run over its pipe to do what Tab does, so the window stays behind
  and the keyboard is left alone, and it can run while the owner works; that
  a real Tab press reaches the app is check 12 of "Before a release" in
  `CONTRIBUTING.md`. Colour contrast is also a test
  (`ThemeContrastTests`), so a colour change is caught by every build.
  `run.ps1 atlas` checks the
  pretend Atlas Air. `looks.ps1` has the app draw every page in dark,
  light and high contrast for a person to look over; it opens the app behind
  other windows without taking the keyboard, needs no screen, and can run
  while the owner works. Every other check runs through
  `tools/pretend-quiet.ps1 -Run '<check> -Inside'` on a desktop of its own,
  such as `-Run 'tools\ui\run.ps1 notes -Inside'`, so it never waits for the
  owner to be away, `all` included. Menu items that answer only Enter are
  pressed inside the practice run over its pipe (`press NAME`), and `mini`
  opens the mini panel there, so no key is sent. Two parts still need the
  screen: part E of `banner`, as the system reports no focus for a window on
  a desktop of its own (it skips itself there), and part K of the headset
  checks, the mix's keyboard shortcuts. Those wait until the owner says they
  are away. `run.ps1 mini` checks the mini panel: its tiles' lists, a choice
  from each, and Open Neap.
- **Before tagging, run `tools\release-check.ps1`.** It reads, and changes
  nothing: main checked out and clean, in step with GitHub both ways, the
  unpushed commits scanned for anything identifying, the version, changelog
  date and Unreleased section agreeing, no tag yet, and the backlog's items
  for before the tag.
- **Size the check to the change.** Notes and docs need only the build. A
  rule in Core needs its tests, and the check for its page if what the page
  shows changes. One screen needs that screen's check. What every screen
  shares (startup, navigation, themes, `Ui.ps1` and `run.ps1`) needs `all`.
  A release needs `all`, `access`, `tools/pretend.ps1` and the checks on the
  headset. Run a check of a minute or more in the background.
  `tools/pretend.ps1` takes about three minutes and comes to the front, as
  UI Automation's select, toggle, set value and expand all bring the window
  forward. `tools/pretend-quiet.ps1` runs it on a desktop of its own, so it
  interrupts nobody and can run while the owner works; it stops at once if a
  practice window ever shows on the owner's desktop. Its Windows steps still
  change the PC's volume, mute and audio format for a moment. With `-Run` it
  runs the headset checks that send no keys the same way, such as
  `-Run 'tools\ui\run.ps1 hardware -Parts BLT -Inside'`; with no tray on that
  desktop, the sign-in check's "opens no window" step fails there, so check it
  on the owner's desktop. It is known to
  trip on timing in a different place some runs, where it looks for a control
  once rather than waiting for it; read each failure before calling it a bug,
  and rerun.
- **Sample, do not snapshot.** One reading after the fact shows where a value
  settled, not whether it moved.
- **Read the app's folder from outside Claude.** The desktop app runs
  Claude's tools in a package, and Windows gives a package its own copy of
  `%LOCALAPPDATA%`. Reading the log from a tool shows that copy, and a Neap
  started from a tool writes to it, so a copy started any other way seems to
  log nothing. Read the real folder through a process Windows starts outside
  the package, such as `Win32_Process.Create`.
- **The headset is the last word.** Anything that changes how the app behaves
  with the hardware goes on a list of checks to run with the owner. It is not
  done until those pass. A transmitter answers from memory for a headset it
  can no longer reach, so prove which device answered.

## Commits

- Small commits, each building and passing its tests, each message saying
  what changed and why.
- Anything a person could notice gets a line in `CHANGELOG.md` under
  Unreleased, in plain words, in the same commit.
- Author `lykosapps <224482331+lykosapps@users.noreply.github.com>`, with
  times in UTC and no Co-Authored-By trailer. A local hook refuses anything
  else; fix the commit, never skip the hook.
- Never push, and never rewrite history, without being asked.
- Nothing that could identify the owner, anywhere: code, docs, commit
  messages, or screenshots committed to the repo.

## Commands

```
dotnet build Neap.sln -c Release
dotnet test --project tests/Neap.Core.Tests -c Release
dotnet format Neap.sln --verify-no-changes
dotnet publish src/Neap.Desktop -c Release -r win-x64 -f net10.0-windows10.0.19041.0 --self-contained
```

## Working notes
Claude working notes for this project are kept privately by the owner, outside this repository (`claude-profile`, `projects/neap/knowledge/INDEX.md`). Read them first when available; never add them here.
