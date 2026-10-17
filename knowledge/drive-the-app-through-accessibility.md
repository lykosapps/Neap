---
name: drive-the-app-through-accessibility
description: "Injected mouse clicks never reach the WinUI app's content; UI Automation does, and it is how to verify UI work without the user's hands"
metadata: 
  node_type: memory
  type: project
  originSessionId: 0678a02a-d7b3-4614-8ae9-4e6f818eaa23
  modified: 2026-09-22T10:16:07.517Z
---

Synthetic mouse clicks (SetCursorPos + mouse_event) do not reach the WinUI
app's XAML content. Scroll wheel does, and clicks on the title bar do, so it
looks like input is working right up until nothing happens. UI Automation
reaches it fine: find an element by its accessibility name and use its
pattern.

**Why:** two rounds of screenshots were read as "the feature is broken" when
the feature was fine and the clicks were going nowhere. See
[[suspect-our-code-before-the-hardware]] — same trap, one layer out: suspect
the harness before the product.

**How to apply:** to verify UI behaviour without the user present, drive it
through UI Automation, not injected clicks. This also means anything worth
verifying needs an accessibility name — which is worth adding anyway, and is
the same work as [[design-by-removal-not-addition]] paying for itself twice.
Also: screen capture returns black while the display is asleep, so nudge the
mouse and wait a few seconds before taking a screenshot.


---

**SUPERSEDED (2026-10-17, memory migration):** the WinUI app is retired. The Avalonia app is driven the same way, through `tools/ui` (CLAUDE.md, Verifying). Why screenshots come out black is still unproven (`project_speed_up_agent_checks.md`).
