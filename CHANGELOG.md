# Changelog

What Neap does and how that changes, in plain words, newest first. It is
the source for release notes and for the project page. [FINDINGS.md](FINDINGS.md)
holds the evidence behind each claim, and the commit history holds every
change in detail.

Each change a person could notice gets a line under **Unreleased**, in the
section it belongs to. At a release, Unreleased takes the version number
and date.

## Unreleased

### Added

- The mini panel has a Superhuman Hearing tile: a press turns it on or off,
  and the arrow in its corner picks Legacy, Footsteps or Gunshots. It is
  left out for a headset without it.
- In Settings, you choose which controls the mini panel shows: the
  microphone, noise cancellation, spatial sound, Superhuman Hearing and the
  equaliser preset. The panel shows what it did before, without Superhuman
  Hearing, until you choose. The mix is always there.

### Changed

- A recording for a bug report names a sound device only when it is the
  headset's. Any other is written as "another device", since its name,
  such as your earbuds', can carry yours.
- Controls in a row line up: list boxes sit centred beside buttons, with
  their words on the same line; buttons that are only an icon are as high
  as the rest; and an icon beside a button's words is centred on them.
- The equaliser preset on Home and in the mini panel is a standard list
  box, as the equaliser's own lists are: what is in use is chosen, with an
  Edited tag once it has been changed, and a preset you made can be deleted
  from the list, with Delete and Cancel in its place. Every list box's rows
  look like the Equaliser page's preset list, the open list has an edge so it
  stands out from the page, and the box shows when its list is open. The
  profile list in the side menu and the chat apps on Home have the same rows;
  the chat apps are a list you choose several from, each marked when chosen.
  An app keeps its name in that list, with a Playing tag while it makes
  sound, and a chosen app that is not open shows the name it had rather
  than its program's, such as ms-teams.
- On the Device page, About the headset is at the end, after the
  transmitters, as it is looked up only now and then.
- Loudness boost is now called Pre-gain, and says what it is: the level the
  headset applies before its equaliser. It goes below the headset's normal
  level as well as above it, down to 12 dB lower, in steps of 1 dB, so
  there is room for an equaliser boost without the headset squashing it.
- The mix dial on the Home page grows a little in a wider card, from its
  old size to a little larger, and the words on it keep their size.
- The parametric equaliser, which fitted up to four adjustments to the ten
  bands, is removed from every headset, with its test tone. A true parametric
  equaliser for the Stealth Pro II is being built and is not in this release.
- The notices about where the headset's sound goes, and the messages that
  ask for access to the headset on Linux, are shorter, and the long heading
  on the Device page is too.

### Fixed

- A saved parametric shape Neap cannot read is no longer sent to the
  headset as a flat one.
- The record Neap keeps of other apps' volumes, so it can put them back
  after a crash, is no longer lost if the PC loses power while it is being
  saved.
- Your settings and profiles are no longer lost if the PC loses power while
  they are being saved, or if another program, such as a virus scanner, has
  the settings file open as Neap starts. Neap tries again, and if the file
  stays out of reach it leaves it alone until the next start rather than
  saving its defaults over it.
- Settings Neap can't read no longer turn the update check back on: Neap
  starts from its defaults with the check off, in case you had turned it
  off.
- Two copies of the headset's game equaliser record kept in the same
  second, or after the clock is put back, are both kept; the later one
  takes a number after its name rather than replacing the first.
- A name kept for an app on a profile is found after a restart whatever
  the case of the app's program name.
- On Linux, Allow access on a desktop that has no password prompt running
  says so, with the command to run yourself, rather than doing nothing.
- On Linux, Allow access always runs the system's own shell as the
  administrator, never a program of the same name in one of your folders.
- Trying an update again after one that failed part-way no longer deletes
  the files it set aside, which can be the only copy of the version
  running. The update says it could not finish instead.
- On the Equaliser page, a preset whose reply from the headset is lost is
  asked for again. If it still can't be read, the page says so and doesn't
  offer to save a new preset, rather than counting its slot as free when the
  headset may be full. A preset whose bands can't be read is no longer
  listed as flat.
