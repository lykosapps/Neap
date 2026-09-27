# Decisions

## 2026-09-27 — What a profile holds

- **Options:** the narrow list first proposed (noise control, Superhuman
  Hearing, the noise gate, AI noise reduction, mic monitoring, both
  equaliser presets); adding spatial sound and the power/button settings;
  leaving those two out for a later version.
- **Chosen:** all of it — noise control (as a three-way mode: off,
  cancelling, transparency, plus level), Superhuman Hearing, the noise
  gate, AI noise reduction, mic monitoring, both equaliser presets, spatial
  sound, and the Mode button, dial and auto-off settings. Mic volume was
  dropped from the first proposal: it mirrors the Windows capture level
  rather than being a real headset setting, the same reason every other
  volume was already left out.
- **Why:** a profile is a full sound-personality swap, not a partial one.
  Spatial sound can occasionally need a follow-up step (a Dolby Atmos
  licence prompt on a transmitter that hasn't activated it), which was
  weighed and accepted as a smaller cost than leaving it out.
- **Revisit if:** the Dolby Atmos prompt turns out to interrupt switching
  often enough to be annoying.

## 2026-09-27 — Where the profile switcher lives

- **Options:** a tile on Home, near the other quick settings; in the title
  bar next to the connection status; a persistent header bar under the
  title bar, above every page.
- **Chosen:** a full-width bar under the title bar, visible on every page.
  The connection status stays exactly where it is, in the title bar, with
  battery, signal and route staying on Home as already decided — the new
  bar is about which profile is active, not a second status dashboard.
- **Why:** the title bar strip is deliberately minimal (a dot and a word)
  and had no room for a profile name too. Corsair, Razer and Logitech's own
  software all give a profile switcher a dedicated, always-visible spot
  rather than folding it into a status strip, and WinUI has a matching
  built-in pattern for a persistent bar under the title bar.
- **Revisit if:** the bar feels redundant once app-to-profile assignment
  (a later version) makes manual switching rare.

## 2026-09-27 — What happens after changing something post-profile

- **Options:** save silently; warn before switching away from an unsaved
  change; show an edited state with no silent saving, matching how the
  equaliser already marks an edited curve.
- **Chosen:** the same treatment as the equaliser: the active profile shows
  as edited the moment anything it covers changes, until it's saved back or
  discarded. Switching profiles while edited does not warn first, matching
  the equaliser's own preset switch.
- **Why:** reusing a pattern already shipped and understood, rather than
  inventing a second way to say the same thing.

## 2026-09-27 — Where profiles are stored

- **Chosen:** on the PC, in Neap's own settings, not on the headset.
- **Why:** profiles survive a factory reset or a different headset, and
  don't compete with the headset's own five equaliser slots per bank.
- **Revisit if:** someone asks to carry profiles between PCs; export/import
  would cover it without changing where they live.

## 2026-09-27 — Assigning apps to a profile, without switching automatically yet

- **Options:** build assignment and automatic switching together; build
  assignment alone first, with automatic switching as a separate step.
- **Chosen:** assignment alone. A profile can be given one or more apps in
  Settings, by process name, but nothing yet switches to it on its own when
  that app runs.
- **Why:** automatic switching needs new plumbing Neap does not have yet
  (watching which window is in front) and its own decisions (what counts as
  "active," what it reverts to, whether a manual switch overrides it) that
  are worth trying against assignment once that part is right, not guessing
  at both together.
- **An app belongs to at most one profile.** Assigning it to a second one
  takes it away from the first, since two profiles both claiming the same
  app would leave nothing to decide between them once switching is built.
- **Revisit when:** automatic switching is built; the decisions above get
  made then.

## 2026-09-27 — A profile's saved equaliser preset, deleted since

- **Chosen:** apply everything else the profile holds, and say which
  preset(s) could not be found, rather than block the whole switch or fail
  silently.
- **Why:** matches the project's standing rule that nothing fails silently.

Choices that shaped Neap, and why, so a later session doesn't reopen a settled question without knowing the reason.

## 2026-09-26 — Audio puts its everyday settings first and the equaliser last

- **Options:** keep the equaliser first, as "what this page is opened for"; put noise control and the levels first and the equaliser last.
- **Chosen:** noise control and Superhuman Hearing, then the levels and spatial sound, then the equaliser.
- **Why:** a UI review measured the page at real laptop sizes: with the equaliser first, the first view on a common laptop held nothing else. USERS.md rates the equaliser and noise cancellation as equally occasional, and the Microphone page already puts its quick controls first and its equaliser last. Home's way to the equaliser scrolls down to it, so it is still one click away.
- **Revisit if:** the equaliser turns out to be what Audio is opened for most, or Microphone changes its own order.

## 2026-09-23 — Disclose AI-assisted development, once, low-key

Logged retroactively on 2026-09-26: shipped in commit "Say it was written with AI assistance" (README.md, under Status) but never written down as a decision, so an old open question about it kept surfacing in stale notes after it was already settled.

- **Options:** say nothing; a prominent disclosure/banner; one low-key line in README.md.
- **Chosen:** one low-key line in README.md ("It was written with AI assistance.").
- **Why:** the project is going public on GitHub, and FINDINGS.md and CONTRIBUTING.md already lean on transparency and rigor — measured/inferred/research tagging, hardware verification standards — as the credibility signal. An honest one-line note fits that existing tone and gets ahead of contributors asking, without making it the headline. This is about the public-facing docs only; it doesn't touch commits, which already stay free of any AI trailer — that policy is unchanged.
- **Revisit if:** a contributor or reviewer raises it as a concern before or after the GitHub launch, or the disclosure line turns out to invite more scrutiny than it heads off.
