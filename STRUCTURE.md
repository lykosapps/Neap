# Structure

Who it's for: see [USERS.md](USERS.md).

## The map

- **Home** — the headset's name, connection state and battery, a routing
  notice when Windows isn't sending sound to the headset, the game/chat mix
  balance, and quick settings. What opens first; serves the at-a-glance check
  and the mix, the two most frequent tasks.
- **Audio** — two sections. What you hear: noise cancellation, transparency
  or off with Superhuman Hearing, master and voice-prompt volume, the
  loudness boost, and Windows' spatial sound (Windows only). Your
  microphone: mic on/off with a live meter, sensitivity, monitoring, AI
  noise reduction and the noise gate. Both equalisers are on the Equaliser page.
  Serves noise control, levels and microphone settings: the occasional
  changes, on one page.
- **Equaliser** — the game and microphone equalisers, one tab each: presets
  beside a large curve, bands or, on the Stealth Pro II, a true parametric equaliser with its test tone.
  Serves tuning sound, which is done occasionally and at length, and needs
  more room than a card on another page.
- **Controls** — what the Mode button and lower dial are assigned to, a
  reference for the fixed controls (volume wheel, chat wheel, boom arm,
  cross-play switch, Bluetooth button), and the chat-mix keyboard shortcuts.
  Serves button and dial mapping.
- **Profiles** — the saved setups: save the headset's current settings as a
  new one, rename or delete them, choose which apps bring one on by
  themselves, and pick the default that comes back. Switching stays in the
  profile switcher at the top of the left menu, one move from wherever someone is; this is
  where they are looked after. Serves keeping a handful of setups tidy,
  which is occasional work that has outgrown a section of Settings.
- **Device** — connected transmitter(s), audio format, ring lighting,
  auto shut-off, wake on motion, and firmware/serial/raw values behind an
  expander. Serves transmitter and device information.
- **Settings** (footer) — Neap itself: its version and whether it is up to
  date, with What's new (the newer version's while one is out, otherwise the
  running version's) and Update and restart (Download, on Linux), whether to
  check for updates by itself, and whether it starts when you sign in.
  Then Problems and other
  headsets: recording headset activity to send with a bug report, and a link
  to the guide for helping support another headset. The licence line is at
  the foot. Neap and its update come first because the update banner and the
  notice by the clock both lead here. Serves keeping the app current and
  reporting a problem, which happens rarely and is about the app as much as
  the headset, so it gets a section rather than a place in the menu.

## How people move

A left menu, always on screen: the profile switcher at its top, then Home,
Audio, Equaliser, Controls, Profiles and Device, with Settings set apart in
the footer. Seven places, one level deep.
Home is selected on launch. There's no back button because the menu is
always there to move sideways instead. In a window narrower than 641 pixels
the menu folds into one button over the page's top corner, which opens it
over the page and closes it once a place is chosen. Between 641 and 1007
pixels the menu shows icons only, and the profile switcher becomes one
profile icon that opens the same menu. One shortcut: Home's equaliser tile opens the Equaliser page on its Game
tab. Closing the window leaves a
tray icon running. A left click on it opens the mini panel: a small card by
the notification area with the four things changed mid-game (the game and
chat mix, the microphone, noise cancellation and the equaliser preset), which
closes on Escape or when focus leaves it, and has a button that opens the
window. The icon's menu opens the window (Open Neap), opens the mini panel, or
quits; it exposes no page. On a Linux desktop with no notification area,
closing the window closes the app instead, since an icon nothing shows could
not be got back, and there is no mini panel to open. On Windows the app draws its own title bar, with the window's buttons
on its top row; on Linux the desktop's own title bar stays.

When a newer version of Neap is out, a banner sits at the very top, under
the title bar, above every page: the news,
What's new, Update and restart, and one Not now menu that puts it away for a
day or for good. In a narrow window What's new and Not now fold into one
more-options menu, so it stays one row. While an update runs it shows
progress and offers nothing else. It is gone when there is nothing to say.
The full account lives at the top of Settings, so putting the banner away
loses nothing.

The profile switcher sits at the top of the left menu, the same on Windows
and Linux, riding along with whichever place is open rather than being a
place of its own. It shows the active profile and an edited mark, and is
where switching to another saved profile or saving the current setup as a
new one happens — the profile task's frequent moves, reachable without
leaving the page someone's on. Naming, deleting, assigning apps and setting
the default live on the Profiles page, so the switcher stays one line. Once a profile has
apps assigned, its own switch also happens automatically as those apps
start and close, or start and stop using the microphone, with no screen to
visit at all.

## Where new things go

- A new headset setting joins whichever of Audio, Controls or Device
  already matches its function; a new tuning tool joins Equaliser.
- A new app-level preference (not about the headset) joins Settings.
- A new everyday move for profiles (switching, saving) joins the profile
  switcher; a new occasional one (managing what's saved) joins the Profiles page.
- An eighth place in the menu only if something has no honest home in the six
  above.

## Why

The groupings start from Swarm II's own, so the owner's mental model still
works. Two places differ: the equalisers have a page of their own because
they outgrew a card, and the microphone joins Audio because, without its
equaliser, it is a handful of rows that sit naturally beside what you hear. Seven places stays flat and within
Windows' own guidance for a side menu. Settings sits apart in the footer
because it's about the app, not the headset.

## Open questions

There's no usage data or support tickets — the owner is the only user, so
every grouping here rests on their own use and on Swarm II's existing
conventions *(assumed, per USERS.md)*. Worth a fresh look if the audience
ever grows past one person.