- With Parametric on, when the headset's stored equaliser comes back in a
  form Neap doesn't understand, Neap doesn't send its shape onto it as the
  headset connects, and says so in its log.
- On Linux, a transmitter that answers Neap's question about the headset
  in a way Neap cannot read is left as it is rather than restarted, which
  switched the headset off. The app log says what it answered.
- A transmitter whose details the headset sends in a form Neap cannot read
  stays on the Device page as it was, rather than dropping off as if its
  slot were empty. The app log and a recording say the slot did not parse.
- A change to a transmitter's lights is sent only once Neap knows which
  transmitter is in use, so after a reconnect it no longer changes the
  lights of the one used before. Until then the app log says it was not
  sent.
- When the headset answers with a reply Neap cannot read, the app log and a
  recording say so and keep the reply, with the headset's serial number
  blanked, rather than saying the headset gave no answer.
- A setting Neap cannot send, such as one out of range in a profile saved
  by hand, no longer shows on screen as if it had changed. It stays at
  what the headset holds, and the app log says why.
- A keyboard shortcut for the mix that another program holds says so beside
  the key even when the PC is slow as Neap starts. On Linux, a key X has
  not answered for within two seconds shows as refused, and the log says
  why.
- What is plugged in, on Home and the Device page, no longer goes back to
  an older list when the PC is slow to list its USB devices.
- With Parametric on, a headset that goes while its curve is being sent
  gets the whole curve again when it comes back. It could come back without it.
- The equaliser curve is sent whole when the headset connects, even while
  its presets are still being read. Rarely, only part of it was sent.
- Neap stays open if switching profile on its own fails part way, as when
  the transmitter is pulled out mid-switch, and says why in its log.
- An app turned down by the mix is no longer left turned down for good
  when the mix moves in the first moments after Neap starts, while it is
  still putting back the volumes a previous run left.
- When the Equaliser page couldn't read the headset just after it
  reconnected, it tries again as the headset's state next changes, rather
  than staying on "Couldn't read the equaliser" until you leave the page.
- A quick drag of a volume or microphone slider leaves the system at the
  level the slider ends on. It could settle on one just before, and the
  slider then moved back to it.
- After the headset is unplugged and plugged back in, a change of spatial
  sound made in the system's Sound settings or in Dolby Access shows in
  Neap straight away again.
- A message that comes after a wait, such as a save that failed, no longer
  closes the app when the window has gone to the notification area or the
  mini panel has closed in the meantime. What it would have said goes to
  the app log. The mini panel stays open while it shows a message, such as
  the one about activating Dolby Atmos.
- Switching profiles, or discarding a profile's changes, while the headset
  stops answering says it couldn't switch, rather than closing the app.
- After an update, the old version closes cleanly rather than leaving a
  crash in the app log on its way out.
- On Linux, with high contrast on, controls that cannot be used are drawn
  in grey. They were bright green, the colour that usually means on.
- A screen reader names the side menu's two lists, Pages and Settings,
  rather than reading each out as a list with no name.
- Compare on the Equaliser page works with voice control and a screen
  reader, which press a button rather than hold it: a press turns it on,
  and another turns it off. Holding it works as before, and a screen reader
  reads its instruction.
- The keyboard keeps its place in the profiles list in Settings and in the
  equaliser's preset lists after Cancel or Delete, a rename, or a profile
  switching as an app opens. Before, the next Tab started again from the
  top of the window.
- A screen reader says a notice as it appears, such as the one asking for
  Ctrl, Alt or Shift with a mix shortcut, and says a warning at once.
- When you name a profile or a preset, a screen reader says why Save is
  off, such as a name already taken, as you type.
- A screen reader says "Press the keys" when a mix shortcut is waiting for
  its new keys, rather than the keys it had.
- A screen reader reads the mix dial and the parametric equaliser's dials
  as sliders, and says their value as they turn. Before, they had no role
  and no value.
