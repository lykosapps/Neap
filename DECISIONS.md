# Decisions

Choices that shaped Neap, and why, so a later session doesn't reopen a settled question without knowing the reason.

## 2026-09-23 — Disclose AI-assisted development, once, low-key

Logged retroactively on 2026-09-26: shipped in commit "Say it was written with AI assistance" (README.md, under Status) but never written down as a decision, so an old open question about it kept surfacing in stale notes after it was already settled.

- **Options:** say nothing; a prominent disclosure/banner; one low-key line in README.md.
- **Chosen:** one low-key line in README.md ("It was written with AI assistance.").
- **Why:** the project is going public on GitHub, and FINDINGS.md and CONTRIBUTING.md already lean on transparency and rigor — measured/inferred/research tagging, hardware verification standards — as the credibility signal. An honest one-line note fits that existing tone and gets ahead of contributors asking, without making it the headline. This is about the public-facing docs only; it doesn't touch commits, which already stay free of any AI trailer — that policy is unchanged.
- **Revisit if:** a contributor or reviewer raises it as a concern before or after the GitHub launch, or the disclosure line turns out to invite more scrutiny than it heads off.
