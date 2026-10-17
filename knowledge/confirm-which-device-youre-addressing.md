---
name: confirm-which-device-youre-addressing
description: "When a system has several devices behind one interface, prove which one a command reached before trusting the result — a wrong target answers happily and looks correct"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 69317492-c616-4797-9d17-41d8585ca95f
  modified: 2026-09-21T14:40:02.944Z
---

When several devices sit behind one interface, establish **which one a
command actually reached** before drawing any conclusion from it. A
command sent to the wrong device does not error — it answers normally,
and every result after that is quietly about the wrong thing.

On the Stealth Pro II, Turtle Beach's own protocol tunnels through the
USB transmitter to the headset. The chip vendor's lower-level protocol,
on the *same HID channel, same report ids, same usage page*, does not: it
addresses whatever is plugged into USB. With only the dongle connected I
spent most of a session reading and writing the **dongle's** firmware
settings while reasoning about the headset, including a "decisive" test
that blanked an entire configuration table and proved nothing.

What gave it away was a size mismatch noticed by accident — the data was
507 bytes where the headset's copy should have been 529.

**Why:** "it responded" feels like confirmation that you are talking to
the thing you meant. It is not. It only confirms something is listening.

**How to apply:**

- Before trusting a new low-level channel, read an identifying value
  through it — model name, serial, a field whose expected value differs
  per device — and check it against what you expect.
- Prefer an identity check the data itself provides (a length, a name)
  over an assumption about routing.
- Build the check into any tool that writes: make it name the target and
  refuse when the connected device is not that one. A guard that only
  lives in your head does not survive the next session.
- When a change "has no effect", suspect the wrong target before
  concluding the feature does not exist.

Related: [[parsers-must-not-drop-silently]], [[verify-before-declaring-absence]]
