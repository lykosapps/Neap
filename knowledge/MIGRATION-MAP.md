# Migration map: local Claude memory → `knowledge/` (2026-10-17)

Every Claude memory file that held something about Neap now lives here, so the rules and facts travel with the repository to
any Claude account or machine. The sources were three local memory folders, one per folder Neap has lived in:

- `E--Dev-neap`: sessions started in the parent folder. All 9 notes are exact duplicates of `E--Dev-neap-neap`.
- `E--Dev-neap-neap`: the repository's old folder, which no longer exists.
- `E--Dev-neap-Neap-Repo`: the current repository.

**66 source files** (63 notes and 3 memory indexes), every one listed below. The local files were left in place, and each
local index now opens with a pointer here.

### How they were copied

- **Redacted, never reworded.** The repository is public and must not identify its owner. Names, email addresses, the
  employer and the work account were replaced by `the owner`, `<personal email>`, `<work email>`, `<employer>` and
  `<work GitHub account>`, along with the user folder name, the country and the time-zone offset. Nothing else in a note
  was changed. A note marked byte-identical had nothing to redact.
- **Out of date, never deleted.** Where a note is stale, a `SUPERSEDED (2026-10-17, …)` block is added at its end, and
  the original text stays. A `NOTE` block adds context without saying the note is wrong.
- **Retired, unchanged.** Duplicates, old indexes and finished one-off instructions are in `_retired/`. Each starts with a
  `RETIRED <date>: <reason>; see <replacement>` line, followed by the note itself.
- **Not copied.** The three notes about the owner's Claude and GitHub accounts identify the owner, so they stay in local
  memory, for the owner's private profile repository.
- **Moved to Tenon.** Two notes about Tenon itself, `tenon-process-kit` and `update-tenon-plugins`, went to the Tenon
  repository's own `knowledge/`.
- **Backups:** the whole local memory tree was zipped before anything moved.

