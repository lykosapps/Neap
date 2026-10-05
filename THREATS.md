# Threats

A one-page record of what could go wrong, and what is done about it. Revisited at every major release.

## The picture

**In:** HID reports from the headset and its transmitters, over USB; Windows' own audio APIs (which programs are playing, their volumes, a device's format); the app's own files under the person's profile (settings, log, volume-restore journal); command-line flags at launch; and, only when started with `--pretend`, commands from a local named pipe; and GitHub's answer about the latest release, with that release's zip when someone chooses to update.

**Kept:** locally, under the person's own profile. Nothing about the person; see the project profile in `CLAUDE.md`.

**Out:** writes to the headset over HID, every one checked against `Settings/Registry.cs`; writes to Windows' own volume, mute and format controls; a shortcut in the Startup folder, only if that is turned on. Over a network, only the update check: a request for GitHub's latest release, once a day unless turned off, carrying nothing but the app's name, which GitHub sees with the PC's internet address as with any website visit, and the download of that release when asked.

**Where trust changes:**

1. **The person at the keyboard.** Same trust level as the app: one person, one machine, no accounts.
2. **Other programs on the machine.** Read through Windows' Audio Session API to run the game/chat mix; can connect to a local pipe only when the app was started with `--pretend`.
3. **The headset and its transmitters.** An external USB device. Neap trusts it to answer honestly, but not to ask for anything: every write is checked, and a garbled reply is treated as a failure to parse, never guessed at.
4. **Distribution.** A zip on GitHub's release page, not signed yet, fetched either by hand or by the app's own updater (`UpdateService`), which replaces the app's files and restarts it.

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
   Deferred; already on `BACKLOG.md` ("Sign the download"). The updater (threat 12) checks the SHA-256 published beside the zip, which catches a damaged download but not a release replaced by someone holding the account; once the download is signed, the updater should check the signature too.

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

12. **The updater installs something that isn't a genuine release.**
    Mitigated, 2026-10-05. It only talks to GitHub over HTTPS, and refuses a release whose links are anywhere but Neap's own repository on GitHub (`Release.Parse`, with tests, including links that climb out of it with `..` and other owners' repositories). It offers only a version newer than the one running, never a draft or a pre-release. Before anything is moved it checks the zip against the published SHA-256 and the Neap.exe inside against the release's version, and unzipping refuses any entry that would land outside its folder. It runs as the person, never elevated, so it can't update a copy in a folder they can't write to; it says so and links to the download instead. A swap that fails part-way is undone (`FolderSwap`, with tests). Residual: the hash comes from the same release as the zip, so someone who takes over the GitHub account can publish both, the same exposure a manual download has today. Signing (threat 6) closes it. The release's notes are shown in the app as text: only bold and links are read, a link is kept only if it is to a web page over HTTPS, and nothing in them is run. The release kept between launches is read back through the same checks as one from GitHub, so a changed settings file can't point the updater anywhere else either (13).

13. **A release names files from a repository that isn't Neap's.**
    Fixed, 2026-10-05. Found in the first review: links only had to be on github.com. Every link must now start with Neap's repository, whether it comes from GitHub or from the settings file, and the check is tested against other owners, other repositories, a lookalike name and `..` in the path.

14. **The privacy wording says more than is true.**
    Fixed, 2026-10-05. A check is a request to GitHub, which sees the PC's internet address and the app's name, as any website visit does. The changelog, README, decision record and project profile said "nothing about you"; they now say what is sent: the request itself, and nothing from the PC or the headset.

15. **An update is cut off part-way.**
    Partly mitigated, accepted, 2026-10-05. A failed move is undone. Power loss or a killed process during the few seconds of the swap can leave a mix of two versions that may not start; the replaced files stay in the hidden update folder until the next start that works. Recovery is a download by hand. Not worth code that would itself have to run from a broken install. Related and deferred (BACKLOG.md): if the new version won't start after a good swap, the app has already closed with only a line in the log to say so; and a download has no size limit or overall time limit.

## What wasn't examined

- The Windows App SDK, WinUI, and the HID/WASAPI stacks themselves: trusted as platform code, not independently audited here.
- Physical access to the machine, or a headset whose firmware was already tampered with before Neap ever saw it.
- The GitHub account and repository's own settings (two-factor, token scopes): outside the code, a hosting-account matter.
- No automated scanner (OSV-Scanner, CodeQL) was run for this pass; it was a manual reading of the code at each boundary above.
- The updater was read in full on 2026-10-05, and unzipping was tried against a zip with entries named to escape its folder (both slash styles, a drive letter): all refused or kept inside. Redirects were checked against GitHub's own: downloads go on to githubusercontent.com over HTTPS, and .NET refuses a step down to HTTP. What wasn't examined: the update against a hostile server, and what Windows' own checks do to a replaced Neap.exe.