- Saving an equaliser preset says while you type when a name is too long
  for the headset, such as one with quotation marks or accented letters
  near the limit. It let you press Save and then said it could not.
- The mini panel shows the battery while the headset is switched off and
  charging on its cable, as Home does. It hid it there.
- On a Linux desktop running Wayland, the keyboard shortcuts say they work
  only while an X11 program is in front, as most games are. Before, they
  looked as if they worked everywhere. Where Linux has no shortcuts at all,
  the log says why.
- On Linux, the desktop notification that a newer version is out is sent
  in the form the notification service takes, and the log says when the
  desktop refuses it. Before, it could be refused without a word.
- On Linux, the icon in the notification area is dark when the desktop's
  theme is light, so it shows on a light panel. It was always light. On
  GNOME, whose top bar is dark in either theme, it stays light.
- A screen reader names the Details row on the Device page, which opens
  the firmware and serial number, rather than reading out a code name.
- The mix dial, before any chat app is chosen, is drawn in the colours for
  what is off rather than faded, so in high contrast it takes the
  system's colour for it.
- Superhuman Hearing's sound choices, while it is off, are drawn in the
  colours for what is off rather than faded, so in high contrast they
  take the system's colour for it.
- The focus mark on the menu is drawn in the text colour, as every
  other focus mark is. In the light theme it was a pale purple that was
  hard to see.
- A screen reader names the box for a preset's name when saving an
  equaliser preset. It was read out as an unnamed text box.
- The arrow after a link to another page, such as Keyboard shortcuts on
  Home, points right, to where it goes. It pointed up.
- Every switch looks the same. The switches in Settings, on the Device
  page's Keep high-resolution audio and for the mix's keyboard shortcuts were drawn another
  way, with an outlined track when off, and were smaller to touch.
- Saving or deleting an equaliser preset no longer closes Neap when the
  headset goes just after it.
- Deleting the equaliser preset in use shows Signature Sound as the
  preset in use, as the headset moves to it.
- Plugging in the headset's USB-C cable no longer leaves its microphone
  muted from an earlier time while the headset is unmuted: the cable's
  microphone takes the headset's own mute.
- In Parametric, Undo, Redo and Compare come in the same order as with
  the ten bands, so switching between them no longer moves the buttons.
- List boxes in stacked settings rows are one width, so they line up, as
  on the Device page, where Auto shut-off's box was narrower than the
  audio format's.
- The chat apps list and the mix work when the headset is Windows' output
  for calls but another device is the default, as when the speakers play
  everything else. Before, the list was empty and the mix did nothing,
  without saying why.
- Updating works from a copy built by hand, which runs as Neap.Desktop.exe.
  It said it couldn't update, because it looked in the download for its own
  file name rather than the Neap.exe a release has.
- Settings Neap can't read are kept as app-settings.json.unreadable beside
  the settings file, and the log says so, rather than being replaced by the
  defaults with no copy.
- In high contrast, a slider's track is drawn in the text colour, not the
  colour Windows uses for what is switched off, so a slider that works no
  longer looks disabled.
- A recording no longer lists a headset output's level every second when
  two of its outputs have the same name; each is noted only when its own
  level changes.
- A recording lists a change to the loudness boost beside the other changes
  Neap sends, and Neap sends it only to a headset known to take it.
- The list of chat apps, and the apps on a profile, show a program Windows
  gives no name for as words, "Ms Teams" rather than "ms-teams".
- The battery no longer shows more than 100%. A headset switched back on
  was seen reporting 250%; a number that is not a charge is left out until
  the next real one.
- On Controls, the Mode button is described as "The button above the volume
  dial", where it said beside the power switch.
- With Dolby Atmos on, the note says Dolby Access's equaliser applies on top
  of the headset's, rather than telling you to turn it off.

## 0.3.0 — 2026-10-24

**A redesigned Neap for the Stealth Pro II.**

### Highlights

- **A redesign.** Home, Audio and a new Equaliser page are set out afresh,
  with a flatter, calmer look, a new icon and a typeface of its own that
  looks the same on Windows and Linux.
