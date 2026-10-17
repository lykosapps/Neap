---
name: real-launch-allowed-with-safeguards
description: "As of 2026-10-07 the owner allows real (non-pretend) runs of the new app against the real headset, for release checks, with safeguards; never as a stray smoke test"
metadata:
  node_type: memory
  type: feedback
  originSessionId: fbc97cb2-a5bc-4426-a3dd-08a7a872d8fa
  modified: 2026-10-07T11:24:00.753Z
---

Earlier rule (never start any Neap build without --pretend) came from a stray `Neap.Desktop.exe --help` on 2026-10-07 that started a real second copy: it re-pointed the owner's Startup shortcut (`Startup\Neap.lnk`) at my scratch folder, wrote to the real app log, and opened the real headset for about twenty minutes. I restored the shortcut to `E:\Dev\neap\build\Neap.exe --startup`.

The owner then said: "change the rule so you can do as much as possible by yourself" (the hardware release checks in CONTRIBUTING "Before a release"). So real runs are now allowed, on purpose, for those checks.

**Why the rule existed:** a real launch calls `Startup.Refresh()`, rewriting the sign-in shortcut to wherever the running copy lives; the owner's try-it copy (`E:\Dev\neap\build\Neap.exe`, the old app) runs all day and holds the headset and the one-copy lock differently from the new app.

**How to apply:** before any real run, record the Startup shortcut's target and copy the real app-settings and log; stop the try-it copy only for the run and start it again afterwards (`Neap.exe --startup`); never turn on "Start when you sign in" from the new app unless that check is being run, and restore the shortcut after; run only when the owner has said they are away and the headset is on (windows come to the front, see [[no-foreground-windows-while-owner-works]]); never a stray run to "see it start". Still use `--pretend` for everything that doesn't need the headset. See [[scoped-pretend-runs]], [[parallel-pretend-runs]].


---

**SUPERSEDED (2026-10-17, memory migration):** the file name says 'no real launch', but the rule it holds is the reverse: real launches are allowed with these safeguards (DECISIONS.md, 2026-10-08). The try-it copy at `E:\Dev\neap\build` now runs the new app (`Neap.exe`), not the old one.
