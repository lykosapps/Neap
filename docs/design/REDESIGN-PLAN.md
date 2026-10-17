# Redesign plan

Brings Neap's screens to the [design principles](PRINCIPLES.md). The screens
change, and what they do does not. Every setting, word and automation name
stays as it is unless a step says otherwise.

## Where it starts

What the current code gives the redesign:

- **Already in good shape.**
  - Every brush is a token.
  - No hex appears outside the theme files.
  - All text is in the resource file.
  - The rail already adapts to width.
  - Avalonia 12.1.3 with the Fluent theme, and no third-party UI library.
- **What makes a restyle hard.**
  1. **Fluent overrides are written out three times.** There are about 80
     aliases for dark, the same again for light, and a third time as lists
     in `HighContrast.cs`.
  2. **High contrast is built in code.** Several surfaces have never been
     seen on screen in it, and no check covers it.
  3. **There are no `ControlTheme`s.** Custom controls get their template
     through selectors.
  4. **Sizes and radii are literals.** Spacing, radii and icon sizes are
     typed in AXAML and repeated as constants in C#.
  5. **The page skeleton is copied into all seven pages.**
  6. **Panels and tiles use gradients.**
  7. **Type classes overlap.** Three of them share 28/semibold.
  8. **Two title-bar values are hard-coded.** The 40px height and a 138px
     caption-button width.
  9. **Several controls have no focus style of their own.** The rail and
     setting cards rely on Fluent's.
  10. **Layout is built in code.** Pages and controls have no view models,
      so layout is partly written in C#.
- **Out of scope.** The redesign does not introduce view models. It is a
  restyle, and the code-behind pattern stays. Moving to MVVM would be its
  own decision.

## Phases

Phases 1 and 2 run one after the other, because everything after them
depends on them. Phases 3 and 4 run as parallel sessions. Each session owns
its own files, so no two sessions touch the same file.

```
1 Foundations ──► 2 Shell ──┬─► 3A Cards and tiles ──┐
                            ├─► 3B Fluent controls ──┤
                            ├─► 3C Dial and curves ──┼─► 4 Pages (4 in parallel) ──► 5 Verify
                            └─► 3D Dialogs and bars ─┘
```

### 1. Foundations (one session)

- **Split the theme files by job.** Create `Tokens.axaml` (colour, spacing,
  radius, type and icon sizes) and `Fluent.axaml` (Fluent's keys pointed at
  Neap tokens, once). Each later session then owns its own file.
- **Write the new palette as tokens.**
  - Use flat, neutral fills with no violet tint: ground, card, inset.
  - Choose Neap's own values, not MultiDrive's. Keep Neap's violet as the
    accent, and keep the dark theme's pale lavender primary button with
    dark text.
  - Use the accent in its strong and tint strengths.
  - Keep game and chat, the tones and the text levels.
  - Set light, dark and high-contrast values side by side.
- **Define Fluent's overrides once.** Start with a short spike: can one
  alias dictionary serve light and dark? If not, generate both from one
  list. Either way, high contrast reads the same list.
- **Add the spacing, radius and icon-size scales as resources.** Merge the
  type classes to one per role.
- **Choose the typeface and embed it.**
- **Done when** the app looks broadly the same as today, but every value
  comes from a token. Check with `run.ps1 all`, plus screenshots in all
  three themes.

### 2. Shell (one session)

- **Make the page scaffold a shared control**, and use it in all seven
  pages.
- **Restyle the rail** with a pill selection, groups, an optional badge,
  and its own `ControlTheme` and focus style.
- **Rework the title bar.** It blends into the window, and its height and
  caption width are measured, not hard-coded. Linux keeps the system bar.
- **Done when** `run.ps1 all` passes, the rail works by keyboard in all
  three themes, and the window narrows to 640px with nothing cut off.

### 3. Components (four sessions in parallel)

| Session | Owns | Work |
| --- | --- | --- |
| **3A Cards and tiles** | `Themes/Tiles.axaml`, `SettingsCard`, `SettingRow`, `SettingTile`, `SpatialTile`, `NoiseControlPanel`, `Notice` | Card, inset row and setting row as `ControlTheme`s; drop gradients; one tile shell in place of two |
| **3B Fluent controls** | `Themes/Fluent.axaml` | Buttons (primary, secondary, quiet), toggle, slider, combo box, radio, check box, expander and flyout restyled through Fluent's keys and a few nested-selector styles |
| **3C Dial and curves** | `Themes/Dial.axaml`, `Themes/Curves.axaml`, `GameChatDial`, `EqualiserPanel`, `ParametricPanel`, `MixPanel` | Flat track and fill, the accent rules for indicators on dark, and inline letter spacing removed |
| **3D Dialogs and bars** | `NeapDialog`, `UpdateBar`, `ProfileBar`, `HeadsetStatusStrip`, the `*Dialogs.cs` files | Built from the card and button parts; `Thickness` and radius constants in C# replaced by classes |

- **Each session is done when** its controls show correctly in all three
  themes, the check for its screen passes, and `run.ps1 access` finds
  nothing new.
- **Run the screen checks one at a time.** Only one practice copy of the
  app can run at once, so sessions queue for `tools/ui` rather than
  running together.

### 4. Pages (four sessions in parallel)

| Session | Pages |
| --- | --- |
| 4A | Home |
| 4B | Audio, Microphone |
| 4C | Controls, Device |
| 4D | Profiles, Settings |

- **Each page is rebuilt from the parts above:** cards, inset rows, setting
  rows, and container queries for side-by-side cards. No new tokens or
  styles are added at this stage. A gap goes back to phases 1 to 3.
- **Done when** the page's check passes and screenshots in three themes
  match the principles.

### 5. Verify (one session)

- **Run the full set.** That is `run.ps1 all`, `run.ps1 access` in both
  themes, `tools/pretend.ps1` and `run.ps1 atlas`.
- **Add the missing high-contrast check.** It is a new script beside the
  others that walks every page in high contrast and fills the gap noted in
  the backlog.
- **Run the headset checks with the owner,** because the dial, meters and
  tiles draw live readings.
- **Compare Neap with MultiDrive, side by side.** The two should look like
  the same school of design, not the same app. Anything that reads as a
  copy goes back to the phase that made it.
- **Refresh the screenshots in `docs/screenshots`.**
- **Record the redesign in `DECISIONS.md`.** Add a `CHANGELOG.md` line per
  phase that changes what a person sees.

## Rules for every session

- **Keep the names the checks look for.** Automation names, ids and
  visible words stay the same. The checks look for literal English such as
  "Update and restart" and "Not now", and for the ids `PrimaryButton`,
  `CloseButton` and `profile_working`.
- **Commit small, with the check passing.** Each commit builds and passes
  its tests, and follows the commit rules in `CLAUDE.md`.
- **Compare screenshots before and after,** in all three themes, for every
  surface the session touched.

## Open questions for the owner

1. **Typeface.** Embed Inter (Avalonia's own font package) for the same
   look on Windows and Linux, or keep each system's font?
2. **How far to follow MultiDrive's density.** Its cards are airy and its
   text small. The principles keep body text at 14. Is that the right
   balance?
