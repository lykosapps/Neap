# Design principles

How Neap's screens are designed and built. The look is inspired by MultiDrive,
another Avalonia app: calm, flat and dark first, with one accent. The way it is built
follows Avalonia's own guidance, linked from each rule. Where the two disagree,
Neap's standards in `CLAUDE.md` decide: WCAG 2.2 AA, light, dark and high
contrast, and text in the resource file.

The plan for getting there is in [REDESIGN-PLAN.md](REDESIGN-PLAN.md).

## The look in one paragraph

A deep, neutral near-black window. Large rounded cards sit on it, and
lighter rows are set into the cards. There are no shadows, gradients or
borders on surfaces: depth comes from steps in fill. Quiet text sits in a
clear rhythm of space. Thin outline icons are used throughout. One strong
ultraviolet marks only what is on, selected, in progress or the main action,
and Neap's own segmented controls, sliders and switches make it feel made
for the headset rather than borrowed from the system. The light theme is the
same structure in pale tones, and high contrast is the same structure in
system colours.

## Inspired, not copied

MultiDrive is a reference for qualities: calm, flat, roomy, and sparing with
its accent. Neap takes those qualities and keeps its own identity.

- **Neap's own values.** No colour, size or radius is lifted from MultiDrive.
  Neap's tokens are chosen for Neap and checked against its own contrast
  rules. The figures from MultiDrive in this document are measurements for
  comparison, not values to use.
- **Neap's own layouts.** MultiDrive is a task app built around a dashboard
  grid, source-and-target cards and a bottom action row. Neap is a device
  app whose controls apply at once. Its pages are shaped by its own jobs:
  the headset at a glance, the mix dial, tiles and setting rows.
- **Neap's own signature.** These elements are Neap's alone, and each one
  gets more room in the new look:
  - The half-moon mark.
  - The twilight game and moon chat pair.
  - The game and chat dial.
  - The ultraviolet accent, Neap's own, not MultiDrive's indigo.
- **Nothing borrowed.** Neap uses no MultiDrive icon, image, typeface
  choice made only to match it, or component that only makes sense in
  MultiDrive.
- **The test:** put a Neap screenshot next to MultiDrive's. They should
  look like they come from the same school of design, and not like the
  same app.

---

## 1. Surfaces

- **Three steps of depth, no more.** These are the window ground, the card,
  and the inset row inside a card. Elevation is a change of fill. A card
  never gets a shadow or a border just to stand out.
  - *Why:* MultiDrive separates everything with fill steps. It reads as
    calm and keeps contrast easy to measure.
- **A deep neutral ground.** The window, card and inset fills are neutral
  greys in the dark theme and near-whites in the light one, with no tint:
  ground `#08080A`, card `#121214`, inset `#1A1A1E` in dark. The colour in
  Neap comes from the accent and the game and chat pair, which stand out
  more for it.
- **The inset is lighter than the card in dark.** On a near-black card, a
  darker inset would vanish, so rows set into a card step up, not down.
- **Flat fills only.** No radial or linear gradients on panels or tiles.
  - *Why:* gradients make the contrast check sample a different colour at
    each point, and they are the main thing that dates the current look.
- **No outlines at rest.** Controls are soft fills one gentle step from
  the surface they sit on, with hover one step more and pressed one step
  back; the accent marks only what is selected, on or primary. Buttons,
  drop-downs, fields, segmented controls, switches and tiles draw no edge.
  The focus ring and high contrast keep their outlines.
  - *Why:* a flat, low-contrast page is calmer, and the fill step already
    says "this is a control". High contrast removes the steps, so there the
    outline is what identifies a control.