- **A mini panel by the clock.** The mix, the microphone, noise
  cancellation, spatial sound and the equaliser preset, mid-game, without
  opening the window.
- **See the mix.** On Windows, a live meter for each side shows chat
  speaking and the game's sound as you set it.
- **Louder than the headset's maximum.** A loudness boost raises the
  Stealth Pro II's own level, for when it is too quiet even at full volume.
- **An equaliser to try things on.** Drag the points or use the keyboard,
  hold Compare to hear the headset flat, and undo any change.
- **Profiles for calls.** A call app can switch to its profile only while
  it is using the microphone.
- **The microphone follows every mute:** the boom arm, the mute button, and
  the boom microphone going in or out.
- **Steadier on Linux.** Transmitters are kept from sleeping, and one that
  goes quiet is brought back.

### Added

- A mini panel for mid-game. A click on Neap's icon by the clock opens it:
  the headset's name and battery, the game and chat mix as a slim slider,
  tiles for the microphone, noise cancellation and spatial sound, and the
  equaliser preset. A press turns a tile off, or back on the way it was last
  on, and the arrow in its corner lists its choices. Spatial sound offers
  what the system has, and is left out where it has none. The panel closes
  on Escape or a click elsewhere, and "Open Neap", on it and on the icon's
  menu, opens the window. A switch in Settings turns the panel off; a click
  on the icon then opens the window.
- Live levels for the mix. On Windows, the game and chat card shows a meter
  for each side beside the dial, with the level the mix sets, so you can see
  chat speaking and the game's sound as you set the mix. The meters stop
  while the window is behind, and are left out when animation is off. When
  the chat wheel moves the dial, the dial glides to its place.
- A loudness boost on the Audio page raises the Stealth Pro II's own level,
  so everything through the dock plays louder, for when the headset is too
  quiet even at full volume. It starts at +0 dB, is saved on the headset, and
  only the Stealth Pro II shows it.
- The equaliser is easier to try things on. Points show they can be dragged:
  a resize cursor, a halo and a larger point under the pointer, and a bubble
  with the value while one is moved. The curve takes the keyboard, with Left
  and Right to choose a band and Up and Down to move it, and Shift for a
  bigger step. Compare plays the headset flat for as long as you hold it and
  puts your curve back when you let go. Undo and Redo, or Ctrl+Z and Ctrl+Y,
  step back through the edits made since the page was opened. The curve
  grows with the window and shows a dB scale, and the part of your curve the
  headset can't reach is shaded grey and named in the legend.
- An app given to a profile can switch to it only while it's using the
  microphone, rather than whenever it's open. A profile for Teams calls comes
  on as a call starts, and the default comes back when it ends.
- On its cable alone, where the headset sends nothing from its chat wheel,
  the mix says so, with what still moves it.
- With a headset whose settings Neap only reads, Home says so, rather than
  leaving its controls greyed out without a word.
- A recording lists each change Neap sends to the headset beside what the
  headset says, and says why when a change wasn't sent.
- What's new is always in Settings, beside Neap's version. With no update
  waiting, it shows what's new in the version you have, without going online.

### Changed

- A flatter, calmer look. Neutral grey grounds, with one deeper ground in
  the dark theme; cards, rows, tiles, buttons, fields and switches without
  outlines or gradients, each a gentle step of fill from what it sits on;
  smaller text, tighter rows and even spacing on every page, so more of each
  page fits on screen. Neap's ultraviolet accent marks only what is on,
  chosen or playing, so a tile that is on is a soft violet tint. High
  contrast keeps every outline, and the keyboard focus ring is as clear as
  before.
- The app's text is set in Figtree, and the name beside the mark in Outfit.
  Both are carried inside the app, so Windows and Linux show the same
  letters.
- The app icon is redrawn as a split half moon over one wave, and the icon by
  the clock is its own simpler drawing that follows a light or dark taskbar
  on Windows.
