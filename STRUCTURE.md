# Structure

Who it's for: see [USERS.md](USERS.md).

## The map

- **Home** — the headset's name, connection state and battery, a routing
  notice when Windows isn't sending sound to the headset, the game/chat mix
  balance, and quick settings. What opens first; serves the at-a-glance check
  and the mix, the two most frequent tasks.
- **Audio** — the game equaliser, noise cancellation/transparency/off with
  Superhuman Hearing, and master and voice-prompt volume. Serves EQ and noise
  control.
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
- **Settings** (footer) — start with Windows, and About. App housekeeping,
  not a headset function.

## How people move

A navigation rail, always on screen: Home, Audio, Microphone, Controls,
Device, with Settings set apart in the footer. Six places, one level deep.
Home is selected on launch. There's no back button because the rail is
always there to move sideways instead. One shortcut: Home's quick settings
can jump straight to Audio. Closing the window leaves a tray icon running,
whose own menu only opens or quits the app — it doesn't expose any page.

## Where new things go

- A new headset setting joins whichever of Audio, Microphone, Controls or
  Device already matches its function.
- A new app-level preference (not about the headset) joins Settings.
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
