---
name: dolby-access-eq-per-profile
description: "Investigation into reading/writing Dolby Access's own profile equalizer from Neap, to enforce it off per profile and avoid double-EQ"
metadata:
  node_type: memory
  type: project
  originSessionId: 47d548ae-57ea-4163-b39f-b8260c5fd2d0
  modified: 2026-09-26T04:57:20.215Z
---

The owner wants Neap to offer Dolby Access's own profiles (Game/Movie/Music/Voice) and force that profile's own equalizer ("Intelligent Equalizer") to Off when chosen from Neap, so it never doubles up with the headset's own EQ.

Dolby Access has no public API or plain settings file. Investigated so far (same-day session before the project folder moved, see [[claude-account-per-project]] for why that history nearly got lost):

- The two Dolby Access app pieces (frontend + OEM backend) talk over a private Windows app-service channel, seen live in the OEM app's own log as a `SyncProfile` call. That channel is packaged-app-to-packaged-app only.
- Neap is built unpackaged (`WindowsPackageType=None`), so it cannot be the other end of that channel — confirmed structurally, not worth re-testing.
- Got further by going straight to Dolby Access's own settings file (its offline registry hive under its LocalState\Settings folder — the same "read the vendor's own files" approach used on the headset, see [[read-the-vendors-own-files]]) rather than the app-service channel. Found a value inside it that changes when Dolby Access's equalizer setting changes.
- **Resolved 26 Sep 2026:** ran the test in the new session. Set Movie's equalizer to Warm, switched to Game: Game still read Detailed. Each profile keeps its own equalizer value — it is genuinely per-profile, not one shared slot.

**Why:** this means "force this profile's equalizer off" is a real, meaningful per-profile setting to build, not a compromise (just overwriting one shared toggle right before a format switch).
**How to apply:** don't re-derive the app-service dead end (see above) or re-run the Movie/Game test. Next open step is a write test: change the value in the same settings-file location Neap would write, then open Dolby Access itself and confirm on screen that the right profile shows Off — a value that reads back correctly is not proof it took effect (see [[reading-it-back-proves-nothing]]).

**Blocked 26 Sep 2026, second session:** tried the write test and couldn't get past Windows itself. The same offline-file technique that worked in the first session now fails even with admin approval: enabling the two Windows permissions that technique needs (Back up / Restore files and directories) is refused by Windows for this account, confirmed down to the raw error code, even though those permissions show as available (just switched off) for the account. That's a change since the first session, or something on this account/machine actively blocking it (security software commonly blocks exactly these two permissions, since ransomware abuses them the same way). Not something to keep guessing at from here.
**How to apply:** before trying again, the owner should either sign out and back in once (in case it is just a stale permission on this sign-in) or check whether any installed security software has a log/notification about blocking a "backup/restore privilege" request around 26 Sep 2026. Don't retry the elevated write again without one of those first — it will hit the same wall.
