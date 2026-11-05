---
name: release
description: Get Neap ready to release and find what stands between main and a tag. Use when someone says "release", "ship it", "is it ready to tag", "cut a version" or names a version to release.
---

# Release

The owner tags, pushes and publishes. This skill gets main ready and says what
is left. Never push, tag, publish or merge into `main` without being asked for
that step.

## 1. Where it stands

- **Run the read-only check first** (in the main checkout, on `main`):

  ```
  powershell -ExecutionPolicy Bypass -File tools\release-check.ps1
  ```

  It changes nothing. Each line is `ok`, `todo` or `note`. A failed fetch means
  another GitHub account is signed in: check `gh auth status`.
- **Report the `todo` lines** in plain words, most important first.

## 2. Make it ready

- **Version and changelog:** `<Version>` in `Directory.Build.props` matches the
  newest heading in `CHANGELOG.md`, dated today in UTC, with nothing left under
  Unreleased. These go in one commit.
- **Backlog:** the items `release-check.ps1` lists for before the tag are done,
  or the owner moves them.

## 3. The checks a release needs

Run them on the Release build, one at a time. Ask before any that takes the
screen; the rest run on the hidden desktop through `tools\pretend-quiet.ps1`,
as the only thing in the command. Say the time before starting: together they
take about half an hour.

- **Every screen check:** `-Run 'tools\ui\run.ps1 all -Inside'`. A release is
  the one time `all` is right; for a single change, size the checks instead.
- **Accessibility:** `-Run 'tools\ui\run.ps1 access -Inside'`.
- **The pretend walk:** `tools\pretend-quiet.ps1` with no `-Run`.
- **The headset:** the `headset-checks` skill, with the parts and numbered checks
  `CONTRIBUTING.md` lists under "Before a release".

A failed part is fixed and rerun alone, never the whole batch.

## 4. Hand over

- **Rerun `release-check.ps1`.** It should end with nothing to do.
- **Give the owner the steps they run,** each in its own block: merge into
  `main` if the work is on a branch, push `main`, then push the tag
  `v<version>`. Pushing the tag starts `.github/workflows/release.yml`, which
  builds and publishes the release.
