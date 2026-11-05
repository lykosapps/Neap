---
name: headset-checks
description: Check a Neap change against the real headset, sized to what changed. Use when a change alters how the app behaves with the hardware, when someone says "headset checks", "check it on the headset" or "run the hardware checks", or when the numbered checks in docs/design/HEADSET-CHECKS.md come up.
---

# Headset checks

A change that alters how Neap behaves with the hardware is not done until it
passes on the real headset. Run only what the change touched.

## 1. Size it first

- **Write down what changed:** which screens and which headset behaviours. Then
  pick only the checks and parts that cover them. Never `all`, never a check for
  something that didn't change.
- **Cheapest first:** `dotnet test`, then `dotnet format`, then the one check
  part, then pictures. A failed part is fixed and rerun alone.
- **Say the time** before a batch of more than two checks: ten minutes or more.

## 2. What runs without the owner

- **Scripted parts on the hidden desktop:** `tools\ui\hardware.ps1` drives the
  real app and reads the headset back with the probe. Run its parts through
  `tools\pretend-quiet.ps1`, as the only thing in the command:

  ```
  powershell -ExecutionPolicy Bypass -File tools\pretend-quiet.ps1 -Run 'tools\ui\run.ps1 hardware -Parts BFLT -Inside'
  ```

  Pick the letters from `hardware.ps1` (each part is an `if ($Parts.Contains(...))`
  block) and `CONTRIBUTING.md`, "Before a release". It stops the owner's copy for
  the run and puts it, the sign-in shortcut and the settings back afterwards.
- **Reads that don't persist** go ahead on the headset without asking.

## 3. What needs the owner

- **Ask first, every time,** with how long it takes, for anything that takes the
  screen or the keyboard: part K (it sends real key presses), any check run
  outside the hidden desktop. One yes covers one run. Until then, say plainly
  that it has not run.
- **Writes that persist** (chip settings, firmware, anything kept): ask with a
  risk rating from 1 to 5, what could go wrong and the expected outcome.
- **By hand:** the numbered checks in `docs/design/HEADSET-CHECKS.md` need hands
  or ears (the wheel, unplugging, hearing a mode). Give the owner only the
  numbers that cover the change, one short step each.
- **Prove which device answered.** A transmitter answers from memory for a
  headset it can no longer reach.

## 4. Record it

- **Each passed check** gets a line under it in `HEADSET-CHECKS.md`:
  `*Passed <date>: <what was seen>.*`
- **The headset's tracker** in `docs/headsets/` changes in the same commit when
  a check settles what Neap does with a feature.
- **The commit message** names the check numbers and what was seen, and any
  check in the group that has not run yet.
