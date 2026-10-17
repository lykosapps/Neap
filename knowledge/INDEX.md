# Neap knowledge index

The lessons, working rules and project history that used to live in local Claude memory, for anyone working in this
repository on any Claude account or machine. Read this at the start of a session, after `CLAUDE.md`. The short versions
of the working rules are in `CLAUDE.md` (Working with the owner), so they always load. The detail and the reasons are
here.

**Append-only.** Never delete or replace text in this folder. When something changes, add a
`SUPERSEDED (date, reason)` block beside the old text, and put the new fact in its own note or in the project documents.
New durable facts go here, or into `DECISIONS.md`, `FINDINGS.md` or `BACKLOG.md`, never only into local Claude memory.

Where every original file went: `MIGRATION-MAP.md`. The project's current state stays in its own documents: `BACKLOG.md`,
`DECISIONS.md`, `FINDINGS.md` and `CHANGELOG.md`. A note here that disagrees with them is the older one.

---

### Working with the owner

- [Act without asking once approved](act-without-asking-once-approved.md): after a yes, do the whole job, side steps included.
- [No prompts while away](away-no-prompts.md): when the owner is away, batch anything that needs approval to the very end.
- [The machine is in use](the-machine-is-in-use.md): state changes under you are normal; a line at most, never dig in.
- [No foreground windows while the owner works](feedback_no_foreground_windows.md): ask or wait before screen checks.
- [Label files repo or local](feedback_label_files_repo_or_local.md): say whether each file mentioned goes in the repo or stays local.
- [Mirror means restate](feedback_mirror_means_restate.md): "Mirror" in a message means restate it in cleaner words first.
- [Model per task](model-per-task.md): the cheapest capable model for each task; always name the model for a subagent.
- [Run long checks in the background](feedback_long_runs_background.md): a time limit and a log file, so nothing looks hung.
- [Always update the daily copy](always-update-daily-copy.md): publish every committed change to the try-it copy. **Path superseded.**
- [Backlog lives in the repo](backlog-lives-in-repo.md): bugs and to-dos go in `BACKLOG.md`, not memory.
- [One-step tester asks](feedback_one_step_tester_asks.md): every ask to a tester must be doable whole in one step.

### Investigating the headset and other systems

- [Device-test by default](feedback_headset_device_test_default.md): safe reads proceed on the real device; persistent writes need a GO with a risk rating.
- [No headset tools alongside the app](feedback_no_hid_alongside_app.md): a probe on the control collection steals Neap's reports.
- [Confirm which device you're addressing](confirm-which-device-youre-addressing.md): a wrong target answers happily.
- [Parsers must not drop silently](parsers-must-not-drop-silently.md): "cannot parse" is not "does not exist".
- [Read the vendor's own files](read-the-vendors-own-files.md): config and state files name what captures show anonymously.
- [Semantics come from the manual](semantics-come-from-the-manual.md): the vendor's support docs say what a control means.
- [Headset support page is the reference](feedback_headset_support_page.md): each model's support article decides what it has.
- [Suspect our code before the hardware](suspect-our-code-before-the-hardware.md): "intermittent" is usually two code paths of ours.
- [Reading it back proves nothing](reading-it-back-proves-nothing.md): a round-trip proves the write, not the effect.
- [Sample, do not snapshot](sample-dont-snapshot.md): one reading shows where a value settled, not whether it moved.
- [Diagnose the symptom, not my own innocence](diagnose-symptom-not-blame.md): check the likely specific cause first.
- [Bluetooth-flag write stopped the buttons](bluetooth-flag-write-bricked-buttons.md): a write to `0x300` once stopped every button. Now also in `FINDINGS.md`.

### Verifying

- [Test the build you ship](test-the-build-you-ship.md): a Debug build proves nothing about Release.
- [Efficient verification](efficient-verification.md): tests first, a narrow check next, the full walk once at the end.
- [Scoped pretend-mode runs](feedback_scoped_pretend_runs.md): a small targeted script, never the full walk to iterate.
- [Speed up the agent checks](project_speed_up_agent_checks.md): how `tools/ui` came about; why screenshots go black is unproven.
- [Drive the app through accessibility](drive-the-app-through-accessibility.md): UI Automation reaches the window, injected clicks don't.
- [Close only your own copies](close-only-your-own-copies.md): stop only copies under your own build folder.
- [Parallel pretend runs](project_parallel_pretend_runs.md): one pretend run at a time. **Partly superseded.**
- [Real launch allowed, with safeguards](feedback_no_real_launch.md): real runs for release checks, with the shortcut and settings backed up.
- [Neap app data is sandboxed for Claude](neap-appdata-sandboxed.md): read the real log through a process started outside the package.
- [WSL distro checks](project_wsl_distro_checks.md): Rocky 10 and openSUSE kept for Linux checks.
- [Never round-trip source through PowerShell](never-round-trip-source-through-powershell.md): it mangles non-ASCII silently.

### Design

- [Flow reviews must find absences](feedback_flow_review_absence.md): look for what a screen doesn't offer, from real situations.
- [A mechanism is not an affordance](a-mechanism-is-not-an-affordance.md): two intents need two visible controls.
- [Design by removal, not addition](design-by-removal-not-addition.md): replace a confusing state; don't annotate it.

### Project history

- [Public identity and commits](public-identity-and-commits.md): why commits are pseudonymous, in UTC, with no AI trailer.
- [Neap 0.2.0 released](project_neap_0_2_0_released.md): state after 8 Oct 2026 and the release command quirks. **Superseded by later releases.**
- [Neap folders](neap-folders.md): the folder layout from 25 Sep. **Superseded: the repository is `E:\Dev\neap\Neap-Repo`.**
- [Other headsets support](other-headsets-support.md): the first research on other models. **Superseded: Atlas Air is under way.**
- [Stealth Pro II volume reports](stealth-pro-2-volume-reports.md): low-volume complaints, grouped, and what Swarm II does (issue 6).
- [Parametric EQ direction](parametric-eq-direction.md): decisions from the 25 Sep build.
- [Dolby Access per-profile EQ](dolby-access-eq-per-profile.md): per-profile proven; the write test was blocked.
- [Taskbar quick access wanted](taskbar-quick-access-wanted.md): keep quick-panel controls separable.
- [Redesign review verdict](redesign-review-verdict.md): Option A became the base. **Superseded by the shipped app.**
- [Fixes left on option branches](fixes-left-on-option-branches.md): search old branches before debugging a known-looking bug.
- [Standard checks to add](standard-checks-to-add.md): the pre-commit format check, and pull-request checks. **Partly superseded.**

### Retired (`_retired/`)

Exact duplicates from the parent-folder memory, the three old memory indexes, and two finished or superseded working
instructions (`ask-about-effort-before-each-part.md`, `review-runs-through-without-stopping.md`). Each starts with a line
saying why it was retired and what replaces it.

### Known gaps

- `[[verify-before-declaring-absence]]`, `[[console-redesign-status]]` and `[[redesign-builds-folders]]` are linked from
  some notes but were never in this memory set.
- The cross-project lessons above are also written, without project detail, into Tenon's standards. The notes here keep
  Neap's own cases.
