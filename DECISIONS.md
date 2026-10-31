# Decisions

## 2026-10-31 — A release's changelog is a short summary by theme

- **Options:** keep every line under the version, with Highlights first, as
  0.3.0 did; a short summary by theme only.
- **Chosen:** the short summary, the owner's call. Lines still go under
  Unreleased as changes are made, and are bundled at the release.
- **Why:** the release page and the app's notes are read by people deciding
  whether to update; three hundred lines hid what mattered. The commit
  history keeps every change in detail.
- **Revisit if:** testers or users need the detail to tell whether a
  particular bug is fixed.

## 2026-10-31 — The Linux AppImage updates itself

- **Options:** keep opening the release page; a delta update tool for
  AppImages (AppImageUpdate); download the whole new AppImage and rename it
  over the running one, as Windows unzips over its folder.
- **Chosen:** the whole AppImage, renamed over the old one under the same
  name, after its SHA-256 matches the one published in the same release.
  Only a copy run from its AppImage does this; any other Linux copy still
  opens the release page.
- **Why:** an AppImage is one file, so a rename puts it in place whole or not
  at all, and the running copy keeps reading the image it started from, so
  the crash that ruled updates out on 7 October does not arise. The delta
  tool would need another program fetched and run, and a file more on every
  release, to save part of one download. The same path keeps the sign-in
  entry working. Proven against the real 0.3.0 release in a Linux virtual
  machine (read, hash, swap, and the new file runs); a release-to-release
  update in the app is proven only once two releases have it.
- **Revisit if:** an update leaves a copy that does not start, or a desktop
  shows the old version's name for the file and people are confused by it.

## 2026-10-31 — Parametric is on for the Stealth Pro II in 0.4.0

- **Options:** keep it held back until its headset checks are done; turn it
  on now, with the checks still to pass before the tag.
- **Chosen:** on now, the owner's call. `FirmwareEdit.ParametricReleased` is
  on, so the Stealth Pro II's Equaliser page offers it; a headset without
  live bands does not. The Atlas Air gets it in a test build only, after its
  firmware was read to have the same controls, until a tester has heard it.
- **Why:** SP2Tools proved the live bands audible on the owner's headset,
  wired and through the dock, and Neap read and wrote the stored record
  there. What is left is checks of Neap's own flows on the headset.
- **Revisit if** a headset check fails: turn it off again before the tag.

## 2026-10-31 — The Atlas Air ships with every function

- **Options:** ship only what its testers confirmed, as the rule of
  8 October says, which hides both equalisers, auto shut-off, mute and
  custom presets, and everything on Linux; ask the testers for one more
  round first; or ship every function Neap has for it.
- **Chosen:** every function, the owner's call. Neap knows the Atlas Air in
  every build; `HeadsetModels.AtlasAirReleased` is gone.
