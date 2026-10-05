# Threats

A one-page record of what could go wrong, and what is done about it. Revisited at every major release.

## The picture

**In:** HID reports from the headset and its transmitters, over USB; Windows' own audio APIs (which programs are playing, their volumes, a device's format); the app's own files under the person's profile (settings, log, volume-restore journal); command-line flags at launch; and, only when started with `--pretend`, commands from a local named pipe.

**Kept:** locally, under the person's own profile. Nothing about the person; see the project profile in `CLAUDE.md`.

**Out:** writes to the headset over HID, every one checked against `Settings/Registry.cs`; writes to Windows' own volume, mute and format controls; a shortcut in the Startup folder, only if that is turned on. Nothing over a network — there is no networking code in the app at all.

**Where trust changes:**

1. **The person at the keyboard.** Same trust level as the app: one person, one machine, no accounts.
2. **Other programs on the machine.** Read through Windows' Audio Session API to run the game/chat mix; can connect to a local pipe only when the app was started with `--pretend`.
3. **The headset and its transmitters.** An external USB device. Neap trusts it to answer honestly, but not to ask for anything: every write is checked, and a garbled reply is treated as a failure to parse, never guessed at.
4. **Distribution.** A signed-in-future zip on GitHub's release page. No auto-update yet.

## Threats and their status

1. **A device that shares the headset's product id, or one that is simply malfunctioning, sends malformed or hostile HID data.**
   Fixed. `HeadsetClient` caps how much unparsed data it will hold onto (8 KB), and every array read from a device reply is bounds-checked and returns "not known" rather than throwing (`Transmitters.At`). A reply that does not parse is kept as evidence, not silently dropped. Nothing here can grow without bound or crash the reader.

2. **A write reaches the firmware path, or a transmitter slot's base address.**
   Fixed. `Registry.WireValue` is the one path to the headset and refuses both outright, whatever asked for it. Covered by direct tests as of today, not only indirectly through the pretend headset's own.

3. **Another local program drives the app, or a pretend-mode script, over the named pipe `--pretend` opens.**
   Fixed. The pipe is opened `CurrentUserOnly`, so only the same Windows account can connect, and it does not exist unless the app was started with `--pretend`. Nobody running the ordinary app has this surface at all.

4. **Neap exits abnormally and leaves another application's volume turned down.**
   Fixed. Every mix change is journalled before it is made and replayed on the next launch. As of today, a journal that cannot be read or written leaves a line in the app's log instead of failing silently, so a stuck volume is at least explainable.

5. **A file with the same name as a system library, planted next to `Neap.exe` (a Downloads folder, say), gets loaded instead of the real one.**
   Fixed today. Every native call now loads only from the system folder, and the build refuses a new call that does not say so.

6. **The downloaded zip is not signed**, so Windows warns about it and a tampered copy on a mirror cannot be told from the real one.
   Deferred; already on `BACKLOG.md` ("Sign the download"). There is no auto-update channel yet either, and one will need the same signature checking when it exists.

7. **A headset serial number, a device address, or the owner's identity ends up in the repository, a capture file, or a screenshot.**
   Accepted, mitigated by process rather than by code: `FINDINGS.md` and `CONTRIBUTING.md` require this to be stripped before anything is shared, and the whole repository and its commit history were checked today with nothing found. This is ongoing discipline, not a one-time fix — worth a fresh look before anything captured from the hardware is committed.

8. **Another program or account on the machine reads Neap's own settings, log or journal.**
   Accepted. None of them holds a password, a token, or anything about the person; Windows' ordinary per-user file permissions are enough for files this unremarkable.

9. **A vulnerability ships inside a NuGet package or a GitHub Action.**
   Accepted and monitored. Dependabot watches both, weekly; every version is pinned exactly rather than left to float, and Actions are pinned to a commit. No separate scanner (OSV-Scanner and the like) is wired in yet; low priority, since Dependabot already covers most of the same ground.

10. **The app runs with more rights than the task needs.**
    Already true, confirmed today. The manifest asks for no elevation (Windows defaults it to run as the person, not an administrator), and only the app's real connection to the headset ever asks to write; the probe defaults to read-only.

11. **A recording made for a bug report identifies the person or their hardware once it is posted.**
    Mitigated. The file blanks the headset's serial number, anything shaped like a radio address, the Windows account and PC names and the profile folder, and leaves out the time zone (`Redaction`, with tests). It lists the programs playing sound, which is the point of it, and keeps the headset's own name, which a person may have changed; the owner chose to keep it (2026-10-05). It is plain text the person can read before sending. Neap sends nothing itself. A USB capture for mapping another headset is not blanked; the guide says not to post one publicly.

## What wasn't examined

- The Windows App SDK, WinUI, and the HID/WASAPI stacks themselves: trusted as platform code, not independently audited here.
- Physical access to the machine, or a headset whose firmware was already tampered with before Neap ever saw it.
- The GitHub account and repository's own settings (two-factor, token scopes): outside the code, a hosting-account matter.
- No automated scanner (OSV-Scanner, CodeQL) was run for this pass; it was a manual reading of the code at each boundary above.
