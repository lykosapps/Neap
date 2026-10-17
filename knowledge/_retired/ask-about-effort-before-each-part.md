RETIRED 2026-10-17: superseded by the model-per-task rule (no per-part effort asks) and overridden for its one review; see ../model-per-task.md

---
name: ask-about-effort-before-each-part
description: "before starting each part of a multi-part review, say whether the current model and effort level suit that part, and wait"
metadata:
  node_type: memory
  type: feedback
  originSessionId: bda12273-502f-4412-86bf-f0c1654b5fe1
  modified: 2026-09-23T09:47:51.163Z
---

Before starting each part of a staged review, check this session's model and effort (get_session "self") and say in one line whether they suit that part, with a recommendation. Then wait for the owner before starting.

**Why:** he asked for it on 2026-09-23, before Part 4 of the open-source code review: "for each part we should ask this question before starting".

**How to apply:** a session can't change its own effort. If a change is worth making, tell him to switch it in the app's model picker, then start once he confirms.