- The menu and the page each run the full height of the window, and the menu
  starts in the title bar's row, with the Neap mark and name at its top and
  the profile switcher under them. When the menu is icons only, just the mark
  and one profile icon show. The headset's status and the update bar sit at
  the top of the page.
- Home is redesigned. The headset's readings run across the top, with how it
  is connected, such as "Through the USB Transmitter". The game and chat mix
  is one shorter card, with a Keyboard shortcuts link; before any chat app is
  chosen, its dial shows faded, with the question about chat apps below it.
  Every other card works the same way: on or off is a switch at the top
  right, and the card is tinted while it is on; a choice among a few is a row
  of segments under the name, such as Off, Cancelling and Transparency for
  noise cancellation, or Off, Windows Sonic and Dolby Atmos for spatial
  sound. Superhuman Hearing gets a switch, with its sound (Legacy, Footsteps
  or Gunshots) under the name, and the equaliser card a preset choice.
- Audio holds what you hear and your microphone in two sections, with noise
  cancellation, Superhuman Hearing and spatial sound as choices you can see
  at once, a link from each section to its equaliser, and every slider
  ending at the same edge. The microphone settings live here, and Microphone
  has left the menu.
- The equalisers have a page of their own, Equaliser, with a Game and a
  Microphone tab, and Home's equaliser card opens it. Your presets sit beside
  a larger curve, with names on one line, and the test tone is folded away
  until it is wanted. Discard shows only once the curve has been edited, and
  a saved preset's delete button only on the row you point at, focus or have
  chosen. Saving or deleting a preset takes about half the time.
- On Controls, each job for the Mode button and the lower dial is a full-width
  row you can press anywhere, the chosen one tinted. The dial's first job is
  named Microphone monitoring volume.
- On Profiles, the profile in use has a soft tint and says "In use" under its
  name, following a switch from the menu or by an app starting as it
  happens, and New profile sits beside the page's title. A screen reader
  hears each profile's apps, and whether it is in use, on the button that
  changes its apps.
- Sliders are the system toolkit's own, in Neap's colours: a white thumb in
  the dark theme and a purple one in light. The value beside each slider
  shows it while dragged.
- The headset is called what its firmware calls it, the name the system and
  a phone show, such as "Stealth Pro II Xbox", rather than "My Headset".
  Where the firmware gives no name, it is called by its model, and a headset
  Neap doesn't recognise, or none at all, is simply "Headset".
- What Neap says is shorter and clearer throughout, with Noise cancellation,
  Strength, Noise reduction, Loudness boost and Microphone monitoring as
  names, and every failure says what to do next.
- When your system sends sound to a device the headset isn't listening on,
  Home's connection line says there's no sound, as the title bar does,
  rather than naming the transmitter.
- A microphone muted in Windows while the headset itself is live, as Neap's
  switch does with the boom microphone in, says "Muted on this PC", so
  nobody waits for the headset's tone or red ring.
- "Keep high-resolution audio" is gone from the Device page, for now. It
  restarted the headset's sound to turn the Charging Dock's ring purple again
  after an app used the microphone, and that only works while something is
  playing. Neap no longer restarts the sound. After a call the ring stays
  white until something plays, then turns purple by itself. Windows keeps
  sending the same format while the ring is white, and the ring is the only
  change seen.
- "Help support another headset" says it's for Turtle Beach headsets that
  work with Swarm II, as the GitHub form and the guide now do: Neap talks to a
  headset the way Swarm II does, so it can't support others.
- On a small screen, such as the Steam Deck's, text, rows, switches, sliders
  and the menu step up a size so they are easier to read and touch.
- Dialogs and the update banner share the app's spacing, a dialog's main
  button uses the accent colour, and long dialog headings wrap instead of
  being cut off.
- Start when you sign in says why when it can't be changed, as in a practice
  run.

### Fixed

- The Charging Dock's two light sliders no longer stop partway through a
  drag. The dock reports its lights back as they change, and the transmitter
  list was redrawn each time, taking the slider away from under the pointer.
- On Home, the dot beside how the headset is connected sits level with its
  words, and the headset's name and readings sit in the middle of their card.
