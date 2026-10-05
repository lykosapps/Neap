# Users

## Who uses it

One person: the owner, on one Windows machine, with one Stealth Pro II
(Xbox edition). *(known — project profile, README status)*

Linux users with a Stealth Pro II, once the Linux version exists. The owner
tests it on a Steam Deck in desktop mode. *(known — the owner's request for
Linux support)* Other Linux users are on ordinary desktops and laptops.
*(assumed)*

- Linux users can set volume per application with tools they already have,
  so the first Linux version covers headset settings and leaves out the
  game/chat mix. *(assumed)*
- On a Steam Deck the screen is small (1280×800) and touch is the usual way
  in, so every page fits that screen and works by finger. *(known — Steam
  Deck hardware)*
- On a Steam Deck the transmitter goes through a USB-C hub, which also
  carries the charger. *(known — the Deck has one USB-C port)*

## What they come to do

- Check the headset is connected and see its battery, at a glance. Several
  times a day. *(known — README: "working and in daily use")*
- Move the game/chat mix balance. Often, mid-session. *(known — README)*
- Adjust the equaliser or noise cancellation. Occasionally. *(known — README)*
- Manage microphone settings. Occasionally. *(known — README)*
- Remap the Mode button or dial, or check what a control does. Rarely, after
  setup. *(known — README)*
- Switch the headset between saved setups, such as a game one and a call one,
  in one move; save a new one after tuning; adjust or tidy them rarely.
  *(known — the owner's request for profiles; how often is assumed)*
- Check transmitter or device details (firmware, serial, format). Rarely,
  when troubleshooting. *(known — README, ARCHITECTURE)*
- Report a problem with a recording of what the headset and Windows did,
  for the developer to diagnose. Rarely, once others use the release.
  *(known — the owner's request for diagnostics; how often is assumed)*

## Where and how

Desktop, at the PC, often while gaming — wants a quick glance or a quick
change, not a deep menu dive. *(assumed)*

## The domain

Third-party control for a gaming headset, replacing the vendor's own app
(Swarm II). Conventions borrow Swarm II's own groupings (audio, microphone,
controls, device) since that's the mental model already in place.
*(known — README, ARCHITECTURE)*

## What they already know

Swarm II's layout and terms; ordinary Windows settings patterns. *(assumed)*
Linux users know their own desktop's settings patterns, and most have never
used Swarm II, which only runs on Windows. *(assumed)*
