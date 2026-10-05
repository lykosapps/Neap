# Structure

Who it's for: see [USERS.md](USERS.md).

## The map

- **Home** — the headset's name, connection state and battery, a routing
  notice when Windows isn't sending sound to the headset, the game/chat mix
  balance, and quick settings. What opens first; serves the at-a-glance check
  and the mix, the two most frequent tasks.
- **Audio** — noise cancellation/transparency/off with Superhuman Hearing,
  master and voice-prompt volume, Windows' spatial sound format, and the
  game equaliser last, as on Microphone. Serves noise control, levels,
  Windows-level audio format and EQ.
- **Microphone** — mic on/off with a live meter, sensitivity, monitoring, AI
  noise reduction, the noise gate, and the microphone equaliser. Serves
  microphone settings.
- **Controls** — what the Mode button and lower dial are assigned to, a
  reference for the fixed controls (volume wheel, chat wheel, boom arm,
  cross-play switch, Bluetooth button), and the chat-mix keyboard shortcuts.
  Serves button and dial mapping.
- **Device** — connected transmitter(s), audio format, ring lighting,
  auto shut-off, wake on motion, and firmware/serial/raw values behind an
  expander. Serves transmitter and device information.
- **Settings** (footer) — start with Windows, About, and Profiles' upkeep:
  naming, deleting, assigning apps to switch to a profile automatically, and
  setting the default profile. App housekeeping, plus the one piece of
  profile management too infrequent for the always-visible bar.
  Diagnostics sits there too: recording headset activity to send with a bug
  report, and a link to the guide for helping support another headset.
  Serves reporting a problem, which happens rarely and is about the app as
  much as the headset, so it gets a section rather than a rail item.

## How people move

A navigation rail, always on screen: Home, Audio, Microphone, Controls,
Device, with Settings set apart in the footer. Six places, one level deep.
Home is selected on launch. There's no back button because the rail is
always there to move sideways instead. One shortcut: Home's quick settings
can jump straight to Audio's equaliser. Closing the window leaves a tray icon running,
whose own menu only opens or quits the app — it doesn't expose any page.

A profile bar sits under the title bar, above every page, riding along with
whichever place is open rather than being a place of its own. It shows the
active profile and an edited mark, and is where switching to another saved
profile or saving the current setup as a new one happens — the profile
task's frequent moves, reachable without leaving the page someone's on.
Naming, deleting, assigning apps and setting the default stay in Settings,
so the bar never grows past what fits under a title bar. Once a profile has
apps assigned, its own switch also happens automatically as those apps
start and close, with no screen to visit at all.

## Where new things go

- A new headset setting joins whichever of Audio, Microphone, Controls or
  Device already matches its function.
- A new app-level preference (not about the headset) joins Settings.
- A new everyday move for profiles (switching, saving) joins the profile
  bar; a new occasional one (managing what's saved) joins Settings.
- A seventh rail item only if something has no honest home in the five
  above — which hasn't happened yet.

## Why

The groupings mirror Swarm II's own, so nothing asks the owner to learn a
new mental model for the same headset. Six places stays flat and within
Windows' own guidance for a side rail. Settings sits apart in the footer
because it's about the app, not the headset.

## Open questions

There's no usage data or support tickets — the owner is the only user, so
every grouping here rests on their own use and on Swarm II's existing
conventions *(assumed, per USERS.md)*. Worth a fresh look if the audience
ever grows past one person.