- Profiles no longer ask you to turn the headset on when it is already on;
  they say Neap can't reach its settings.
- Switched off with only its cable plugged in, Home says the headset is
  off from the start, rather than first suggesting CrossPlay and the
  transmitters.
- On Linux, while the system keeps Neap from the headset, the title bar,
  Home, the mini panel and the notification-area icon say "No access",
  rather than "Not connected" with a hint that the headset is off.
- The connection status no longer shows the device's technical description
  when you point at it, and Home's connection line has no tooltip repeating
  what Home already says.
- The chat wheel no longer gets stuck at the middle. Turned quickly through
  the middle to its game or chat end, the mix stopped at the middle and the
  wheel then had no room left to turn it further; the mix now goes to that
  end with the wheel.
- On Linux, a transmitter that has stopped answering is restarted only once
  Neap's newer access rule is in. Under the older rule the system can put it
  straight back to sleep, so a restart before then was used up for nothing.
  The notice then says Neap's access needs updating, with an Update access
  button, rather than that Neap isn't allowed to use the headset.
- On Linux, Neap's access rule keeps Turtle Beach transmitters from being put
  to sleep to save power, which could leave one answering nothing until it
  was restarted. If an older rule is installed and a transmitter stops
  answering, Neap offers the new one with the same password prompt; a headset
  that is only switched off is no reason to ask.
- On Linux, a transmitter that answers but has no headset linked to it is no
  longer restarted. The restart switched the headset off as it came back.
- On Linux, when a transmitter stays silent after Neap has restarted it,
  Home asks you to unplug it and plug it back in. It used to say the headset
  was off or out of range, though switching it on could not help.
- Choosing which apps carry chat now opens in a dropdown over the page,
  instead of an in-card list that grew and shrank. The card keeps one height,
  so the game and chat dial and the settings below it no longer jump as you
  open or close it.
- The microphone follows the headset through every way it mutes: the boom
  arm while the boom microphone is in, the mute button while it is out, and
  the boom being plugged in or taken out while connected. A mute by the
  button mutes the PC too, and the microphone card says "Muted on the
  headset". Neap's own switch mutes the headset itself, with its tone, while
  the boom is out, and Windows while it is in. Before, Neap followed only
  the boom arm, so the PC stayed live; the switch did nothing with the boom
  in; and a mute left on the built-in microphones showed as muted after the
  boom went back in.
- Button text sits in the middle of its button; it sat a little high.
- A click anywhere along a slider moves it there. Before, only dragging the
  handle moved it.
- Links keep their size when pointed at, and show a soft fill instead of
  turning bold, which pushed the text around them.
- The line that shows the equaliser reading its presets, and a profile being
  saved, moves again. It stays still when animation is off in Windows.
- The microphone meter on the Audio page moves as soon as the page opens.
  It could stay still until you switched away and back or opened the page
  again.
- Sliders move in whole steps, so they no longer shake as they are dragged,
  and never show a number past the slider's end.
- If the headset muted the PC's microphone, by its arm or by being switched
  off, the microphone comes back on when the arm is seen down, even after
  Neap was closed or the headset was off in between. A mute you set
  yourself in Neap stays.
- When the headset gives no answer for a group of its settings, such as
  the transmitter light, the app log and recordings say so.
- A recording says "no answer" for a transmitter slot the headset didn't
  answer for, rather than calling it empty, and names each change Neap sends
  by the setting it changes.
- A screen reader now announces noise cancellation, the Mode button, the lower
  dial and Superhuman Hearing's sound as named groups of options, instead of
  leaving the group's name out.
- The volume and microphone sliders no longer jump back while they are moved.
  The headset reports each level a moment late, and a late report pulled the
  slider back, then forward again. They now leave the headset's reports alone
  for a moment after a change.
- A profile saved on one headset no longer shows as changed on another that
  lacks some of its settings, and saving over it keeps them.
- On Linux, a headset that seems to say nothing is also listened for where
  Linux keeps what the headset sends unasked. If it still says nothing, the
  log says what arrived there.