- **Window chrome blends in.** On Windows the title bar is the window
  ground, with the app's own content in it. On Linux it is the system's.
  - *Why:* Avalonia's support for extending into the title bar on Linux is
    limited ([window management](https://docs.avaloniaui.net/docs/app-development/window-management)).
- **No Mica or acrylic.** Transparency depends on the platform and the
  compositor, and it would make text contrast unpredictable
  ([window management](https://docs.avaloniaui.net/docs/app-development/window-management)).

## 2. Colour

- **Every colour is a token.** A colour is a named brush in a theme
  dictionary, with `Light`, `Dark` and a high-contrast value. No hex
  appears outside the theme files.
- **One accent, used with discipline.** Ultraviolet: `#6D4AFF` as a fill
  with white text, and a lighter `#9C88FF` for indicators with no text on
  the dark ground. Use it for on, selected, in progress, the primary action
  and the focus ring. Never use it for decoration or headings.
- **Accent in two strengths.** A strong accent is for fills with text on
  them. A darkened tint of the accent is for tracks, chips and badges. On
  the dark ground, an indicator with no text (a ring, a slider thumb or a
  progress fill) uses the lighter accent so it reaches 3:1.
  - *Example:* a deep indigo gets only about 2.5:1 on a near-black ground,
    so it fails as a ring. A lighter lavender of the same hue reaches about
    4.5:1.
- **Contrast floors.**
  - Text is 4.5:1 against the surface it sits on, which is the card, not
    the window.
  - Controls, their boundaries and focus marks are 3:1.
  - On a dark grey card, secondary text has to stay a mid grey or
    lighter. Measure it against the card, not the window.
- **Game and chat keep their own pair.** They are twilight and moon, and
  they differ in lightness as well as hue. They are not status colours and
  are never confused with the accent.
- **Status colours are for status only.** Use good, caution and critical,
  each paired with a word or an icon, never colour alone
  ([accessibility](https://docs.avaloniaui.net/docs/app-development/accessibility)).

## 3. Layout

- **One page scaffold.** Every page is a scroll area with a column of up to
  1040px, the page title, the headset notice and then sections. It is
  written once as a shared control, not repeated in each page.
- **Pick the simplest panel that fits.**
  - `StackPanel` with `Spacing` is for a single line.
  - `Grid` is for rows and columns.
  - `DockPanel` is for the shell.
  - `Panel` is for layering.
  - Don't nest panels more than three deep: one `Grid` usually replaces a
    tower of stacks
    ([choosing a layout panel](https://docs.avaloniaui.net/docs/layout/choosing-a-layout-panel)).
- **`Spacing` between siblings, `Padding` inside a container, `Margin` only
  for exceptions.** Every value comes from the spacing scale.
- **Spacing scale: 4, 8, 12, 16, 24, 32.**
- **One rhythm, so the eye can group without lines.**
  - 4 between a name and the line under it.
  - 16 inside a card, and 8 between its rows. A row that has options
    keeps them in an inset panel under it, 12 inside, with no rules.
  - 12 between stacked cards, and between the last card and a link under it.
  - 32 above a section heading, 12 below it, and 24 for the page margin.
  - The space above a heading is always larger than the space below it, so
    a heading belongs to what follows.
- **Corner radii: 12 for cards, 8 for inset rows and tiles, 6 for buttons
  and inputs, full round for pills and badges.** These are tokens, not
  constants in C#.
- **Respond to the space a component has, not the window.** Use container
  queries for size rules a style can express, such as two cards side by
  side becoming a stack. Keep code only for rules that depend on more than
  size
  ([responsive layouts](https://docs.avaloniaui.net/docs/layout/responsive-layouts)).
- **Nothing is cut off at 640px wide.** The navigation rail moves from
  names, to icons, to an overlay as the window narrows.
- **Show unavailable choices dimmed, with the reason.** Don't hide them.
  - *Why:* MultiDrive shows the boot drive greyed with a "BOOT" tag rather
    than leaving it out. It fits Neap's rule that nothing fails silently.

## 4. Type and icons

- **One type ramp, as style classes.**
  - Caption 12.
  - Body 13.
  - Card title 15, semibold.
  - Section 18, semibold.
  - Page title 24, semibold.
  - Label 11 in capitals with letter spacing.
  - No size or spacing is set inline. One class per role: no two classes
    with the same values.
- **Compact at the desk, roomier on a small touch screen.** On a desktop
  window body text is 13 and setting rows are 40px, so more of a page fits.
  In a window the size of the Steam Deck's, 1280 by 800 or smaller, the
  same tokens take one step up: body 14, rows 48, and every control a
  40px target for a finger. Nothing goes below 11 anywhere, and no target
  is smaller than 24px.
- **Inter, on both systems.** It is embedded through Avalonia's own font
  package, so Windows and Linux render the same text. The device details
  keep their monospace font.
- **Thin outline icons at fixed sizes: 16 in rows, 20 in tiles and the
  rail, 24 in headers.** They are geometry resources shown through
  `PathIcon`, and they are hidden from screen readers unless they are the
  only content.

## 5. Styling in code

- **Styles for content, `ControlTheme` for a control's whole look.**
  - Neap's own templated controls (`NavItem`, `SettingsCard`, tiles, the
    dial) each get a `ControlTheme` that can be swapped, rather than a
    `Template` set through a selector.
  - A style class is for a variation, such as `.compact`
    ([control themes](https://docs.avaloniaui.net/docs/styling/control-themes)).
- **States as nested selectors.** Write hover and pressed states as
  `^:pointerover`, `^:pressed`, `^:checked` and `^:focus-visible` inside
  the control's theme or style. Don't write separate top-level selectors
  ([styling](https://docs.avaloniaui.net/docs/basics/user-interface/styling/)).
- **`DynamicResource` for anything that changes with the theme, including
  every brush.** Use `StaticResource` only for icons, geometry and fixed
  sizes. A theme-dictionary key is found only through `DynamicResource`
  ([theme variants](https://docs.avaloniaui.net/docs/styling/theme-variants)).
- **Each token is defined once.** Fluent's own keys (buttons, sliders,
  toggles, lists) point at Neap's tokens in one place. The same token list
  feeds light, dark and high contrast, so a new token can't be missed in
  one of them.
- **Scope styles by reach.**
  - Brand tokens, type and control themes are app-wide.
  - Page-only overrides live on the page.
  - Component internals live in the component
    ([style best practices](https://docs.avaloniaui.net/docs/styling/style-best-practices)).
- **Order selectors from general to specific, and avoid broad ones.**
  Avalonia has no `!important`. When a style loses, find the later style or
  local value that wins, using DevTools (F12)
  ([style best practices](https://docs.avaloniaui.net/docs/styling/style-best-practices)).
- **Name classes in kebab-case. Name template parts `PART_…`**
  ([style best practices](https://docs.avaloniaui.net/docs/styling/style-best-practices)).
- **Visual values never live in C#.** Code adds classes. It does not build
  `Thickness`, `CornerRadius` or brushes.

## 6. Components

- **Card.** Card fill, radius 12, padding 16, no border. It has a title at
  top left and, optionally, one quiet action at top right.
- **Inset row.** Ground fill, radius 8. It is used for list rows and readings
  inside a card.
- **Setting row.** An icon, a name, an optional one-line description and
  the control on the right. It is 40px high, and the whole row is
  the click target where that makes sense.
- **Left menu.** The profile switcher sits at its top, the same on Windows
  and Linux, showing the active profile. Below it: Home, Audio, Equaliser,
  Controls, Profiles and Device, each an icon and a name. The selected item
  is a rounded pill one fill step lighter, with its accent bar. Settings
  sits at the foot. In a narrow window the menu shows icons only, and the
  switcher becomes one profile icon.
- **Segmented control.** A choice of two to four short options, shown as one
  pill with the chosen segment filled in the accent. It is built on radio
  buttons, so a screen reader still says which of how many is chosen. Use it
  in place of a radio list wherever the options fit on one line.
- **Home tiles.** Every tile on Home is a card with one pattern, so nothing is
  learned twice: the icon at top left, the title, then its controls, and no
  click target on the card itself. On or off is Neap's switch at the top
  right, and the card takes the soft on tint while it is on. A choice among a
  few is a segmented control under the title. A longer list is a drop-down.
  Where more settings live on another page, an accent link sits at the foot.
  A control that cannot be used yet is faded and disabled, not hidden, such as
  the sound of Superhuman Hearing while it is off. High contrast keeps the
  outline and leaves the on card unfilled, since the switch says it is on.
- **Slider.** Neap's own: a thicker track filled in the accent up to the
  thumb, and the value shown on the thumb while it is dragged. The value
  also stays as text beside the slider.
- **Switch.** Neap's own shape: a pill track, filled in the accent when on,
  with its state also said in words beside it.
- **Buttons.**
  - Primary is the ultraviolet fill with white text, in both themes. It
    appears at most once per view.
  - Secondary is outlined.
  - Quiet is text only.
  - Buttons don't carry icons unless the button is icon only, and then it
    has a name.
- **Controls apply at once.** Neap's settings take effect when changed, so
  pages have no Save bar. A page shows a primary action only when there is
  a real job to start, such as an update or a recording.
- **Dialogs.** Used rarely. They are 440px wide, with one primary and one
  secondary action, built from the same card and button parts.

## 7. Motion

- **Brief, purposeful and never looping.**
  - Page changes are a cross-fade of about 150–250ms.
  - State changes are transitions of 100–300ms
    ([page transitions](https://docs.avaloniaui.net/docs/graphics-animation/page-transitions),
    [style best practices](https://docs.avaloniaui.net/docs/styling/style-best-practices)).
- **Follow the system's animation setting.** When it is off, change state
  instantly. This is already decided (Decisions, 8 October 2026).
- **Live readings may move, but stay calm.** Meters and dials update
  smoothly. They never pulse or flash.

## 8. Accessibility, as built

- **Every interactive control has a name.** It comes from the resource
  file, through `loc:Uid`. Give an icon-only control an explicit
  `AutomationProperties.Name`.
- **Mark decorative icons `AccessibilityView="Raw"`.** Mark page and
  section titles with `HeadingLevel` 1 and 2
  ([accessibility](https://docs.avaloniaui.net/docs/app-development/accessibility)).
- **Every templated control styles `:focus-visible`.** A custom
  `ControlTheme` that drops the focus mark is a bug
  ([accessibility](https://docs.avaloniaui.net/docs/app-development/accessibility)).
- **Targets are at least 24×24px.** Tab order follows reading order, and
  Tab never gets trapped.
- **Changing readings announce politely.** Use `LiveSetting="Polite"` for
  values that change on their own, such as battery or connection.
- **Every screen in three themes.** Light, dark and high contrast each work
  and switch live. A new surface is not done until it has been seen in all
  three.

## 9. Words

- **How Neap writes is in [COPY.md](COPY.md).**
- **All text is in `Strings/en-US/Resources.resw`.** Write whole sentences,
  never fragments joined in code.

---

## What we take from MultiDrive, and what we don't

| Take, as a quality | Leave |
| --- | --- |
| Fill-step depth, large radii, flat surfaces | Its exact colours, sizes and radii |
| A neutral ground, so the accent carries the colour | Its indigo accent: Neap has its own ultraviolet |
| One disciplined accent, with a darker tint for tracks | 11–12px body text: Neap's is 13 |
| Slim icon-and-name rail with a pill selection | Dark only: Neap keeps light and high contrast |
| Unavailable choices shown dimmed with a reason | Its dashboard grid, source-and-target cards and bottom action row |
| Title bar that blends into the window | Its pill-shaped task progress bars |

Sources: MultiDrive's product screenshots on its site, and its maker's article
[The story behind MultiDrive](https://dev.to/empiree/the-story-behind-multidrive-eo8),
which says its controls were built on Avalonia without a third-party library.
Neap does the same.