- **Why:** both connection fixes are confirmed, on Windows (17 minutes) and
  Linux (10 minutes, and every start with the PC in a day's USB trace). The
  functions not yet confirmed by a tester are seen in recordings to take
  their values, and use the Stealth Pro II's encoding. Hiding the equaliser
  would take away what people use the app for.
- **This sets aside, for the Atlas Air only,** the decision of 8 October that
  a release carries only what a tester confirmed.
- **Revisit if** a tester reports a function doing nothing or the wrong
  thing: hide it in Core until it is fixed.

## 2026-10-31 — The routing warning sets Windows' defaults when pressed

- **Options:** keep opening Windows' Sound settings and leave the change to
  the person; open the old Sound panel, where the device for calls can be
  chosen; or make the change from the warning.
- **Chosen:** on Windows, the warning's button, "Choose for me", makes
  each device it names the default for what it names it for: sound, the
  microphone, or calls. If Windows refuses one, Neap logs it and opens
  Sound settings as before. Linux keeps opening its sound settings.
- **Why:** seen on the headset on 31 October: with the USB-C cable in,
  the warning said to choose the cable for calls, but Windows 11's Settings
  app has no way to choose the device for calls, so the warning could not be
  acted on from where it sent the person. Neap already set the default
  device through the same Windows interface for the audio format. The
  earlier stance, that changing the default is too unreliable to do on
  anyone's behalf, came with the move from the first app and had no case
  behind it; the button acts only when pressed, and falls back if refused.

## 2026-10-30 — The app's folder is no longer moved from the name before Neap

- **Options:** call the move from the earlier folder at startup again, or
  remove it.
- **Chosen:** removed. Neap keeps its settings, log and volume journal only
  in its own folder.
- **Why:** nothing has called the move since the app was rebuilt in
  Avalonia, and every public release, from 0.1.0 on, was named Neap and
  kept its folder under that name, so no one installing a release has a
  folder under the earlier name to move.

## 2026-10-30 — The headset's output is found among every output, not only the default

- **Options:** keep using only Windows' default output, and say so when it
  is not the headset; or look further, safely.
- **Chosen:** the default output if it is the headset's; otherwise the
  default output for calls, if that is; otherwise the headset's output when
  there is only one. With two headset outputs and neither a default, none is
  used, as the mix cannot tell which is in use. The log says which way it was
  found, or why not, whenever that changes. The same rule serves Linux,
  which has no default for calls.
- **Why:** the owner's PC had its speakers as the default and the headset
  as the default for calls, where Discord plays, so the chat apps list was
  empty and the mix off, with nothing saying why. Only the default was used
  so that two transmitters plugged in at once could not leave the mix on the
  one not listened to; the new rule keeps that guard.

## 2026-10-30 — Both built-in measurements are plain choices, and Auto-EQ needs both curves shown

- **Chosen:** the Measured list offers the Stealth Pro II with noise
  cancellation off and with it on as two rows, off by default, and no longer
  follows the headset's setting. Auto-EQ is on only while a measurement and a
  target are both shown, with a tip saying so while it is off. Pre-gain's
  row says what it is, the level the headset applies before its equaliser,
  in place of its hearing caution, and the boost warning names no figure.
- **Why:** the owner, testing on the headset: a list the person chooses
  from needs no rule behind it, Auto-EQ fitting to a hidden curve was
  unexpected, and the warning's figure and the caution's mark read as more
  alarming than they are. This replaces the parts of 29 October's entries
  that say otherwise.

## 2026-10-29 — Curves are chosen from lists, Auto-EQ uses the chosen ones, and filters import as a preset

- **Chosen:** the Curves menu lists the built-in measurement and target by
  their real names (the Stealth Pro II on a B&K 5128, with noise cancellation
  on or off; 5128 DF + Tilt) beside every file loaded, with one chosen in
  each and Show kept apart from the choice. Auto-EQ uses the chosen curves
  whether or not they are shown, and shows both after it fits, so it is
  never offered greyed out; a headset with no measurement has no Auto-EQ.
  Importing Equalizer APO or AutoEq filters is "Import preset" in the
  preset menu.
- **Why:** the owner, on a test build: the built-in target was not named, a
  target loaded replaced the last one, a greyed-out Auto-EQ did not say
  why, and imported filters are a preset of bands, not a curve.

## 2026-10-29 — Parametric draws the headset's measurement and a target, and Auto-EQ fits one to the other

- **Options:** the bands' curve alone; a measurement and a target drawn
  behind it, from files only; or the same with a built-in pair, and Auto-EQ
  to fit bands to the gap.
- **Chosen:** the Parametric curve runs the full width, with the band's
  dials beside the list of bands. Behind it, Neap draws the Stealth Pro II's
  measured response (noise cancellation off, or on when it is on), that
  response with the bands added, and a target: the B&K 5128 diffuse field
  with a tilt. Each can be hidden, and either can be replaced by a file of
  frequency and level lines. Auto-EQ fits up to ten bands to the gap, with
  no boost above 6 dB and nothing fitted above 10 kHz, as one step that
  Undo takes back.
- **Why:** the owner asked to see how changes line up against the headset's
  own sound and to work towards a target. A target only means something
  against a measurement from the same head, so the built-in target is made
  for the 5128 that measured the headset; Harman's curve is for another
  head. The built-in data is plain numbers with no site's name in Neap;
  other curves, such as a review site's, can be loaded by hand. Boosts stop
  at 6 dB so a fit leaves the headset room before it squashes the sound,
  and above 10 kHz a measurement depends too much on how the ear cup sits
  to fit to.

## 2026-10-29 — Another app's ten-band change ends the Parametric mode

- **Options:** keep following another app's gains into the parametric shape;
  ignore them and put the shape back; or go back to the ten-band equaliser
  with that app's gains and keep the shape.
- **Chosen:** when the phone app, Swarm II or anything else changes the ten
  bands' gains while Parametric is in use, Neap goes back to the ten-band
  equaliser with those gains on the stock bands, keeps the parametric shape,
  and says so on the Equaliser page with a way back to Parametric. A change
  made while Neap was closed is seen when the headset next connects.
- **Why:** on the owner's headset, choosing a phone preset that cuts the
  treble came out as a treble boost: its gains, meant for fixed frequencies,
  landed on parametric bands at other frequencies. A gain means nothing away
  from its own frequency. One equaliser active at a time, the newest choice
  winning, is the rule that holds whether or not Neap is running.
- **Changes:** "follows a phone's gains" (27 October). The shape is no longer
  changed by another app.
- **Revisit if:** the headset's Mode button turns out to step presets in a way
  people use with Parametric on, which this rule would also end.

## 2026-10-28 — Parametric is turned on from the Equaliser page, through the dock or the cable

- **Options:** keep turning it on from the Firmware page over the cable;
  have Neap switch to the cable for that moment when both are connected; or
  turn it on from the Equaliser page on whichever connection is in use.
- **Chosen:** choosing Parametric on the Equaliser page asks once, then
  clears the gain correction, through the dock or the cable; declining goes
  back to the ten bands. Turn off, in the Parametric editor, puts the stock
  correction and bands back. The switch is refused when the headset's battery
  is under 10% and it is not charging. The Firmware page keeps only the
  firmware edits (the Mode button's transparency step and the light), which
  write the firmware and need its warnings.
- **Why:** the owner asked for the most convenient way. A test build read and
  wrote the record through the dock on the owner's headset (headset tracker,
  28 October), so the cable is not needed. The write changes a stored setting,
  not the firmware, and the same record is written on every band change.
  Power lost mid-write is the one case not traced, so a low battery is not
  risked.
- **Changes:** "Parametric is enabled from the Firmware page" (27 October):
  the Settings switch and the Firmware page are no longer the way in.
- **Revisit if:** a write through the dock is ever seen to land wrong, or a
  headset is found that does not pass the record's reply back.

## 2026-10-28 — Parametric shapes are saved as Neap's presets, and a profile holds the shape itself

- **Options:** named parametric presets on the Equaliser page with profiles
  that remember them; profiles only, each holding the shape on screen; or
  named presets only, with profiles holding the ten-band preset as before.
- **Chosen:** both. Parametric presets are saved in Neap's settings, with no
  limit, and chosen, saved over, renamed and deleted from a bar above the
  editor. A profile remembers whether the game equaliser was in Parametric
  and, if so, the ten bands themselves; switching to it puts them back and
  names the preset they match.
- **Why:** the owner's call. The headset keeps gains only, so the presets
  have to be Neap's. Holding the shape by value keeps a profile working after
  its preset is renamed or deleted, and a profile saved while the shape was
  unsaved still brings it back.
- **Revisit if:** profiles need to follow later edits to a preset rather than
  keep the shape they were saved with.

## 2026-10-27 — One level control: the loudness boost, extended below stock

- **Options:** a separate Level control on the equaliser page; the existing
  loudness boost extended below stock, with the equaliser advising.
- **Chosen:** the loudness boost goes from 12 dB below stock to 18 dB above,
  in 3 dB steps. The equaliser page shows a notice and a "Lower level by"
  button only when its biggest boost at the current level would reach the
  headset's compressor (-10 dB); nothing changes the level on its own.
- **Why:** the boost and a separate Level would both set the headset's one
  input level and fight over it. A person who raised the level for a quiet
  headset may want the squash, so the equaliser advises and never decides.
  Lowering from stock is the safe direction (SP2Tools); the reach of the
  compressor is SP2Tools' arithmetic and has not been listened to.
- **Revisit if:** listening shows the notice fires when nothing is squashed,
  or the name "Loudness boost" misleads on a negative value.

## 2026-10-27 — Parametric is enabled from the Firmware page, and only the Stealth Pro II has it

- **Options:** ship parametric at once with the headset's stock gain correction
  left on (about 2 dB of error at the edges of a boost, faint dips where a band
  has moved) and offer the fix later; or make clearing that correction part of
  turning parametric on. For other headsets: keep the fitted mode, or take it away.
- **Chosen:** the correction is cleared as part of enabling parametric. The
  Equaliser page shows Parametric switched off, with a way to Settings, where
  the "Firmware edits" switch is highlighted; turning it on shows the Firmware
  page, where the user turns parametric on, and the page says that Swarm II's
  own presets then play louder in the mids (up to about 5 dB for several
  boosts together). The Settings switch stays. The Firmware page ships in
  0.3.0 cut down to that one item and its Restore; flash edits stay held back.
  The fitted Parametric mode is taken away from every other headset.
- **Why:** the owner's call: a parametric equaliser that is slightly wrong
  until a later step would be confusing, and the fitted mode is not truly
  parametric.
- **Changes:** "Firmware edits wait for a later release" (22 October), for the
  settings-record write only: flash edits still wait. The Settings switch can
  no longer be hidden for a Stealth Pro II.
- **Revisit if:** clearing the correction proves hard to undo from the backup,
  or the headset can be made exact without writing to its settings record.

## 2026-10-27 — The Stealth Pro II gets a true parametric equaliser, and follows a phone's gains

- **Options:** keep the fitted Parametric mode beside a true one; replace it on
  this headset; and, for the phone app's gain edits, put Neap's shape back,
  or follow the phone's gains and show them.
- **Chosen:** on the Stealth Pro II the true parametric equaliser (ten bands,
  each with a type, frequency, width and gain, changed live) replaces the
  fitted mode, and the user picks between it and the ten-band equaliser.
  The shape stays on the headset when Neap closes, so it plays over Bluetooth
  too; only choosing the ten-band equaliser puts the stock frequencies,
  widths and types back. When another app, such as the phone app, changes the
  gains, Neap keeps its frequencies, widths and types, takes the new gains
  into its saved shape and shows them.
- **Why:** SP2Tools proved live frequency, width, type and gain changes on
  the headset, wired and through the dock, and Bluetooth audio passes through
  the game equaliser (owner). The phone app saves a preset by deleting it and
  writing it again, so it edits the same ten bands (seen over the cable,
  27 October). Neap cannot block it while closed, so one rule that holds
  whether or not Neap is running is easier to trust than a block that works
  only while it runs.
- **Revisit if:** the phone app also writes the equaliser when it connects
  (not yet checked), or the Mode button's preset step turns out to move the
  bands, which Neap would then have to block.

## 2026-10-26 — The band view draws a line through the gains, not the model's sum

- **Options:** draw what the filter model says the ten bands play together;
  draw it faintly behind a line through the gains; draw only the line
  through the gains, as before.
- **Chosen:** only the line through the gains, on the gains' own ±9 dB scale.
- **Why:** what the bands play together is not known. The filter model
  says neighbouring bands add up past the gains, but it rests on an
  undocumented filter type (SP2Tools has since read the type codes from
  the firmware: 4 is peaking, 6 and 7 are shelves), and SP2Tools reads an
  active compressor after the equaliser (read from the code, not proven)
  that squashes a loud tone by about 4, 7 and 10 dB at +6, +9 and +12 dB of
  boost, so what plays also depends on how loud the sound is, and a boost
  mostly raises compression, not level. The only measurement, on 28 September, was a desk
  microphone held to an unsealed ear cup and is not trusted (FINDINGS.md).
  The line through the gains shows what was set and claims nothing about
  what is heard. The parametric view still draws from the model, as the
  only way to choose ten gains for a curve.
- **Revisit if:** a sealed, calibrated measurement shows what neighbouring
  bands add up to.

## 2026-10-23 — "Keep high-resolution audio" is out of 0.3.0

- **Options:** keep it as it was; keep it but skip the reset when nothing is
  playing and reword it; take it out of 0.3.0 and look into the dock first.
- **Chosen:** take it out. `RingRefresh.Released` is off, so the Device page
  has no switch for it and nothing sets the output format again, whatever
  the settings file says.
- **Why:** a USB capture of the dock (FINDINGS.md) showed the ring turns
  white when the dock is told a microphone stream has started, and purple
  again only when it is told a playback stream has started at 96 kHz. The
  reset works only while something is playing, and restarts every
  application's sound when it does. The owner's call was to remove it for
  now rather than ship it half understood. The ring then turns purple by
  itself the next time anything plays.
- **Revisit when:** the desk investigation of the dock's ring code in
  BACKLOG.md says whether the dock can be stopped from changing it.

## 2026-10-23 — 0.3.0 ships without a Narrator pass

- **Options:** read every page and dialog with Narrator before 0.3.0, as
  CONTRIBUTING.md's release check 12 says; ship without it.
- **Chosen:** ship without it. The accessibility check (`access`) passed on
  every page in both themes, which covers names, target sizes, Tab order,
  focus marks and contrast; what it cannot see is how a screen reader reads
  a page.
- **Why:** the owner's call, to get the release out.
- **Revisit when:** the next release, or a report from someone using a
  screen reader; the redesigned pages are the ones to read first.

## 2026-10-23 — The Atlas Air waits for a later release

- **Options:** ship 0.3.0 with the Atlas Air hidden in part, only what its
  testers have confirmed; keep the Atlas Air out of 0.3.0 altogether.
- **Chosen:** keep it out. `HeadsetModels.AtlasAirReleased` is off, so Neap
  does not know the Atlas Air: its hardware, speakers and microphone are
  those of a headset it has no support for. The code and tests for it stay,
  and its changelog lines are kept in `docs/headsets/atlas-air.md`.
- **Why:** the owner's call: 0.3.0 officially supports the Stealth Pro II
  only, as the Atlas Air's testers have not yet confirmed what Neap does on
  it, and its latest fix has had no reply.
- **Revisit when:** the testers confirm 0.2.1.8 or a later test build.

## 2026-10-23 — The headset is called what its firmware calls it

- **Options:** the name the headset keeps at `0x220`; the model's name;
  the USB product string of the device Neap talks through, as given or with
  words taken off.
- **Chosen:** the USB product string, as given, and the model's name where
  none is given. Through the white Xbox dock that is "Stealth Pro II Xbox";
  on the cable alone it is "Stealth Pro II Xbox Headset".
- **Why:** every headset reports "My Headset" at `0x220` until renamed, and
  no way to rename it has been seen, so it named nothing. The product string
  is the name Windows and a phone show. The owner chose to show it whole
  rather than trim it to match across devices.
- **Revisit if:** a way to rename the headset is found, or a tester's device
  gives a name that reads badly.

## 2026-10-22 — On Linux, Neap's access rule also keeps transmitters awake

- **Options:** rely on Neap's one restart of a silent transmitter; have the
  access rule stop the system suspending Turtle Beach's USB devices too.
- **Chosen:** the rule sets `power/control` to `on` for them, and Neap
  offers the new rule wherever an older one is installed. Before restarting a
  silent transmitter, Neap asks it whether a headset is linked, and leaves
  one that says no alone.
- **Why:** SP2Tools read in the Atlas Air's firmware that its transmitter
  drops every reply once its USB state is lost, with a suspend the likely
  cause, and only a reset brings it back (F115). The reset switched the
  tester's headset off as it came back, so a transmitter that never sleeps
  is better than one that is reset. An older rule still gives access, so
  nothing else would notice it.
- **Revisit when:** a real Linux PC shows whether the rule keeps a
  transmitter answering; the VM tried so far never suspended it.

## 2026-10-22 — Firmware edits wait for a later release

- **Options:** ship the Firmware page behind its Settings switch in the
  next release; keep it out of the release altogether until Neap can write
  a real headset.
- **Chosen:** keep it out. `FirmwareEdit.Released` is off, so the page and
  its switch are nowhere in the app, whatever the settings file says, and
  nor is the Stock or Edited firmware choice for the Mode button, which
  works as on stock firmware until the release. Only
  a pretend run asked to open the page shows it, against the pretend
  headset.
- **Why:** the owner's call: release the improvements made so far first,
  and ship firmware editing once the writer is built into Neap.
- **Revisit when:** Neap's own writer is proven on a real headset.

## 2026-10-22 — Firmware edits are ticked and applied as one plan

- **Options:** an Install and a Remove button for each edit; the edits
  ticked as a list, with Neap working out what to write.
- **Chosen:** the list, as SP2Tools' delivery spec sets out. Each section of
  flash a changing edit touches is written whole, as stock with every ticked
  edit in it, so two edits in one section never undo each other, and
  Restore to stock is unticking everything. The plan is shown before Apply.
- **Also chosen:** an edit's sections are written in an order that leaves a
  headset that starts wherever the write stops: new code before the call
  that reaches it, and the call put back first. The spec writes and checks
  one section at a time, and the brick of 15 October was a call into flash
  with nothing in it.
- **Revisit if:** SP2Tools' list of edits gives an edit whose sections have
  no such order.

## 2026-10-22 — Firmware edits get their own page, behind an opt-in

- **Options:** leave installing firmware edits to SP2Tools; build the screens
  in Neap now, either on the Device page or on a page of their own; write to
  real headsets straight away, or run the whole flow on the pretend headset
  first.
- **Chosen:** a Firmware page of its own, in the menu only once "Firmware
  edits" is switched on in Settings, off by default. The first version runs
  every step and failure on the pretend headset; on a real headset it
  shows which edits fit and writes nothing. Real writes come next, ported
  from SP2Tools' flasher.
- **Why:** the owner's call: firmware edits are part of Neap from here on.
  An edit can stop a headset starting, so nothing about them shows until
  asked for, every write is backed up first, and a failure from the write on
  is undone from the backup.
- **Revisit if:** an edit leaves a headset Neap cannot put back, or a tester
  without the recovery tools needs one.

## 2026-10-22 — Touch size goes by the screen, not the window

- **Options:** keep drawing at touch size whenever the window is the size of
  the Steam Deck's screen or smaller; go by the size of the screen instead.
- **Chosen:** the screen. Touch size is for a screen of 1280 by 820 or less,
  in device-independent pixels, for the main window and the mini panel
  alike.
- **Why:** the owner's window, made small on a large screen, drew its
  switches and sliders at a finger's size, which looked too large with a
  mouse. This replaces the reason in "Ultraviolet on a darker neutral,
  roomier on small screens": a small window is not a touch screen.
- **Revisit if:** someone using a touch screen larger than the Deck's asks
  for the larger controls.

## 2026-10-22 — The mini panel as quick tiles

- **Options:** keep Home's controls in the panel at full size; the same
  controls made small, as a compact list with small segmented controls;
  quick tiles, as the system's quick settings and the Steam Deck's quick
  menu have.
- **Chosen:** quick tiles. The mix is a slim balance slider, game at the
  left and chat at the right, in place of Home's dial. The microphone,
  noise cancellation and spatial sound are three tiles in a row: a press
  turns one off, or back on the way it was last on, and the arrow in a
  tile's corner lists its choices. The equaliser preset is one compact row
  under them. The panel is 320 wide.
- **Why:** the owner found the panel full-size, not mini: it reused Home's
  controls, and the dial alone took a third of it. Tiles are the most
  compact form, and a pattern people know from their system. A press that
  turns a setting off or back on, with a list for the rest, keeps every
  choice one step away, where stepping through them would hide what comes
  next mid-game.
- **Revisit if:** people miss seeing noise cancellation's three modes at a
  glance, or want the dial back in the panel.

## 2026-10-17 — A mini panel on the tray icon's left click

- **Options:** leave a click on the icon opening the window; a menu of
  actions on the icon; a small panel by the notification area with the
  mid-game settings, with or without moving the window to the icon's menu.
- **Chosen:** a left click opens the mini panel: the game and chat dial, the
  microphone switch, noise cancellation (Off, Cancelling, Transparency) and
  the equaliser preset, under the headset's name, state and battery, with a
  button to open the window. It closes on Escape or when focus leaves it,
  like the system's volume flyout. The window moves to "Open Neap" on the
  icon's menu, which also lists "Mini panel". Where the desktop shows no
  icons there is no panel.
- **Why:** someone mid-game wants to change the mix or mute without the
  window taking the screen and their focus from the game. Those four are
  what Home's tiles already put first, so the panel is those controls made
  small, with the same logic behind them. A click that opened the window
  made the icon a slow way to reach them.
- **Revisit if:** people miss opening the window with one click, or ask for
  Superhuman Hearing or the profile switcher in the panel.

## 2026-10-17 — The equaliser gets a page, and Audio takes in Microphone

- **Options:** keep both equalisers at the foot of Audio and Microphone;
  fold them to one row there; give them a page of their own, with or
  without merging Audio and Microphone.
- **Chosen:** an Equaliser page with Game and Microphone tabs, and one Audio
  page in two sections, What you hear and Your microphone. The left menu
  becomes Home, Audio, Equaliser, Controls, Profiles and Device, with
  Settings at the foot.
- **Why:** each equaliser was bigger than everything else on its page put
  together, and sat below the fold, so the everyday settings above it were
  scrolled past to reach it and it was scrolled to every time. Tuning sound
  is its own, occasional job. Without their equalisers, Audio and
  Microphone are a handful of rows each, and read naturally as one page.
  The menu keeps its size.
- **Revisit if:** people coming from Swarm II can't find the microphone
  settings, or the merged page grows past two screens.

## 2026-10-17 — The profile switcher moves to the top of the left menu

- **Options:** keep the row under the title bar; put it in the title bar on
  Windows and somewhere else on Linux; the top or the foot of the left menu
  on both.
- **Chosen:** the top of the left menu, on Windows and Linux alike.
- **Why:** the row cost a full line on every page to show one name. Linux
  can't put it in the title bar, which the desktop draws, and the owner
  wanted one place on both systems. At the top it is the first thing seen
  and one move away mid-game.
- **Revisit if:** a narrow window makes the switcher hard to find as an
  icon.

## 2026-10-17 — Ultraviolet on a darker neutral, roomier on small screens

- **Chosen:** an ultraviolet accent, `#6D4AFF` with white text, replacing
  the pale lavender button; a ground of `#08080A`; spacing one step roomier
  in a window the size of the Steam Deck's; Neap's own segmented controls,
  sliders and switches; the mark redrawn on the same idea.
- **Why:** the owner wanted a stronger, more modern and more custom look.
  The Deck is a touch screen, where 40px rows are tight for a finger.
- **Revisit if:** the ultraviolet reads as another brand's, or the Deck's
  larger size makes desktop-sized windows look sparse.

## 2026-10-17 — Copy principles

- **Chosen:** `docs/design/COPY.md`: name things by what they do for you,
  one name per thing, short enough to read at a glance, help text that
  earns its place, a way out of every state, warm and not cute, and words
  that translate.
- **Why:** the only rule was "plain and restrained". With Audio and
  Microphone merged, five noise names would sit on one page, and the
  equaliser had three scopes under one name.

## 2026-10-17 — A calmer, compact look, drawn from shared tokens

- **Options:** keep the violet-tinted look and its gradients; adopt a
  ready-made Avalonia theme (FluentAvalonia, Semi, SukiUI); restyle on
  Avalonia's own Fluent theme with Neap's own tokens, after MultiDrive.
- **Chosen:** Neap's own tokens on the Fluent theme, following
  `docs/design/PRINCIPLES.md`. Neutral grey grounds, so the violet accent and
  the game and chat pair are the only colour. Flat panels with depth from
  steps of fill, not gradients, shadows or outlines. Compact sizes: 13px body
  text, 40px rows. Inter, carried by the app. The dark theme's primary button
  stays pale lavender with dark text.
- **How it is built:** every colour and size is a token in
  `Themes/Tokens.axaml`; the toolkit's own keys point at them from one list in
  `FluentKeys.cs` for light, dark and high contrast; Neap's templated
  controls have control themes; pages share one `PageView` frame.
- **Why:** each theme repeated the toolkit's keys by hand and high contrast a
  third time, so they drifted, and sizes were typed in dozens of places. The
  gradients dated the look and made contrast hard to measure. MultiDrive
  showed the qualities wanted, calm, flat and roomy with one accent, and is
  taken as a reference for those, not copied: its colours, layouts and parts
  are not used.
- **Revisit if:** a 13px body proves too small for people to read at arm's
  length, or the neutral ground makes Neap hard to tell from other apps.

## 2026-10-17 — Profiles hold the game and chat mix on a headset with no chat wheel

- **Options:** keep the mix out of every profile; hold it in every profile;
  hold it only where the headset has no chat wheel.
- **Chosen:** a profile saves and restores the mix on a headset Neap knows
  has no chat wheel, such as the Atlas Air. On the Stealth Pro II it is left
  out, as before, and a profile saved there keeps whatever mix it had.
- **Why:** the mix was left out because the chat wheel owns it: a profile
  moving it would leave the wheel and the mix disagreeing. A headset with no
  chat wheel has nothing that owns it, and its tester expected a profile to
  keep it.
- **Revisit if** a headset with a chat wheel has testers asking for the same,
  or the mix moving on a switch surprises people mid-game.

## 2026-10-16 — The firmware rules are lifted

- **Options:** keep firmware off limits; lift the rules so Neap can build on
  the firmware and flash work done in SP2Tools.
- **Chosen:** lift them. This replaces the decision of 5 October that
  firmware is studied only from Swarm II's files. Reading the device's
  flash, the firmware update path and keeping firmware files in the
  repository are no longer ruled out.
- **Still in place:** every write goes through the registry, and a
  transmitter slot's base address is never written.
- **Revisit if:** a firmware change leaves a headset that cannot be
  recovered, or Turtle Beach objects to its files being published.

## 2026-10-08 — A profile's app can switch only while it is using the microphone

- **Options for a call app such as Teams, open all day:** follow the window
  in front; switch while the app is using a microphone; leave it to
  switching by hand.
- **Chosen:** each app assigned to a profile has a choice of when it
  switches: while it's open, as before and by default, or while it's using
  the microphone. A call's profile comes on as the call starts and the
  default comes back as it ends, whatever window is in front.
- **Why:** on a call, people tab to slides, notes and a browser. Following
  the window would change the sound mid-call, and would undo the choice of
  28 September for games. Using the microphone is what a call is, and the
  system already says which programs are recording, without administrator
  rights.
- **Any microphone counts,** not only the headset's, and only while the
  program is recording, which is when the system shows its own microphone
  mark. On Linux, a program recording what an output plays is not counted.
- **Revisit if** a call app lets go of the microphone during a call, when
  muted say, and the profile flickers, or a call app records from a program
  with a different name from the one assigned.

## 2026-10-08 — A new headset's test build has every function; its release has only what was confirmed

- **Options:** keep a new headset's settings read only until each is
  confirmed; unlock only what is easy to undo; or unlock every function in
  the build its tester tries.
- **Chosen:** the build a tester tries has every function Neap has for that
  headset, writes included, saving and deleting presets too, and the tester
  reports what works. The public release carries only the functions the
  tester confirmed; the rest are hidden or read only until they are. Each
  headset's functions, and whether its settings may be changed, are listed in
  one place in Core.
- **Why:** reading a setting does not prove writing it does the same thing
  (the Stealth Pro II accepts a mute it ignores, and ignores a write to the
  chat wheel), so nothing unconfirmed goes to everyone. But a tester who can
  only look cannot find what is wrong, and unlocking a function at a time
  costs a round trip with a volunteer for each. The checklist starts by
  asking the tester to note their own saved presets.
- **Revisit when** a test build harms a tester's headset or settings, or a
  headset has more than one tester.

## 2026-10-08 — The battery readings are shown as the hardware can be trusted

- **Options:** show whatever each device reports; smooth it in the app; or show
  a word where the number cannot be trusted.
- **Chosen:** the spare in the Charging Dock shows a bolt and "Charging" until
  it reports full; the headset's own reading shows "Settling…" while it falls
  more than two points in five minutes off power. No bar is drawn without a
  level, since an empty bar reads as empty. Every reading is written to the app
  log with the one before it.
- **Why:** measured with a real swap (FINDINGS.md): the dock's figure is stale
  after a swap and runs high and uneven while charging, and the headset's falls
  fast for some minutes after a battery goes in. Smoothing would hide real
  changes, and no other value the headset reports moves with it.
- **Revisit when** the log shows how long settling really takes over several
  swaps, or the headset's charging flag is seen on its USB-C cable.

## 2026-10-08 — The first public release of this work is 0.2.0, not 0.3.0

- **Chosen:** 0.2.0, with the Linux work and the earlier 0.2.0 features in one
  set of notes, opening with highlights and a how-to-update section.
- **Why:** GitHub only ever had 0.1.0; the earlier local 0.2.0 tag was never
  pushed, so no one had seen 0.2.0 and skipping to 0.3.0 would skip a number
  nobody had. That tag was deleted and recreated on the final commit.

## 2026-10-08 — Animation is brief, follows Windows' setting, and never loops

- **Chosen:** a page changing fades up over a fifth of a second and a drop-down
  or menu fades in over an eighth. Both are off when Windows' "Show animations"
  is off; elsewhere they are on. A charging battery is not animated.
- **Why:** the old toolkit gave these for free and the new one does not. An
  endless animation would need a way to stop it to meet WCAG 2.2.2, and the
  toolkit cannot read the reduce-motion setting on Linux.

## 2026-10-08 — An agent may run the real app against the real headset for the release checks

- **Chosen:** `tools/ui/run.ps1 hardware` drives the real app and reads the
  headset back with the probe, stopping the owner's own copy for the run and
  putting it, and the sign-in shortcut, back after. Only with the owner away,
  since windows come to the front.
- **Why:** it found a real fault pretend mode could not (choosing Off with the
  Mode button cycling stepped on to transparency). What needs hands or ears
  stays by hand: see BACKLOG.md.

## 2026-10-07 — One app for Windows and Linux, rebuilt on Avalonia

- **Options:** keep the WinUI app and write a second one for Linux; one Avalonia
  app for both; a web or Electron shell.
- **Chosen:** one Avalonia app, with everything that was only Windows moved
  behind a few named seams, and the WinUI app retired once the new one matched it.
- **Why:** two apps would mean every screen, fix and string made twice, and the
  Steam Deck, where it was first wanted, has no other way to draw a WinUI
  window. Avalonia draws the same screens on both with .NET already in use,
  with no browser in the download, and has an accessibility story on both
  systems. The cost is that the app draws itself rather than using Windows'
  own controls, so the look has to be matched by hand (done: the same colours,
  rounded fills, buttons and card titles, checked against the old app's
  screens).
- **Revisit if:** a feature needs Windows' own controls, or Avalonia's
  accessibility on Linux fails a screen reader check.

## 2026-10-07 — Linux never installs updates itself

- **Options:** replace the running files as Windows does; download and open a
  new package; say a newer version is out and open its page.
- **Chosen:** the last. The same daily check runs, and the same banner and
  Settings card appear, with What's new and Download in place of Update and
  restart.
- **Why:** replacing files under a running copy crashed it on the Deck, the
  program may belong to a package or an AppImage (a read-only image), and the
  updater runs as the person, so it could not write to most places Linux apps
  live. Telling people costs nothing and cannot break an install.
- **Revisit if:** Linux users ask for in-app updates; a delta update tool for
  AppImages (AppImageUpdate) is the likely route.
- **SUPERSEDED (31 October 2026, the owner asked for in-app updates on
  Linux):** the AppImage now updates itself; see the entry of that date.

## 2026-10-07 — Neap asks for headset access once, through the system's own prompt

- **Options:** ship a package that installs the rule; ask people to run a
  command; install it from the app when the person chooses.
- **Chosen:** the app notices when the system refuses it, says so in words on
  every page, and offers **Allow access**, which asks for the password once
  through the desktop's own prompt and writes one three-line udev rule
  (`uaccess` for Turtle Beach's vendor id only: no group to join, nothing
  world-writable, no access for someone not at the machine). Where there is
  no prompt, it hands over the command to run. The rule also ships as a file
  in `packaging/linux` for any package to install.
- **Why:** an AppImage cannot install system files, and a refusal that looks
  like "the headset is off" sends people to switch it off and on to no effect.
- **Revisit if:** a distribution package is made; it would install the rule
  and the prompt would never be needed.

## 2026-10-07 — A Windows zip and a Linux AppImage; no installer yet

- **Options:** the zip as before; a Windows installer; an AppImage, Flatpak or
  distribution packages for Linux.
- **Chosen:** keep the zip for Windows, and an AppImage for Linux.
- **Why:** the zip already works with the in-app updater, which swaps files
  in the app's own folder and so cannot update anything installed in Program
  Files (it never runs elevated, a security decision). An installer would add
  a tool, signing and a second path to test for a Start menu entry. An AppImage is one file that runs on any
  distribution, needs nothing installed, and suits the notify-only update.
  Flatpak and packages cost more per distribution and can come on request.
- **Revisit if:** people ask for an installer (per-user, so updates keep
  working) or a store listing.

## 2026-10-07 — Shortcuts on Linux are grabbed from X, and keep Windows' key codes

- **Chosen:** shortcuts are stored as Windows virtual-key codes on both
  systems, and Linux takes them from the X window system on a connection and
  thread of its own.
- **Why:** one settings file and one screen to record keys serve both;
  Wayland has no keys for the whole desktop without a portal, but a game on a
  Wayland desktop is an X program on the compatibility layer, which hears the
  press while it is in front. That is as far as it reaches, and the README says so.
- **Revisit if:** the global shortcuts portal becomes dependable on the
  desktops people use.

## 2026-10-07 — Outlined Fluent icons, drawn by the app

- **Chosen:** the app carries its own icons, Microsoft's outlined Fluent
  System Icons (MIT), plus one Material ear (Apache 2.0) for Superhuman
  Hearing, which Fluent does not have.
- **Why:** Windows has an icon font and Linux does not, so the icons have to
  travel with the app; the outlined set matches the old Windows app's own
  icons, where the first Linux build used filled Material icons that read as a
  different product.

## 2026-10-05 — Profiles get a page of their own, and Settings is reordered

- **Options:** leave Profiles as a section of Settings; give it a rail item;
  give it a rail item and a way to save a profile from its own page.
- **Chosen:** a **Profiles** rail item between Controls and Device, with
  **New profile** at the top of its page, then the default and every saved
  profile. Switching stays in the profile bar above every page.
- **Why:** with its list, default choice and per-profile app picker, the
  profiles section had become most of the Settings page, and someone looking
  after their profiles would not look under app housekeeping. A page that
  can list profiles but not add one would send people back to the bar, so
  New profile is on the page too, using the same dialog as the bar's menu.
  It is the same action in two places, not a second behaviour.
- **Settings, reordered:** Neap's own version and update status are first,
  with What's new and Update and restart beside them. The update banner and
  the notice by the clock both lead to Settings, so what they promise is
  the first thing there. Then the two switches, start with Windows and
  check for updates, then Problems and other headsets, then the licence
  line. "Updates" no longer sits under About below a paragraph of licence
  text.
- **Words cut:** the note under Running said closing the window keeps Neap
  running; the notice shown the first time the window is closed already says
  so, and the Start with Windows line says it starts in the notification
  area. "Diagnostics" became "Problems and other headsets", since the first
  word is the developers'. That Neap has only been tested with the Stealth
  Pro II moved from the licence paragraph to beside the invitation to help
  support another headset, which is where it matters.
- **The banner moved above the profile bar,** to the very top under the
  title bar, as the owner asked: it is about the app, not about the
  headset's setup, so it goes above the profile bar rather than between it
  and the page. The cost is that the profile selector drops one row while a
  banner shows, where before the page did.
- **The rail is seven long,** the most it should be. The next thing without a
  home needs a closer look at what is in the rail before it gets one.
- **Revisit if:** the Profiles page stays nearly empty for most people, which
  would say it should have stayed a section.

## 2026-10-05 — Neap updates itself from GitHub

- **Options:** keep updating by hand (download, quit, unzip over the
  folder); tell people a new version is out and leave the rest to them; a
  built-in updater.
- **Chosen:** a built-in updater. Neap asks GitHub for its latest release
  once a day, shows "Version X is available" in Settings under About with
  **What's new** and **Update and restart**, and says so once in a
  notification by the clock. Updating downloads the zip, checks it against
  the published SHA-256, puts it in place and restarts. Settings are kept.
- **The promise changes** from "nothing leaves the PC" to "nothing from
  the PC or the headset is sent". The check is a plain request to GitHub,
  which sees the PC's internet address and the app's name, as any website
  does; it says so rather than "nothing about you", which would not be
  true. **Check for updates automatically** turns it off for anyone who
  wants Neap offline, and **Check now** still works.
- **How the files are swapped:** each file being replaced is renamed aside
  and the new one moved in, all on one drive, so nothing is copied while
  the app is half replaced, and a failure undoes every move. Neap does this
  itself, so any newer release works as the new version, including ones
  that know nothing of updating.
- **The cost:** until the download is signed, updating this way is as safe
  as downloading by hand, no safer. A folder the person can't write to
  can't be updated; Settings says so and links to the download.
- **Revisit when** the download is signed: the updater should check the
  signature too.
- **What the updater needs from every release,** so the release workflow
  and any other platform's build keep it true: the tag is `vMAJOR.MINOR.PATCH`
  with nothing after it (a `-beta` tag is refused), the release is not a draft
  or a pre-release, and it carries `Neap-MAJOR.MINOR.PATCH-win-x64.zip` with
  its `.sha256` beside it, both under Neap's own repository. A release
  without them reads as "cannot tell", never as "nothing new". The updater
  asks for the latest release, so a release for another platform alone would
  hide the Windows one: each carries the Windows zip.
- **A banner across every page**, under the profile bar, tells people a
  newer version is out. A notice by the clock alone would be missed or
  switched off, and a card in Settings is somewhere people only go to look.
  The banner leads with **Update and restart** and has **What's new**, and
  is the only thing that shows there. It goes when there is nothing to say.
- **Putting it away is one Not now menu holding two choices, written out:**
  **Remind me tomorrow** hides it for a day, even across a restart, and
  **Skip this version** hides it until a newer version is out, so someone
  who doesn't want 0.3.0 is still told about 0.4.0. They share a menu
  because ignoring the banner is what people do most, and two buttons as
  large as Update and restart gave putting it away as much room as updating.
  There is no way to hide updates for good short of turning the check off;
  that would leave people on old bugs without knowing. Settings always shows
  what is available, so putting the banner away loses nothing. There is no
  close button, because it wouldn't say which of the two it meant.
- **The banner is one row at any width.** Below 720 pixels at the app's own
  scale, What's new and Not now fold into one more-options menu beside
  Update and restart: wrapping the buttons under the message took nearly
  half of a narrow window's height. While an update downloads or installs,
  Update and restart stays on screen, off, with a progress bar under the
  message, and What's new and Not now are not offered, since neither would
  change anything. A flat wash of the accent hue and a thin accent edge on
  its left mark it as news, not as part of the profile bar beneath it; the
  profile bar's own ground fades from its top, so a second fade below the
  banner would have shown as two bands. High contrast keeps the plain
  window colour.
- **Release notes are read in Neap,** in a dialog, not on a web page: the
  notes come with the version check, so reading them asks for nothing more,
  and the person is deciding whether to update inside the app. The install
  steps are left out, since they are for someone downloading by hand. A
  release whose notes can't be read opens its page instead.
- **After an update, the old version waits to see the new one open.** It
  hides, starts the new version and watches it for ten seconds. If the new
  one is still running, the old one closes as before; if it exits inside
  that time, or could not be launched, the old one says so with a
  Download button. Installed-but-won't-run is worse than not having
  updated, and the old version is the only thing left to say so. It does
  not put the old files back: the swap's own undo covers a failed swap,
  not a version that installs cleanly and then won't run, and a rollback
  would itself run from a broken install. A practice run with
  `--update-fails` makes the simulated update end in a program that exits
  at once, so the message can be seen without breaking anything.
- **A version found is remembered** until the next check, so restarting
  Neap doesn't make the banner disappear for up to a day.

## 2026-10-05 — Recordings are sent through GitHub, not uploaded by Neap

- **Options:** an upload from the app to a small service that emails the
  file on; a form on another website that needs no account; GitHub issues.
- **Chosen:** GitHub. After a recording, **Report on GitHub** opens a form
  with Neap's version, Windows' version and the hardware filled in, and
  opens the folder with the file selected, so the person only drags it
  across. Two forms, a problem report and a request to support a headset,
  are picked by whether Neap knows the hardware plugged in.
- **Why not an upload:** it means running a service, an address anyone can
  send junk to because the code is open, and changing the promise that
  nothing leaves the PC.
- **The cost:** a GitHub account, free, made once. Reports are public,
  which the blanking in the file allows for.
- **Revisit if:** people who would report a problem are put off by needing
  an account.
- **Before a request to support another headset is sent,** Neap shows which
  of its functions it found on the headset, which it didn't, and which
  changed while recording, and puts the same list in the form. It says
  "found", never "works": Neap only reads from a headset it doesn't know,
  and the same key can mean something else on another model. It covers
  headsets that speak the Stealth Pro II's command language; one that
  doesn't is told Neap can't talk to it yet.
- **The steps are in the app** behind a help button beside Start, as well
  as in the guide, because the people who most need them won't go looking
  for a web page. The two lists have to be kept the same.

## 2026-10-05 — Firmware is studied from Swarm II's files, never the headset

Replaced on 16 October by "The firmware rules are lifted". Its reading that
the images are signed and encrypted was wrong: they are compressed, carry a
plain checksum, and the headset accepts a changed one (FINDINGS.md, *How
Swarm II updates firmware*).

- **Options:** read the firmware off the headset's chip; study the copies
  Swarm II already installs; leave firmware alone entirely.
- **Chosen:** study Swarm II's copies, kept outside the repository.
- **Why not the headset:** the commands that read its flash sit beside the
  ones that erase and rewrite it, and one wrong byte could leave a headset
  only Swarm II can recover, if anything can. Swarm II's files are the same
  firmware at no risk.
- **Never committed:** the files are Turtle Beach's, and this repository is
  public.
- **What it can and can't do:** the images are signed and encrypted, so a
  changed image won't install. Studying them can explain behaviour and point
  at settings worth testing; it can't fix the firmware itself.

## 2026-10-05 — Players can record diagnostics for a bug report

- **Options for what players can run:** the developer's own tools and
  scripts, sent to them as needed; a fixed set of tests built into the app;
  both.
- **Chosen:** a fixed recording built into the app, under Settings →
  Diagnostics. It covers what debugging here relies on: which devices are
  plugged in and which one answered, every value the headset holds,
  everything it reports while the recording runs, and where Windows sends
  sound and how loud, second by second. New tests arrive with updates.
- **Why not scripts sent to players:** if the app runs whatever it is
  given, anyone pretending to be the developer can send something harmful,
  and some of the local tools need administrator rights the app never asks
  for.
- **It only listens.** Probing settings nobody has confirmed stays out of
  the app, because a wrong value could leave someone's headset in a bad
  state. Reading every setting the headset already answers for is in.
- **Saved, not sent.** The file goes to Downloads with identifying values
  blanked, and the player attaches it themselves. Sending from the app would
  break "nothing leaves the PC", and an address built into open-source code
  can't be hidden.
- **Supporting another headset** starts with Neap's own recording, made
  while pressing each button and turning each dial in a set order, so
  anyone can help with nothing to install (docs/MAPPING.md). The pauses
  and order stand in for notes, which non-technical testers wouldn't keep.
  Settings only Swarm II can change need a USB recording of it, which needs
  a driver, a restart and administrator rights, so that stays outside the
  app as an optional step for confident testers.
- **A section in Settings, not a seventh rail item:** reporting a problem is
  rare, and Settings is where the app's own housekeeping lives.
- **Still open:** a private way to receive USB recordings.

## 2026-09-28 — Save and Discard move into the profile menu

- **Chosen:** Save and Discard no longer sit as buttons in the bar itself.
  They now live in the profile menu, above the list of saved profiles,
  and only the "Edited" mark stays on the bar.
- **Why:** a UI review of Profiles found the pair naggy for someone just
  trying things — every setting changed while a profile is active showed
  both buttons, on every page, for a case the switch-time prompt (Save and
  switch / Switch without saving / Cancel) already covers. This was the
  exact case the 2026-09-27 "What happens after changing something
  post-profile" decision flagged to revisit if it turned out to be a
  nuisance.
- **Revisit if:** tucking them away costs more than it saves — for
  instance if saving a tuning change turns out to be frequent enough that
  the extra click to open the menu grates.

## 2026-09-28 — The profile menu's own layout, after a screenshot review

- **Save and Discard share their row evenly,** instead of sitting at their
  own width with empty space beside them, and Save is the filled button:
  the one people are more often choosing.
- **"Save current settings as a new profile" is "New profile", moved above
  the list of saved profiles instead of below it.** The full sentence read
  as an explanation rather than a label, next to buttons that are a word
  each. Below the list, its place depended on how many profiles happened
  to be saved, for a task that isn't rare.
- **Why:** the owner caught both from a screenshot of the shipped menu,
  after the move into the menu above made them visible in a way they
  weren't before.

## 2026-09-28 — Nothing of Neap's reaches into a running game

- **The Charging Dock's ring is left white while a game is full-screen.**
  Keeping it purple means setting the output format again whenever a
  microphone opens, which restarts the audio device the game is playing
  through; a game's voice chat can open the microphone several times a
  session. The ring catches up once nothing is full-screen. Windows' own
  "a full-screen app is running" signal decides, the one it uses to hold
  notifications back.
- **Neap names other programs with the least access Windows has:** asking
  where the program's file is, and reading its description from the file
  on disk, never from the running program's memory, which anti-cheat
  software watches for. The mix names each program once rather than every
  two seconds.
- **A page's polling and the microphone meter pause whenever Neap's window
  isn't in front,** not only when it is hidden to the notification area.
  A reading on a second monitor stops updating while another window is
  active; it catches up the moment Neap is clicked.
- **Why:** a game closed after an alt-tab. The log showed Neap wasn't the
  cause, but an audit found these were the places it touched a game at all.
- **Still to do, with the headset:** the connection to the headset asks for
  news about 200 times a second instead of waiting to be told.

## 2026-09-28 — Profiles switch as their apps start and close

- **Options for "when":** the app coming to the front and going behind;
  the app starting and closing.
- **Chosen:** starting and closing. Tabbing out of a game to Discord or a
  browser doesn't switch anything; only closing the game does. This replaces
  the earlier suggestion of following the window in front, made before
  assignment was split out and never confirmed.
- **A default profile, set in Settings,** comes back when the last app with
  a profile closes. With none set, the profile from before the first app
  started comes back.
- **Two apps at once:** the one that started last decides, and when it
  closes the other's profile comes back.
- **A profile chosen by hand while an app runs holds:** closing apps then
  changes nothing, and the next app to start switches as usual.
- **Unsaved changes make the switch wait,** with "Waiting to switch to …"
  in the bar, and it happens the moment they are saved or discarded. It
  never asks, since a game may be in front. The same wait covers the
  headset being off.
- **How:** the running programs are listed every two seconds, matched by
  process name. Windows' process events need administrator rights, which
  Neap doesn't ask for, and nothing is listed while no app is assigned.
- **Revisit if:** two seconds feels slow, or a game's launcher and the game
  are separate programs often enough that assigning the right one confuses.

## 2026-09-28 — Profiles, after a flow review

- **Switching away from unsaved changes now asks**, with Save and switch,
  Switch without saving, and Cancel. This reverses the earlier choice to
  match the equaliser, which discards without asking: a profile's changes
  can span several pages, so losing them costs more than losing one curve.
  It only asks when something was changed, so an ordinary switch is still
  two clicks.
- **Saving and switching are unavailable while the headset is off or its
  settings are still being read**, and the list says why. Before, saving
  refused only after a name was typed, and switching did nothing and said
  nothing.
- **Both equaliser banks are read in the background once the headset
  connects**, so a saved profile always holds its presets, "Edited" only
  means edited, and the first switch takes a second instead of about eight.
- **A program can be added by browsing for its file**, and every app already
  assigned to a profile stays in its list so it can be taken away, running
  or not. Assigning still doesn't switch anything automatically; the
  Settings note says so.
- **Why:** a UI review and a flow review of the first version; the flow
  review also missed the browse gap until the owner pointed it out.
- **Revisit if:** the warning turns out to be a nuisance when tuning.

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