- On Linux, Neap waits for the headset's replies at the same pace as on
  Windows, rather than asking about four times as often.
- On Linux, a transmitter that stays silent is restarted once, as if it had
  been unplugged and plugged back in, with Neap listening as it comes back.
  Some transmitters answer only if something has listened since they were
  plugged in. Neap asks for permission once more where it was allowed
  before this.
- What the headset reports, under Device, can be selected and copied, and
  a Copy button puts all of it on the clipboard.
- Settings and recordings show a test build's full version, such as
  0.2.1.1, rather than the release it follows.
- When a transmitter opens but the headset gives no answer, the log and any
  recording say so and what was tried, rather than leaving "Quiet"
  unexplained, and the first ask after Neap starts waits longer, in case the
  answer is only slow.
- Neap no longer changes the settings of a headset it doesn't know, as it
  always said it wouldn't. It only reads them.
- A recording of another headset now says which functions the headset didn't
  answer about, rather than counting them as not found, and asks it again
  before giving up.
- A link in What's new sits in line with the words around it, rather than
  above them.
- A recording keeps going for well over an hour. It notes a sound level when
  it changes rather than every second, which filled it after about 37
  minutes.
- A recording made on Linux names Linux and its version, rather than calling
  it Windows, and says once rather than every second that it can't yet tell
  where sound is going.

## 0.2.1 — 2026-10-08

**A small fix.**

### Fixed

- The beep when the game and chat mix reaches the centre plays again on Windows.
  In 0.2.0 it went silent.
- The beep is no longer missed when the chat wheel is turned quickly. Turned
  through the centre at any speed, the mix now stops there for half a second
  and beeps; keep turning and it carries on.
- Turning the chat wheel on past the centre no longer sends the mix racing
  ahead of the wheel.
- On Windows, the beep is no longer lost after the headset has been quiet for
  a few seconds. It plays at once while anything else is playing, and a third
  of a second late after a quiet spell.
- What's new wraps its text, so no line is cut off at the edge, and leaves
  out the install steps, which are for someone downloading Neap by hand.

## 0.2.0 — 2026-10-08

**Neap now runs on Linux, and updates itself.**

### Highlights

- **Linux.** The same app, with the same pages, colours and words, on Windows
  and on Linux. Used with the real headset on a Steam Deck in desktop mode;
  other desktops have been run only against a pretend headset.
