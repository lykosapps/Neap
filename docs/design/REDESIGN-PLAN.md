# Redesign plan

The current position of Neap's redesign: what is being built next and in what
order. How the screens should look and behave is in the
[design principles](PRINCIPLES.md). Finished rounds are summarised in the log
at the end.

## This round: a stronger look, and Home, Audio and Equaliser rebuilt

### What the owner chose

- **Ultraviolet accent.** `#6D4AFF` fills with white text, replacing the pale
  lavender button. Game keeps the twilight hue of the accent; chat keeps the
  moon.
- **A darker neutral.** The ground is `#08080A`, cards are `#121214`, and
  inset rows are `#1A1A1E`. There is no tint: the accent is the only colour.
- **Thoughtful spacing.** A clear rhythm: 24 between sections, 8 between
  cards, 12 inside a card, and 4 between a name and its line. Desktop stays
  compact. A small window, such as the Steam Deck's, gets one step roomier,
  with targets a finger can hit.
- **A more custom look.**
  - Segmented controls in place of radio lists.
  - Neap's own sliders, with thicker tracks and the value on the thumb while
    dragging, and its own switches.
  - The profile switcher at the top of the left menu, on Windows and Linux
    alike, in place of the row it has under the title bar today.
  - The half-moon-over-a-wave mark redrawn in ultraviolet, with a one-shape
    tray icon that stays readable at 16px.
- **The equaliser gets its own page,** with Game and Microphone tabs.
- **Audio and Microphone become one page,** Audio, in two sections: what you
  hear, and your microphone.
- **The left menu** becomes Home, Audio, Equaliser, Controls, Profiles and
  Device, with Settings at the foot: six places, as now.
- **New screens, same plumbing.** Layouts and controls are rebuilt. How the
  screens talk to the headset does not change.
- **Dark first.** Every screen is designed and judged in dark. Light gets
  matching values and passes the same checks. High contrast keeps working.

### Phases

Phase 0 and phase 2 run in this session. Phases 1 and 3 run as Sonnet
sessions in parallel, each in its own worktree and owning its own files.
The orchestrator reviews every branch, merges it and runs the screen checks.

```
0 Design sign-off ──► 1A Tokens ────────┐
                      1B Controls ──────┼─► 2 Left menu ──┬─► 3A Home ─────────┐
                      1C Mark and icons ┘                 ├─► 3B Audio ────────┼─► 4 Verify
                                                          └─► 3C Equaliser page┘
```

#### 0. Design sign-off (this session, with the owner)

- **Update the principles** for the new palette, rhythm, adaptive density and
  custom controls, and redraw the page map in `STRUCTURE.md`.
- **Mockups in dark for sign-off:** Home, the merged Audio page and the
  Equaliser page, each at desktop size and at Steam Deck size.
- **Two or three redraws of the mark** for the owner to choose from, each with
  its tray version.
- **Decision records** for the equaliser page, the merged Audio page and the
  switcher's new place.
- **Done when** the owner signs off the mockups and picks a mark.

#### 1. Foundations (three sessions in parallel)

| Session | Owns | Work |
| --- | --- | --- |
| **1A Tokens** | `Themes/Tokens.axaml`, `HighContrast.cs`, the contrast test | Ultraviolet and the darker neutral in dark, light to match, high contrast; the spacing rhythm; one step roomier below a window size |
| **1B Controls** | `Themes/Controls.axaml`, `FluentKeys.cs`, new control files | A segmented control that screen readers still announce as a choice of one; Neap's slider and switch as control themes; focus marks; 24px targets, 40px at touch size |
| **1C Mark and icons** | `Assets/`, the project file's icon entries, the tray code's icon choice | The chosen mark as source art; `app.ico` at every size; the logo; a tray icon drawn for 16 to 32px on Windows and Linux |

#### 2. Left menu (this session)

- **Move the profile switcher** to the top of the left menu. The row under the
  title bar goes, along with its edited mark.
- **Change the menu's places:** Microphone leaves and Equaliser joins. The
  Alt-letter keys are re-drawn, and the narrow, icons-only menu still works.
- **Update the screen checks** that look for the profile bar or the Microphone
  page.
- **Done when** `run.ps1 all` and the keyboard walk pass, and the switcher
  works in a narrow window.

#### 3. Pages (three sessions in parallel)

| Session | Page | Work |
| --- | --- | --- |
| **3A Home** | Home | The headset at a glance and the mix lead. Tiles that say what they do: a switch looks like a switch, and one that opens something says so. No empty half page. Keyboard shortcuts placed as a real link |
| **3B Audio** | Audio, with Microphone folded in | Two sections, What you hear and Your microphone. One row pattern throughout. Noise control as a segmented control. A short link to the Equaliser page in each section |
| **3C Equaliser** | A new Equaliser page | Game and Microphone tabs; presets beside a large curve; bands, parametric mode and the test tone. Home's Equaliser tile opens it on the Game tab |

#### 4. Verify (this session)

- **Run the full set:** `run.ps1 all`, `access`, `looks` and `atlas`.
- **Fix the pretend walk** (`tools/pretend.ps1`). It already fails three steps
  on the app as it was before the redesign, and now has to find the
  equaliser on its new page.
- **Tidy the release:** new README pictures, changelog lines and the
  decisions.
- **Run the headset checks with the owner,** then the owner decides whether to
  merge into main.

### What could go wrong

- **Lost announcements:** a segmented control built from plain buttons would
  stop screen readers saying "one of three". It is built on radio buttons with
  a new look, so it keeps them.
- **Broken checks:** the checks look for literal names: "No profile", the
  Microphone page, and the equaliser on Audio. Phases 2 and 3 update each
  check they break, in the same commit.
- **Profiles harder to find:** the switcher loses its row. It must stay one
  move from every page and announce the active profile.
- **Tray icon formats:** the tray icon goes through a third-party package on
  Windows and the desktop's own tray on Linux. Each has its own sizes, so 1C
  checks both.

## Log

### 2026-10-17: first round finished

- **The look moved onto shared tokens:** neutral grounds, flat panels,
  compact sizes and Inter, plus one list of Fluent keys for every theme.
- **Every page shares one frame;** the rail item, cards, tiles, controls,
  dial and dialogs each have control themes.
- **Checked:** the screen checks, the accessibility walk, the pretend Atlas
  Air and pictures in three themes, with no new failures against the app
  before the redesign.
- **Recorded** in `DECISIONS.md` under 17 October 2026.