| Source folder | Source file | Destination | Changes |
|---|---|---|---|
| `E--Dev-neap` | `MEMORY.md` | `knowledge/_retired/MEMORY-E--Dev-neap.md` (retired: old memory index of the parent folder session, a subset of the neap\neap index; see ../INDEX.md) | redacted |
| `E--Dev-neap` | `always-update-daily-copy.md` | `knowledge/_retired/dup-E--Dev-neap-always-update-daily-copy.md` (retired: exact duplicate from an older memory folder; see ../always-update-daily-copy.md) | redacted |
| `E--Dev-neap` | `away-no-prompts.md` | `knowledge/_retired/dup-E--Dev-neap-away-no-prompts.md` (retired: exact duplicate from an older memory folder; see ../away-no-prompts.md) | redacted |
| `E--Dev-neap` | `claude-account-per-project.md` | not copied: local only: account/identity note, for the private profile repo (owner decision 2026-10-17); exact duplicate | redacted |
| `E--Dev-neap` | `diagnose-symptom-not-blame.md` | `knowledge/_retired/dup-E--Dev-neap-diagnose-symptom-not-blame.md` (retired: exact duplicate from an older memory folder; see ../diagnose-symptom-not-blame.md) | redacted |
| `E--Dev-neap` | `efficient-verification.md` | `knowledge/_retired/dup-E--Dev-neap-efficient-verification.md` (retired: exact duplicate from an older memory folder; see ../efficient-verification.md) | redacted |
| `E--Dev-neap` | `neap-appdata-sandboxed.md` | `knowledge/_retired/dup-E--Dev-neap-neap-appdata-sandboxed.md` (retired: exact duplicate from an older memory folder; see ../neap-appdata-sandboxed.md) | body byte-identical |
| `E--Dev-neap` | `other-headsets-support.md` | `knowledge/_retired/dup-E--Dev-neap-other-headsets-support.md` (retired: exact duplicate from an older memory folder; see ../other-headsets-support.md) | redacted |
| `E--Dev-neap` | `parametric-eq-direction.md` | `knowledge/_retired/dup-E--Dev-neap-parametric-eq-direction.md` (retired: exact duplicate from an older memory folder; see ../parametric-eq-direction.md) | redacted |
| `E--Dev-neap` | `tenon-process-kit.md` | Tenon repo `knowledge/_retired/tenon-process-kit.md` (retired: exact duplicate from an older memory folder; see ../tenon-process-kit.md) | redacted |
| `E--Dev-neap-Neap-Repo` | `MEMORY.md` | `knowledge/_retired/MEMORY-E--Dev-neap-Neap-Repo.md` (retired: old memory index, replaced by the repo index; see ../INDEX.md) | body byte-identical |
| `E--Dev-neap-Neap-Repo` | `feedback_flow_review_absence.md` | `knowledge/feedback_flow_review_absence.md` | SUPERSEDED note added |
| `E--Dev-neap-Neap-Repo` | `feedback_headset_device_test_default.md` | `knowledge/feedback_headset_device_test_default.md` | NOTE added |
| `E--Dev-neap-Neap-Repo` | `feedback_headset_support_page.md` | `knowledge/feedback_headset_support_page.md` | byte-identical |
| `E--Dev-neap-Neap-Repo` | `feedback_label_files_repo_or_local.md` | `knowledge/feedback_label_files_repo_or_local.md` | redacted |
| `E--Dev-neap-Neap-Repo` | `feedback_long_runs_background.md` | `knowledge/feedback_long_runs_background.md` | redacted |
| `E--Dev-neap-Neap-Repo` | `feedback_mirror_means_restate.md` | `knowledge/feedback_mirror_means_restate.md` | byte-identical |
| `E--Dev-neap-Neap-Repo` | `feedback_no_foreground_windows.md` | `knowledge/feedback_no_foreground_windows.md` | redacted |
| `E--Dev-neap-Neap-Repo` | `feedback_no_hid_alongside_app.md` | `knowledge/feedback_no_hid_alongside_app.md` | byte-identical |
| `E--Dev-neap-Neap-Repo` | `feedback_no_real_launch.md` | `knowledge/feedback_no_real_launch.md` | SUPERSEDED note added |
| `E--Dev-neap-Neap-Repo` | `feedback_one_step_tester_asks.md` | `knowledge/feedback_one_step_tester_asks.md` | redacted |
| `E--Dev-neap-Neap-Repo` | `feedback_scoped_pretend_runs.md` | `knowledge/feedback_scoped_pretend_runs.md` | NOTE added |
| `E--Dev-neap-Neap-Repo` | `model-per-task.md` | `knowledge/model-per-task.md` | NOTE added |
| `E--Dev-neap-Neap-Repo` | `project_neap_0_2_0_released.md` | `knowledge/project_neap_0_2_0_released.md` | redacted, SUPERSEDED note added |
| `E--Dev-neap-Neap-Repo` | `project_parallel_pretend_runs.md` | `knowledge/project_parallel_pretend_runs.md` | SUPERSEDED note added |
| `E--Dev-neap-Neap-Repo` | `project_speed_up_agent_checks.md` | `knowledge/project_speed_up_agent_checks.md` | redacted, NOTE added |
| `E--Dev-neap-Neap-Repo` | `project_two_claude_windows.md` | not copied: local only: account/identity note, for the private profile repo (owner decision 2026-10-17) | redacted |
| `E--Dev-neap-Neap-Repo` | `project_wsl_distro_checks.md` | `knowledge/project_wsl_distro_checks.md` | byte-identical |
| `E--Dev-neap-Neap-Repo` | `stealth-pro-2-volume-reports.md` | `knowledge/stealth-pro-2-volume-reports.md` | byte-identical |
| `E--Dev-neap-neap` | `MEMORY.md` | `knowledge/_retired/MEMORY-E--Dev-neap-neap.md` (retired: old memory index of the orphaned folder E:\Dev\neap\neap; see ../INDEX.md) | redacted |
| `E--Dev-neap-neap` | `a-mechanism-is-not-an-affordance.md` | `knowledge/a-mechanism-is-not-an-affordance.md` | redacted |
| `E--Dev-neap-neap` | `act-without-asking-once-approved.md` | `knowledge/act-without-asking-once-approved.md` | redacted |
| `E--Dev-neap-neap` | `always-update-daily-copy.md` | `knowledge/always-update-daily-copy.md` | redacted, SUPERSEDED note added |
| `E--Dev-neap-neap` | `ask-about-effort-before-each-part.md` | `knowledge/_retired/ask-about-effort-before-each-part.md` (retired: superseded by the model-per-task rule (no per-part effort asks) and overridden for its one review; see ../model-per-task.md) | redacted |
| `E--Dev-neap-neap` | `away-no-prompts.md` | `knowledge/away-no-prompts.md` | redacted |
| `E--Dev-neap-neap` | `backlog-lives-in-repo.md` | `knowledge/backlog-lives-in-repo.md` | redacted, SUPERSEDED note added |
| `E--Dev-neap-neap` | `bluetooth-flag-write-bricked-buttons.md` | `knowledge/bluetooth-flag-write-bricked-buttons.md` | NOTE added |
| `E--Dev-neap-neap` | `claude-account-per-project.md` | not copied: local only: account/identity note, for the private profile repo (owner decision 2026-10-17) | redacted |
| `E--Dev-neap-neap` | `close-only-your-own-copies.md` | `knowledge/close-only-your-own-copies.md` | redacted |
| `E--Dev-neap-neap` | `confirm-which-device-youre-addressing.md` | `knowledge/confirm-which-device-youre-addressing.md` | byte-identical |
| `E--Dev-neap-neap` | `design-by-removal-not-addition.md` | `knowledge/design-by-removal-not-addition.md` | redacted |
| `E--Dev-neap-neap` | `diagnose-symptom-not-blame.md` | `knowledge/diagnose-symptom-not-blame.md` | redacted |
| `E--Dev-neap-neap` | `dolby-access-eq-per-profile.md` | `knowledge/dolby-access-eq-per-profile.md` | redacted |
| `E--Dev-neap-neap` | `drive-the-app-through-accessibility.md` | `knowledge/drive-the-app-through-accessibility.md` | SUPERSEDED note added |
| `E--Dev-neap-neap` | `efficient-verification.md` | `knowledge/efficient-verification.md` | redacted, NOTE added |
| `E--Dev-neap-neap` | `fixes-left-on-option-branches.md` | `knowledge/fixes-left-on-option-branches.md` | redacted |
| `E--Dev-neap-neap` | `neap-appdata-sandboxed.md` | `knowledge/neap-appdata-sandboxed.md` | byte-identical |
| `E--Dev-neap-neap` | `neap-folders.md` | `knowledge/neap-folders.md` | redacted, SUPERSEDED note added |
| `E--Dev-neap-neap` | `never-round-trip-source-through-powershell.md` | `knowledge/never-round-trip-source-through-powershell.md` | byte-identical |
| `E--Dev-neap-neap` | `other-headsets-support.md` | `knowledge/other-headsets-support.md` | redacted, SUPERSEDED note added |
| `E--Dev-neap-neap` | `parametric-eq-direction.md` | `knowledge/parametric-eq-direction.md` | redacted, NOTE added |
| `E--Dev-neap-neap` | `parsers-must-not-drop-silently.md` | `knowledge/parsers-must-not-drop-silently.md` | redacted |
| `E--Dev-neap-neap` | `public-identity-and-commits.md` | `knowledge/public-identity-and-commits.md` | redacted |
| `E--Dev-neap-neap` | `read-the-vendors-own-files.md` | `knowledge/read-the-vendors-own-files.md` | redacted |
| `E--Dev-neap-neap` | `reading-it-back-proves-nothing.md` | `knowledge/reading-it-back-proves-nothing.md` | byte-identical |
| `E--Dev-neap-neap` | `redesign-review-verdict.md` | `knowledge/redesign-review-verdict.md` | redacted, SUPERSEDED note added |
| `E--Dev-neap-neap` | `review-runs-through-without-stopping.md` | `knowledge/_retired/review-runs-through-without-stopping.md` (retired: one-off instruction for the 2026-09-23 open-source review, which is finished; see ../../CLAUDE.md (Working with the owner)) | redacted |
| `E--Dev-neap-neap` | `sample-dont-snapshot.md` | `knowledge/sample-dont-snapshot.md` | redacted |
| `E--Dev-neap-neap` | `semantics-come-from-the-manual.md` | `knowledge/semantics-come-from-the-manual.md` | redacted |
| `E--Dev-neap-neap` | `standard-checks-to-add.md` | `knowledge/standard-checks-to-add.md` | redacted, SUPERSEDED note added |
| `E--Dev-neap-neap` | `suspect-our-code-before-the-hardware.md` | `knowledge/suspect-our-code-before-the-hardware.md` | byte-identical |
| `E--Dev-neap-neap` | `taskbar-quick-access-wanted.md` | `knowledge/taskbar-quick-access-wanted.md` | redacted, SUPERSEDED note added |
| `E--Dev-neap-neap` | `tenon-process-kit.md` | Tenon repo `knowledge/tenon-process-kit.md` | redacted, SUPERSEDED note added |
| `E--Dev-neap-neap` | `test-the-build-you-ship.md` | `knowledge/test-the-build-you-ship.md` | byte-identical |
| `E--Dev-neap-neap` | `the-machine-is-in-use.md` | `knowledge/the-machine-is-in-use.md` | redacted |
| `E--Dev-neap-neap` | `update-tenon-plugins.md` | Tenon repo `knowledge/update-tenon-plugins.md` | redacted, NOTE added |