- **Help bring Neap to your headset.** Own another Turtle Beach headset? The
  Stealth 600 Gen 3, 700 Gen 3, 500 and Atlas Air share the Stealth Pro II's
  platform, and support for them can start from a ten-minute recording made in
  Neap. No technical knowledge needed.
  [Here's how](https://github.com/lykosapps/Neap/blob/main/docs/MAPPING.md).
- **Updates itself on Windows.** A banner tells you when a newer version is
  out, and one button installs it and keeps your settings.

### Updating

- **From 0.1.0:** quit Neap from its icon by the clock, then unzip this
  version over your Neap folder. Your settings and profiles are kept.
- **From 0.2.0 on, Windows:** choose **Update and restart** in the banner or in
  Settings.
- **From 0.2.0 on, Linux:** Neap opens the download page; download the new
  file in place of the old one.

### New on Linux

- **One file.** An AppImage: make it runnable, and run it. It works on any
  64-bit distribution with PulseAudio or PipeWire, and each release has a
  `.sha256` to check it with.
- **Allow the headset once.** If Linux keeps the headset from Neap, every page
  says so and **Allow access** asks for your password once.
- **Keyboard shortcuts for the mix** and **Start when you sign in**, the same
  as Windows. Shortcuts work in X programs, which is most games.
- **Not yet:** Gaming Mode on the Steam Deck (use desktop mode), spatial
  sound, and the audio format.

### Help support another headset

- **Record it in Neap.** In Settings, under Problems and other headsets,
  choose Start next to Record headset activity, then work through the steps
  the ? button lists.
- **See what Neap found,** before you send it: what it recognised on your
  headset, what it didn't, and what changed while you recorded.
- **Send it from Neap.** Report on GitHub opens a form with the details filled
  in and the recording ready to drag in. You'll need a free GitHub account.

### Updates

- **Neap updates itself** on Windows. A banner says a newer version is out,
  with **What's new** to read the notes in Neap, **Update and restart**, and
  **Not now** to put it off for a day or skip that version. On Linux it
  opens the download page.
- **If the new version doesn't open,** Neap says so and offers the download
  again, instead of leaving you with no Neap and no word why.
- **Only a version check.** Neap asks GitHub once a day for the latest
  release and sends nothing from your PC or your headset. Turn it off in
  Settings to keep Neap offline.

### Profiles and Settings

- **Profiles have their own page.** Save the headset's settings under a name,
  choose which apps bring a profile on, and pick the default. Switching is
  still in the bar at the top of every window.
- **Settings is tidier.** Neap's version and whether it is up to date come
  first, then Start when you sign in, then Problems and other headsets, which
  was called Diagnostics.

### Fixed

- **Choosing Off for noise control stays off** when the Mode button is set to
  cycle through all three modes.

## 0.1.0 — 2026-10-04, the first release

A Windows app for the Turtle Beach Stealth Pro II, in place of Swarm II.
No driver, no Turtle Beach software, no admin rights.

### Game and chat mix

- **Nothing to install.** Pick the apps that carry chat, such as Discord
  or Teams, and Neap turns them against everything else. No virtual audio
  cable, driver or reboot.
- **Works on the Xbox edition.** Swarm II's mix relies on the Waves driver
  and isn't officially supported there.
- **Three ways to move it:** the headset's chat wheel, the dial on Home, or
  keyboard shortcuts that work inside games.
- **More than one chat app at once.**

### Noise control

- **Transparency.** Choose noise cancellation, transparency or off.
  Transparency is noise cancellation at zero, which lets the room in.
- **The Mode button steps through all three** while Neap is running.

### Sound

- **Parametric equaliser** for game audio: up to four adjustments, fitted
  to the headset's ten bands, with a plot of what you'll actually hear.
- **Test tone.** Find a frequency that stands out, then cut or boost it on
  the spot.
- **Presets stored on the headset**, so they also appear in the phone app.
  Start one from flat, or duplicate one.
- **Spatial sound:** Windows Sonic, or Dolby Atmos where it's installed.
- **High-resolution audio, kept.** The Charging Dock drops out of high
  resolution whenever an app uses the microphone. Neap puts it back
  afterwards, waiting until a full-screen game has closed.

### Every day

- **Profiles** save your whole setup under a name, and can switch on by
  themselves when a game starts.
- **Clear status.** Connected, no sound, switched off or out of range, each
  with what still works and the one thing to do.
- **Catches the wrong device.** When Windows sends sound or the microphone
  somewhere the headset isn't listening, Neap names the right one.
- **The spare battery's charge** while the headset is on the Charging Dock.
- **Settings Swarm II's desktop app doesn't have:** wake on motion and voice
  prompt volume.
- **Every other setting** the headset has: microphone, noise gate,
  Superhuman Hearing, buttons and dial, lighting, power and audio format.
- **A microphone meter** that shows what other people hear.
- **Microphone mute follows the boom arm**, and mutes in Windows at once.
- **Runs from the notification area**, with the battery on the tray icon,
  and can start with Windows.
- **Works from the keyboard and with screen readers**, in light, dark and
  high contrast themes.

### Not included

- **Firmware updates and pairing.** Keep Swarm II for these, but don't run
  both at once.
- **Other Turtle Beach headsets**, for now. Tested on the Xbox edition; the
  PlayStation edition should work but hasn't been tried.
- **Calls in a web browser** can't join the mix, because the browser also
  carries game sound.
- **A parametric microphone equaliser.** The microphone keeps its ten bands.
- **Not possible on this headset:** changing what the lights do, remapping
  the Bluetooth button, or hiding Windows' volume pop-up.
