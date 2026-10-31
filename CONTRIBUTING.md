# Contributing

Thanks for looking. This app talks to real hardware, so a change is only
finished when it has been tried on a headset as well as by the tests.

## Building and testing

You need the .NET SDK version named in `global.json`, and either Windows 10
version 2004 or later (for the Windows build) or a Linux machine (for the
Linux one; WSL does for everything but the headset).

```
dotnet build Neap.sln -c Release
dotnet test --project tests/Neap.Core.Tests -c Release
dotnet format Neap.sln --verify-no-changes
```

On Linux, build the app for Linux alone, since the solution's Windows
parts do not build there:

```
dotnet build src/Neap.Desktop -c Release -f net10.0
dotnet test --project tests/Neap.Core.Tests -c Release
```

The same tests run on both. A few run on Linux only, such as the headset
permission rule written by a real shell, so both are checked on every push.

Avalonia's build tools report each build to Avalonia (the project's name,
its target framework and runtime, and Avalonia's version). The build on
GitHub turns this off; to turn it off on your own machine, set the
environment variable `AVALONIA_TELEMETRY_OPTOUT` to `1`, as you may already
have done for .NET's own with `DOTNET_CLI_TELEMETRY_OPTOUT`.

Any warning fails the build. The build, the tests and the formatting check
run on every push and pull request.

`tools/pre-commit` runs a fast version of the formatting check on the
files being committed. Copy it to `.git/hooks/pre-commit` to use it.

`tools/pre-push` refuses a push to a remote under another account, or one
carrying commits by another author; the account and author are in `.tenon`.
Copy it to `.git/hooks/pre-push` to use it.

The tests cover what needs no hardware: the frames sent to the headset, the
parsing of its replies, which writes are allowed, the transmitter slots, the
settings registry, presets against a scripted headset, the connection
states, the routing warning, the mix and its volume journal, the app's
resource file, and the contrast of its colours in the dark and light themes.
When you learn something new about the protocol, add a test that would have
caught the old mistake.

## Testing the screens without the headset

`--pretend` runs the app against a pretend headset: nothing reaches the
hardware, the system's audio, the Startup folder or the real app's settings,
and it can run beside the real app. On Windows, publish, then run
`powershell -ExecutionPolicy Bypass -File tools\pretend.ps1`. It operates
every screen through UI Automation, checks each command sent against the
registry, and takes screenshots. It comes to the front as it changes page,
so run it while nobody is using the machine. Add `-Theme Light` or
`-Theme Dark` to see every screen in that theme without changing Windows'
own.

To look at a screen while somebody works, launch it on that screen instead:
`Neap.exe --pretend --behind --page audio` opens behind every window and
never comes forward. The tags are `home`, `audio`, `mic`, `controls`,
`device` and `settings`. Add `--parametric` with `--wired` to open the
Stealth Pro II's game equaliser in its parametric mode, turned on.

## Building the app

One app, drawn the same on both systems (Avalonia):

```
dotnet publish src/Neap.Desktop -c Release -r win-x64 -f net10.0-windows10.0.19041.0 --self-contained
dotnet publish src/Neap.Desktop -c Release -r linux-x64 -f net10.0 --self-contained
```

Run `Neap.Desktop.exe` (Windows) or `Neap.Desktop` (Linux) from the `publish`
folder. A release renames the Windows file `Neap.exe`, which the updater in
every installed copy looks for. You need the .NET 10 SDK to build, but not to
run the result. The Windows build can only be made on Windows; the Linux one
builds on either. To make the Linux AppImage from a Linux publish folder, see
`packaging/linux/build-appimage.sh`.

## The probe

`Neap.Probe` is a console harness that talks to the headset without a
UI, on Windows or Linux. It is how the protocol was worked out and it is still
the fastest way to see what the hardware is actually saying:

```
dotnet run --project src/Neap.Probe -c Release -- read
```

`devices`, `read`, `named`, `registry`, `presets`, `transmitters`, `json`,
`watch`, `diff`, `audio` and `route` are the ones worth knowing — `route`
says where Windows sends sound and takes the microphone from, for each of its
roles, and which of the headset's devices is carrying sound right now. The
ones that ask Windows about sound (`audio`, `route`, `hear`, `formats` and a
few more) run only on Windows; on Linux, `sound` asks the sound server in
their place, and most of the others run on both.

A few more matter when something is not behaving:

- `raw [seconds] [usage page]` prints every report as it arrives, decoded or
  not, and does not require the device to answer a command first. It listens
  for what the device pushes and asks for what it answers, and labels which
  path saw each report — a path that cannot see anything says so rather than
  going quiet. Usage page `000c` is where the volume wheel pushes its clicks;
  the chat wheel reports on the settings channel, through whichever
  transmitter is carrying the headset's controls.
