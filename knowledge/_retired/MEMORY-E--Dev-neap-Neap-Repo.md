RETIRED 2026-10-17: old memory index, replaced by the repo index; see ../INDEX.md

- [Model per task](model-per-task.md) — cheapest capable model per task; Haiku/Sonnet subagents, Opus for judgment
- [Flow reviews must find absences](feedback_flow_review_absence.md) — check missing ways to supply inputs from real situations; never downgrade for by-design
- [Scoped pretend-mode runs](feedback_scoped_pretend_runs.md) — write a small targeted screenshot script; don't rerun Neap's full pretend.ps1 walk to iterate
- [Run long checks in the background](feedback_long_runs_background.md) — time limit + log file, so nothing looks hung
- [Parallel pretend runs](project_parallel_pretend_runs.md) — another session shares the pretend folder/pipe; use NEAP_PRETEND_FOLDER and wait before tools/ui checks
- [Speed up the agent checks](project_speed_up_agent_checks.md) — slow parts, black-screenshot finding, ideas for a future session
- [Real launch allowed, with safeguards](feedback_no_real_launch.md) — owner allowed real runs for release checks; back up shortcut/settings, stop+restore try-it copy, only when owner is away
- [No foreground windows while owner works](feedback_no_foreground_windows.md) — screen checks steal focus; ask/wait before tools/ui runs
- [Label files repo or local](feedback_label_files_repo_or_local.md) — say whether each mentioned file goes in the repo or stays local
- [Neap 0.2.0 released](project_neap_0_2_0_released.md) — state after 8 Oct 2026, where handoff and checks live, release command quirks
- [WSL distro checks](project_wsl_distro_checks.md) — Rocky 10 + openSUSE kept for re-checking the Linux app; how to run them; delete after packaging
- [No headset tools alongside the app](feedback_no_hid_alongside_app.md) — a probe/watcher on the control collection steals Neap's reports; stop the copy first or log from inside Neap
- [Headset support page is the reference](feedback_headset_support_page.md) — use Turtle Beach's support article per model to decide what a headset has
- [Two Claude windows](project_two_claude_windows.md) — personal vs work windows and GitHub accounts, separated 13 Oct; why account warnings misfired
- [Stealth Pro II volume reports](stealth-pro-2-volume-reports.md) — r/TurtleBeach low-volume complaints, grouped; sleep-wake control loss is the candidate to check first
- [Mirror means restate](feedback_mirror_means_restate.md) — "Mirror" in a message: restate what the owner said in cleaner words first
- [One-step tester asks](feedback_one_step_tester_asks.md) — every ask to a tester must be doable whole in one step; ask for the one file that settles it
- [Device-test headset investigations by default](feedback_headset_device_test_default.md) — proceed on the real device when safe; ask with a confidence level for persistent/firmware writes
