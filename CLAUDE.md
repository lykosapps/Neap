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
- **Decisions live in Core.** Anything with a rule in it (connection state,
  routing, what a screen shows for a state, curve or dial maths) goes in
  `Neap.Core` with xUnit tests. The app's screens only draw what Core
  decides. `StatusLook`, `RoutingCheck`, `TransmitterList` and `LinkTracker`
  are the pattern.
- **Every write goes through the registry.** `Settings/Registry.cs` is the
  list of confirmed settings; the client refuses anything else. Never write a
  transmitter slot's base address. Never send anything down the firmware
  update path, and never read a device's flash.
- **Firmware files are for study.** Swarm II's firmware files may be read
  from a copy kept outside the repository. Findings from them go in
  `FINDINGS.md` marked *(research)*; the files themselves are Turtle Beach's
  and are never committed.
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
- **Screen checks go through `tools/ui/run.ps1`.** `run.ps1 banner`, `settings`
  or `profiles` publishes if a source file is newer than the build, then runs
  the check from outside the package; `all` runs the three in about a minute.
  Logs and pictures land in `TestResults/ui`. Only one practice run of the app
  can be open at a time, so run one check at a time. A screen that is off
  captures as pure black, and the screen turns itself off after five idle
  minutes, so a check that stops saying the screen is off needs rerunning
  with the screen on. The check judges this from the app's own window, not
  the desktop, which can be black with the screen on. A new check is a script
  beside it that uses `Ui.ps1`.
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