- `who` asks every transmitter plugged in, separately, which one is carrying
  the headset's controls. With two plugged in, the answer is not always the
  one playing the sound.
- `hear <output>` plays a soft beep on one output every three seconds and
  prints each of the headset's microphones' levels, second by second. It is
  for what a default device cannot tell you: whether a device reaches the
  headset at all, and which microphone actually hears you.
- `decode <file.pcap>` reads a USBPcap recording of Swarm II driving the
  headset and prints every command and reply, naming the ones in the
  registry. This is how anything new gets learned — the registry was built
  by watching the vendor's app. Capture with `tools/capture.ps1`.

## Safety

Read-first. The client writes nothing unless it is made with writes allowed,
and even then it sends only settings in the confirmed registry, within their
confirmed ranges. The one write known to cause harm, a transmitter slot's
base address, is refused outright. Nothing touches the firmware update path,
which is signed and encrypted.

One warning worth repeating from the findings: a transmitter will answer
questions about a headset it can no longer reach, with stale values, and the
chip vendor's own firmware commands reach only the USB device you are plugged
into, never the headset behind it. Confirm which device answered before
trusting a result.

## Working with the hardware

- **One process at a time.** Only one program can usefully hold the
  headset's control channel. Close Swarm II and this app before using the
  probe. Any other tool reading the control channel takes replies meant for
  Neap, so stop Neap before using one, or log from inside Neap.
- **Prove which device you are talking to.** A transmitter answers questions
  about a headset it can no longer reach, with stale values. The probe's `who`
  command asks each one separately.
- **A repeated report is not a new answer.** A transmitter read before it has
  anything new hands back its last report again. Count a reply once, and on
  Linux go by a report's length byte, not by what is left in its buffer.
- **A transmitter can stop answering until it is replugged.** One plugged in
  with nothing reading it, or suspended by the system, can give only empty
  reports. A reset sometimes brings it back; unplugging it and plugging it
  back in with Neap already running always has so far.
- **Never write a transmitter slot's base address** (0x400, 0x420, 0x440,
  0x460). Writing one once left a headset selected onto the wrong
  transmitter. See FINDINGS.md.
- **Anything the headset keeps is a risk.** A setting written through the
  registry is put back by writing the old value. A stored setting written
  beneath the protocol (FINDINGS.md, *The firmware layer*), the loudness
  boost's level among them, survives a restart, and a firmware edit can stop
  the headset starting at all: one did. Back up first, know how to put it
  back, and ask the headset's owner before trying either.

## Performance budgets

Measured on a release build, with the pretend headset until it has also been
done once with a real one. Re-measure before each release, on the same kind of
machine, and record the reading beside the version.

| Moment | Budget | Latest reading |
|---|---|---|
| Start: first usable screen | 2 s (5 s after a restart) | 1.45 s typical, Windows, Ryzen 7 7800X3D, 0.2.x new app (the old app: 0.81 s) |
| Hidden in the notification area: memory | under 50 MB resident | about 12 MB, Windows |
| Doing nothing: processor | under 1% of one core | 0.1 to 0.5%, Windows |
| Over an hour: memory | level, not creeping | levels off after about 5 minutes (about 180 MB private), Windows; Linux not yet measured past 4 minutes |
| Idle wake-ups, Linux key listener | none that are not needed | 4 a second |
| Download, Windows zip | no more than the last plus 10%, or say why | 53 MB (the old app: 91 MB) |

To measure: start the published app with `--pretend --behind` and its own
`NEAP_PRETEND_FOLDER`, sample its working set and processor time every 20
seconds with the window open and then closed to the notification area, and
time five starts. Never start a build without `--pretend` for this: a real
copy rewrites the sign-in shortcut and takes the headset.

## Before a release

Last of all, `powershell -ExecutionPolicy Bypass -File tools\release-check.ps1`
says whether main is ready to tag, and lists what is left if not: anything
uncommitted, unpushed or only on GitHub, anything in the unpushed commits that
could identify the owner, a version, changelog date or Unreleased section out
of step, and the backlog's items for before the tag.

Run through this with the release build, not a debug one. Most of what needs
the headset can be run for you: `powershell -ExecutionPolicy Bypass -File
tools\ui\run.ps1 hardware -Parts BFKLT` runs the real app and reads the
headset back with the probe, covering 2, 3, 5, 6 (the modes), 7 (what the
page says), 9 and 10 below. It stops your own copy of Neap for the run and
puts it back. Run through `tools\pretend-quiet.ps1 -Run 'tools\ui\run.ps1
hardware -Parts BFLT -Inside'` it works on a desktop of its own and takes
nobody's screen; part K sends real key presses, so it runs on its own when
nobody is at the machine. Unplugging, the wheel, the Mode button, hearing the result, a game
in front, signing out and updating from an installed copy stay by hand.

