---
name: daily-copy
description: Put the latest committed Neap change into the owner's try-it copy and confirm it came up with the headset connected. Use after every commit that changes the app, without asking, and when someone says "daily copy", "try-it copy" or "update my copy".
---

# Update the try-it copy

The owner runs a try-it copy of Neap all day and tries every change on the real
headset. Every committed change that touches the app goes into it straight
away, without asking. Changes to docs, tests or tools alone don't need it.

## Steps

1. **Find the folder.** `CLAUDE.local.md` in the main checkout names the try-it
   folder. If none is named, ask; never guess one.
2. **Commit first.** The script refuses uncommitted work, and refuses a
   checkout behind `main`, so the copy never goes backwards.
3. **Run it**, in the background (publishing takes a minute or two):

   ```
   powershell -ExecutionPolicy Bypass -File .claude\skills\daily-copy\update.ps1 -Target <try-it folder>
   ```

   It publishes Release, stops only the copy running from that folder, copies
   the publish over it, starts it again into the notification area as at
   sign-in (no window takes the screen), and reads
   the real app log from outside the desktop app's package.
4. **Report** in a line or two: what is now in the copy and what to try on the
   headset, plus anything not checked.

## Reading the result

- **`ok` and a `headset: Connected` line** (exit 0): healthy.
- **`note`, the headset not connected** (exit 2): the copy started, but the
  headset is off or quiet. Say so in one line; the owner may simply have it off.
  Don't investigate.
- **`FAIL`** (exit 1): say what failed. If the copy was stopped and not
  started again, start it again before anything else.

## Never

- **Stop any other Neap.** Other sessions' copies and practice runs (`--pretend`)
  are left alone; the script filters by the try-it folder's path.
- **Read the app log from a tool directly.** The desktop app gives its tools a
  private copy of `%LOCALAPPDATA%`, so a direct read can show a stale log. The
  script reads it through a process started outside the package.
