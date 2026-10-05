# Decisions

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
- **A banner across every page**, under the profile bar, tells people a
  newer version is out. A notice by the clock alone would be missed or
  switched off, and a card in Settings is somewhere people only go to look.
  The banner leads with **Update and restart** and has **What's new**, and
  is the only thing that shows there. It goes when there is nothing to say.
- **Putting it away is two choices, written out:** **Remind me tomorrow**
  hides it for a day, even across a restart, and **Skip this version** hides
  it until a newer version is out, so someone who doesn't want 0.3.0 is
  still told about 0.4.0. There is no way to hide updates for good short of
  turning the check off; that would leave people on old bugs without
  knowing. Settings always shows what is available, so putting the banner
  away loses nothing. There is no close button, because it wouldn't say
  which of the two it meant.
- **Release notes are read in Neap,** in a dialog, not on a web page: the
  notes come with the version check, so reading them asks for nothing more,
  and the person is deciding whether to update inside the app. The install
  steps are left out, since they are for someone downloading by hand. A
  release whose notes can't be read opens its page instead.
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