1. **Every connection state.** One row per arrangement, the same rows as
   `ConnectionMatrixTests`. Start each from a working headset. The headset
   keeps its settings on the transmitter it was switched on with and
   CrossPlay moves only its sound, so to put the settings on one transmitter,
   unplug the other before switching the headset on.

   | Do this | The header says | The one thing Home tells you to do |
   |---|---|---|
   | Sound and settings on one transmitter | *Headset connected* | |
   | Settings on one, CrossPlay the sound to the other | *Headset connected* | |
   | Then point Windows at the wrong transmitter | *No sound* | choose the right one in Sound settings |
   | Settings on one, sound on the other; unplug the sound one | *No sound* | press CrossPlay |
   | Settings on one, sound on the other; unplug the settings one | *Settings unavailable* | switch off and on |
   | Both on one transmitter; unplug it, the other stays in | *Settings unavailable*, says no sound reaches the headset | switch off and on |
   | Switch the headset off | *Not connected* for as long as it is off, including when the other transmitter was used earlier | switch it on |
   | Switch it on | *Headset connected* | |
   | Unplug everything | *Nothing plugged in* | |
   | Plug in the USB-C cable, headset on | *Headset connected*; sound and microphone move to the cable | |
   | With only the cable plugged in, turn the chat wheel | *Headset connected*; the mix card says the chat wheel doesn't work on the cable | use the dial |
   | Switch it off on the cable | *Not connected*, then *Headset off*, with nothing in between; Home says the headset is off throughout | switch it on |
   | On Linux, with Neap's access rule removed, start the app | *No access* in the header, Home and the mini panel | allow access, from the notice |
   | With the settings unavailable, restart the app | still *Settings unavailable* | switch off and on |

   A state that no row covers is a bug, and gets a row here and a test.

2. **The mix.** Turn the chat wheel through its range and back; the mix
   follows it and beeps once at the centre.
3. **Recovery.** Move the mix fully to chat, end the app from Task Manager,
   and start it again. Every other app's volume comes back.
4. **Presets.** Save a preset named with an ampersand and 19 characters,
   overwrite it, then delete it. The probe's `presets` command agrees at
   each step.
5. **Audio format.** Change the headset's format on the Device page twice
   with the mouse, then twice from the keyboard (Alt+Down, an arrow key,
   Enter). The app stays open, Windows Sound settings shows the same format
   each time, and at 24-bit, 96 kHz the Charging Dock's status ring turns
   purple.
6. **Noise control.** Choose each mode on Audio and hear it change. Set
   Blocking to something other than full, then go to transparency and back:
   noise cancellation returns at that level. Choose the Mode button's cycle
   on Controls and press it six times, pausing on each: noise cancellation,
   transparency, off, twice over, with only a moment of off on the way into
   transparency. From off, the press lands on noise cancellation with no
   transparency first. Close Neap: the button turns noise cancellation on
   and off.
7. **Spare battery.** With the headset on the Charging Dock and the Device
   page open, take the spare out of the dock's slot: the dock's panel says
   *Empty* within a couple of seconds. Put it back: it shows the charge the
   phone app shows. CrossPlay to the USB Transmitter: the line goes.
8. **Linux: headset access.** On a system without the rule (delete
   `/etc/udev/rules.d/70-neap.rules` and replug the transmitter), every page
   says Neap is not allowed to use the headset, with no "switch it on" cards.
   **Allow access** asks for the password once, installs the rule, and the
   headset appears without replugging. Where the system has no password
   prompt, the button copies the command to run. With an older rule
   installed, Neap offers the current one. With the transmitter plugged in
   long before Neap starts, Neap connects, or, if its one restart brings
   nothing back, Home asks for the transmitter to be unplugged and plugged
   back in, and the headset appears once it is.
9. **Shortcuts.** With the switch on, and a game or any program in front, the
   three keys move the mix (Linux: an X program; a native Wayland one is out
   of reach). A key another program holds shows its trouble beside it.
10. **Start when you sign in.** Turn it on, sign out and back in: Neap is in
    the notification area with no window (minimised, on a desktop with no
    notification area). Move the program or, for an AppImage, run a new file
    once: the entry points at it.
11. **Updating.** On Windows, an installed older copy updates through
    **Update and restart** and comes back as the new app with its settings.
    On Linux the banner and Settings card offer What's new and Download, and
    nothing is installed. With the window closed, a newer version shows a
    notice (Windows: from the notification area; Linux: a desktop
    notification).
12. **Screen reader, 200% text and high contrast.** Narrator on Windows and
    Orca on Linux, reading every page and every dialog; the keyboard reaches
    everything.

## Pull requests

Keep each one to a single change, with a commit message that says what
changed and why. Run the formatter before pushing.

By opening a pull request you agree that your contribution is licensed under
the same terms as the project: the GNU General Public License, version 3 or
later.
