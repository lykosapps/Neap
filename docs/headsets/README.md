# Headset support

One file per headset Neap supports or is working towards, each in the format
below, so every headset is tracked the same way: what it has, what Neap does
with it, and how sure we are on each system. [FINDINGS.md](../../FINDINGS.md)
keeps the evidence in full; these files are the working list.

| Headset | File | Where it stands |
|---|---|---|
| Stealth Pro II | [stealth-pro-ii.md](stealth-pro-ii.md) | Supported on Windows and the Steam Deck. |
| Atlas Air | [atlas-air.md](atlas-air.md) | Not in 0.3.0 (`HeadsetModels.AtlasAirReleased` is off); in testing on Windows, does not connect on Linux. |

## Keeping a file up to date

- Update it whenever something new arrives: a recording, a tester's reply, a
  finding from Swarm II or Turtle Beach's pages, or a test build.
- Change the row, then add a line to **Evidence** saying what arrived and when.
- A public release carries only the features confirmed on that system; the
  rest stay hidden or read only ([DECISIONS.md](../../DECISIONS.md), 8 October 2026).

## What Turtle Beach says

The **Turtle Beach** column says whether Turtle Beach lists the feature for
this headset, and where:

| Words | Meaning |
|---|---|
| Listed | In Turtle Beach's Swarm II article for this headset. |
| Listed, with a condition | Listed, but only in some cases, such as "Waves driver only". |
| Quick start guide | A physical control, from the guide that comes with the headset. |
| Not listed | Not in any of Turtle Beach's pages for this headset. Say if the pages disagree. |
| — | Not a headset feature, such as the name Neap shows. |

## Whether Neap can do it

The **Neap can** column says whether Neap could offer the feature at all, so
"missing" or "unconfirmed" is never taken to mean "can be added":

| Words | Meaning |
|---|---|
| Yes | The headset does it, or Neap already does it on the PC. Say where it's kept when known only from Swarm II, as "Yes, at `0x430` (Read)". |
| Yes, on the PC | Neap can do it itself on the PC, without the headset. |
| Only on the PC | Swarm II does it on the PC, not the headset; Neap would have to build its own. |
| Only with Neap's own sound processing | Swarm II does it in audio software Neap doesn't have, such as Waves. |
| Probably, … | Likely, with the reason; to be proven. |
| Unknown | Not yet known whether the headset can do it, or how. |
| No | The headset can't do it, and the PC can't stand in. Say why. |
| Neap's own | A Neap feature that stands in for one of Swarm II's. |
| — | Nothing to do: Neap only shows it, or it doesn't apply. |

## How sure

Use these words in the **Windows** and **Linux** columns, from best to worst:

| Word | Meaning |
|---|---|
| Confirmed | A tester saw or heard it work on their headset. |
| Fails | A tester tried it and it did nothing, or the wrong thing. |
| Seen | A recording shows the headset reporting or taking the value; nobody has checked what it does. |
| Read | Known only from Swarm II's code or files, or Turtle Beach's pages. |
| Untried | Nothing yet. |
| Blocked | Can't be tried until something else works, such as the connection. |
| — | Doesn't apply. |

## What Neap does with it

Use these words in the **Neap** column:

| Word | Meaning |
|---|---|
| Has | Neap offers it for this headset. |
| Has, test | Offered in test builds only. |
| Wrong | Offered, but Neap reads or writes the wrong place or range. |
| Hide | Offered, but the headset doesn't have it; to be hidden. |
| Hidden | Kept from view on purpose until a test settles it. |
| Missing | The headset has it and Neap doesn't. |
| Not planned | Out of Neap's scope; say why in **Next**. |

## The format

Copy this skeleton for a new headset, as `docs/headsets/<model>.md`.

```markdown
# <Model>

<One or two sentences: where support stands.>

- **Hardware:** <USB product ids: transmitter, headset, dock>; firmware <version>.
- **Turtle Beach's feature list:** <link to the Swarm II article>.
- **Testers:** <Windows tester, Linux tester (system)>, on <GitHub issue link>.
- **Latest test build:** <version>.

## Connection

| System | Neap connects | How sure | Next |
|---|---|---|---|

## Features

One row per feature in Turtle Beach's list, in its order, then the physical
controls, then anything Neap shows that the headset doesn't have.

### <Section, as Swarm II names it>

| Feature | Turtle Beach | Neap can | Neap | Windows | Linux | Keys | Next |
|---|---|---|---|---|---|---|---|

## Problems

Bugs a tester found that aren't a missing feature.

| Problem | Found in | Status | Next |
|---|---|---|---|

## Next

- [ ] <one action per line, most useful first>

## Evidence

Newest first: what arrived, from whom, and what it settled.

- <date> — <what arrived>: <what it showed>.
```
