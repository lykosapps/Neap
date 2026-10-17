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
  contrast; it takes about six minutes and the keyboard focus, so it is not
  part of `all` and needs the machine to itself. `run.ps1 atlas` checks the
  pretend Atlas Air.
- **Size the check to the change.** Notes and docs need only the build. A
  rule in Core needs its tests, and the check for its page if what the page
  shows changes. One screen needs that screen's check. What every screen
  shares (startup, navigation, themes, `Ui.ps1` and `run.ps1`) needs `all`.
  A release needs `all`, `access`, `tools/pretend.ps1` and the checks on the
  headset. Run a check of a minute or more in the background.
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

## Working with the owner

These rules travel with the repository, so they apply on any Claude account or
machine. The reasons and the cases behind them are in `knowledge/`.

- **Read first.** At the start of a session, read `knowledge/INDEX.md`, then
  the newest hand-over note in `.claude/` (local only) if there is one.
- **Knowledge is append-only.** Never delete or replace text in `knowledge/`.
  Mark it `SUPERSEDED (date, reason)` and add the new text beside it. New
  durable facts go into `knowledge/` or the project documents, never only into
  local Claude memory.
- **Reply format.** Lead with the answer in bold, in the first sentence. Use
  `###` headers, `---` dividers and bold-lead bullets, put technical terms in
  backticks, and keep the explanations. This replaces the Tenon working
  standard's length and no-paths rules here.
- **Device actions.** Safe reads and commands that don't persist go ahead on
  the real headset without asking. Writes to chip settings or firmware, and
  anything else that persists, need an explicit go-ahead, asked with a 1 to 5
  risk rating, what could go wrong and the expected outcome. Stop the try-it
  copy before opening the control collection, since a second reader steals
  its reports.
- **Delegate.** Hand self-contained work to subagents and always name the
  model: Sonnet by default, Haiku for lookups and checks, and the main session
  for judgement and for checking their work before it is committed.
- **Run long work in the background,** with a time limit and a log file, and
  say in a line what is running.
- **Don't take the screen.** Checks that raise windows wait until the owner
  says they are away. Real launches against the headset follow the safeguards
  in `knowledge/feedback_no_real_launch.md`.
- **Once approved, carry it through,** side steps included. When the owner is
  away, batch anything that needs approval to the very end.
- **Label files.** Say whether each file mentioned is in the repo, local only,
  or outside the project.
- **"Mirror"** in a message means: restate it in cleaner words first.
- **Testers get one-step asks:** one thing they can send whole, with exact
  steps.

Everything else: `knowledge/INDEX.md`.
