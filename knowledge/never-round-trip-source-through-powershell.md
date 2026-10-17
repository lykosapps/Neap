---
name: never-round-trip-source-through-powershell
description: Get-Content | Set-Content mangles non-ASCII in source files; edit with Python or the Edit tool instead
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 0678a02a-d7b3-4614-8ae9-4e6f818eaa23
  modified: 2026-09-22T13:43:38.838Z
---

Editing C# and XAML with `Get-Content -Raw | ... | Set-Content -Encoding utf8`
corrupted every non-ASCII character in five files at once: em dashes became
"â€”", middle dots "Â·", and Segoe icon glyphs turned into tofu boxes in the
shipped UI. Whole-file reversal could not fix it because the files were a mix
of survived and mangled text, and one glyph lost a byte outright and had to be
retyped.

**Why:** PowerShell reads and writes with the console/ANSI codepage in this
environment, so the round trip double-encodes. The damage is silent — it
compiles and only shows up on screen.

**How to apply:** edit source with Python (`io.open(..., encoding="utf-8")`)
or the Edit tool, never by piping through PowerShell. And write icon glyphs as
`""` escapes rather than literal private-use characters, so a careless
round trip cannot destroy them. Same instinct as
[[sample-dont-snapshot]]: verify what actually shipped, on screen, rather than
trusting that a change went in cleanly.
