---
name: taskbar-quick-access-wanted
description: "The WinUI app is a normal desktop window, but the owner wants a taskbar/tray quick-access surface eventually"
metadata: 
  node_type: memory
  type: project
  originSessionId: 7f4b7543-1b28-44f0-9062-3f360160ece2
  modified: 2026-09-22T07:26:13.345Z
---

Decided 2026-09-22, at the start of the WinUI 3 build: the app is a normal
desktop window. The owner also wants a taskbar quick-access surface "eventually" —
a small panel for the things you reach for mid-game (mix, mute, ANC) without
opening the whole app.

**Why:** it is a deferred intention, not a requirement, so it will not show up
anywhere in the code or the backlog — but it should shape how the window shell
is built, since a quick-access panel wants the same controls hosted somewhere
reusable rather than welded into the page layouts.

**How to apply:** keep the controls that would belong in a quick panel
separable from the page that shows them. Do not build the tray surface until
he asks. See [[design-by-removal-not-addition]] — the quick panel is the
strongest case for showing only what is needed at that moment.


---

**SUPERSEDED (2026-10-17, memory migration):** the app is Avalonia now, not WinUI. The intent is in `BACKLOG.md` ('Quick panel from the taskbar').
