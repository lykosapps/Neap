# Architecture

Two halves, with very different dependencies. Keeping them apart is the main
structural decision in this project. A second decision sits across both: the
same app runs on Windows and on Linux, so what differs between the two
systems is kept in a few named places.

## The projects

- **`Neap.Core`** — the protocol, the settings registry, presets, the mix,
  and everything that reaches a system: the sound system (Windows' audio
  APIs, or PulseAudio and PipeWire), the headset's control channel (Windows
  HID, or Linux hidraw) and key grabs (X11). No UI types at all, so the probe
  and the app get exactly the same behaviour. The rules that decide anything
  (what a screen shows for a state, which key means what, how a newer version
  is offered) live here, with tests.
- **`Neap.Services`** — what the screens share and Core has no business with:
  the headset connection and the services built on it (mix, profiles,
  updates, recording), the settings file, the log, and `IPlatform`, the one
  seam through which the app asks the operating system for things that differ
  (the UI thread, volumes, shortcuts, the dock's ring, spatial sound). No
  UI toolkit.
- **`Neap.Desktop`** — Neap itself: one Avalonia application, drawn the same
  on both systems, with its words in one resource file. It builds twice: for
  every system, and for Windows with what only Windows gives it.
- **`Neap.WinRt`** — Windows' spatial sound, which only the Windows build can
  reach. A small project so the rest never needs a Windows-only reference.
- **`Neap.Probe`** — a console harness over the same core. It is how
  the protocol was worked out and it is still the fastest way to ask the
  hardware a question.
- **`Neap.Core.Tests`** — everything that can be checked without a
  headset, against a scripted one, on either system.

The other projects keep the headset's name because they are about the
headset. Both applications publish self-contained, so nothing has to be
installed on the machine that runs them: a zip on Windows, an AppImage on
Linux.

### What differs between systems

Everything above Core is the same code. Where it differs, one of two things
is true: Core has a Windows class and a Linux class behind one interface
(`IHidTransport`, `IMicrophoneListener`, `IPlayback`, the route and volume
sources), chosen by `OperatingSystem`; or the feature exists on one system
only and says so, through `Unsupported`, so a screen leaves the control out
rather than show one that does nothing. Today that is spatial sound, the audio
format and the dock's ring, which are Windows' alone.

On Linux the pieces are:

- **The headset's control channel** is the kernel's hidraw device, read
  without opening for what it is (`Hid/ReportDescriptor.cs`) and opened when
  needed. Linux gives it to root unless a udev rule says otherwise, so a
  refusal is told apart from an absent headset (`AccessDeniedException`) and
  the app can install the rule once, through the system's own password prompt
  (`HeadsetAccess`, `Hid/UdevRule.cs`).
- **Sound** goes through `libpulse`, which PulseAudio and PipeWire both
  speak (`Audio/Pulse`). An application is its streams, grouped by program,
  the way Windows groups sessions.
- **Shortcuts** are taken from the X window system (`Input/X11KeyGrabber.cs`),
  stored as Windows key codes so one settings file and one screen serve both.
- **The notification area** may not exist (GNOME without its extension), so
  the app asks the session bus whether it does, and closing the window closes
  the app when it does not.

## The headset half — no dependencies

Everything the headset itself does is reached over its own HID protocol:
noise cancellation, both equalisers, microphone, noise gate, Superhuman
Hearing, button and dial assignment, transmitter lighting, battery, power,
wake on motion, voice prompts. Every setting is in the registry,
`Settings/Registry.cs`, and every write is checked against it.

This needs **nothing installed** — no driver, no Turtle Beach software, no
admin rights.

See `Neap.Core/Hid/`, `Protocol/`, `Settings/` and `HeadsetClient.cs`.

### The rule this half keeps breaking

A transmitter answers questions about a headset it can no longer reach. It
holds the last values it was told and serves them cheerfully, so a command
sent to the wrong device does not fail — it succeeds, with stale data. That
has produced wrong conclusions repeatedly: the wrong HID collection, the
wrong audio endpoint, the wrong transmitter slot, all in one session.

So `HeadsetClient.Behind` does not choose a device by product id. It opens
each one and asks, and the first that answers wins. Anything that talks to
the hardware goes through it, including the probe, which spent one session
reading four empty transmitter slots off a headset that was sitting there
connected.

### Knowing what state the headset is in

Nothing on the wire says "connected", so the state is assembled from what can
be observed: which device answers, which transmitter the headset's slots name
as carrying its sound, whether `0x230` says any sound is arriving, and — over
the USB-C cable — whether the headset's own sound device is present in
Windows. What came just before matters too: silence after a transmitter was
unplugged means the settings left with it, and silence in place means the
headset went. `Connection/LinkTracker.cs` holds those rules, each beside the
observation that forced it, and is tested without hardware. The app's
`Services/StateCopy.cs` explains each state, with its sentences in the
resource file, so no two places can word one differently.

The headset keeps its controls on the transmitter it was switched on with,
and CrossPlay moves only its sound, so the device answering and the
transmitter carrying the sound are two facts, tracked apart.

## The PC-audio half — where the problems live

The transmitter presents Windows a **single stereo output**. The headset
therefore cannot separate game audio from chat on a PC. Any game/chat mix
has to happen on the PC.

### How it works now

The chat application and the game both play natively to the headset, exactly
as they would with nothing running. The mix sets the chat application's
session volumes to the chat half of the crossfade and everything else on that
endpoint to the game half. Nothing sits in the audio path.

That means the only configuration is naming the chat application, and the app
asks for it in one card. On Linux the same idea runs on the sound server's own
per-stream volumes (`Mix/PulsePlayback.cs`): a stream's volume belongs to the
stream, not to the output, so one record in the journal covers every output.

See `Neap.Core/Mix/SessionMix.cs`, which documents the measurements.

### What this replaced, and why it is worth knowing

The earlier design gave chat a device of its own — a virtual audio cable the
person installed and pointed their chat application at — captured that device
by loopback, and rendered it into the headset as a stream of our own.

It worked. It also cost a driver install and a reboot, a licence that
restricts redistribution, a real-time capture and render path, drift
correction between two independent clocks, and a supervisor to restart the
engine when it died — plus every failure mode that came with sitting in the
audio path, where a live process with a dead output is the failure that looks
healthiest.

None of it was necessary. Session volume is already a working per-application
gain; it is how the game side always worked. Naming the chat application does
the whole job.

**Kept as history rather than as code.** The old engine has been removed. The
probe keeps the measurement behind what replaced it, `loopback`, which found
that process capture taps the stream after session volume, and `mixapp`,
which drives the mix engine itself so a sweep can be watched session by
session.

### The limit, stated plainly

Routing by application means the chat application has to be its own process.
Discord, Teams and Steam are. A call taken in a browser is not, because the
browser also carries game and media audio. That case still wants a virtual
cable, and this app does not provide one.

### What the mix owes the user

It holds other applications' volumes down while it runs, and it is the only
thing that knows what they were. If it dies without putting them back,
somebody is left with a quiet Spotify and no idea why.

So every change is journalled before it is made and replayed on the next
launch. Recovery restores a session only if it is still sitting at the value
we wrote; if the person has moved it since, their choice wins. A prototype
without this ratcheted an application to silence in three commands.

Two rules fall out of that and are easy to get wrong:

- **Never clear a journal you do not own.** Clearing it on exit without
  having written anything lets a throwaway launch that only lists devices
  wipe the record of a crashed run.
- **Never fail recovery silently.** If the endpoint is missing the journal is
  kept, not deleted.

### Windows disagreeing with the headset

The transmitters each present their own endpoints, so Windows can be playing
to the dock while the headset is on the dongle — everything looks connected
and no sound arrives. `Mix/SessionMix.cs` notices the chat application
playing somewhere other than the headset, and `Audio/Routing.cs` maps
endpoints back to the transmitter they belong to so the app can say which one
Windows should be using. It says it; it does not switch it. Switching was
considered and dropped as too unreliable to do behind someone's back.

Two things widen it. Windows keeps a separate default for communications,
which does not move when the output is changed, so that is checked as well.
And while the headset is on its USB-C cable, the cable is the only right place
for sound, calls and the microphone, whichever transmitter the headset is on:
it plays one source at a time, and sends the voice only over the cable.
`Services/AudioRoute.cs` watches where Windows points, and everything that
touches a Windows device — the volume and microphone sliders, the chat app
picker, the centre beep — follows the headset device Windows is actually
using, not the first whose name matches.

## Where the audio truth lives

`MixFormat` is always 32-bit float in shared mode, so it says nothing about
what the device is configured for. The format Windows actually shows in Sound
settings is a device property, and reading it is the only way to report the
truth. See `Audio/DeviceFormat.cs`.

## Updating the app itself

The one thing that goes over a network. `Core/Updates` holds the rules:
reading GitHub's latest release and refusing anything that isn't exactly
what the release workflow publishes (`Release`), once a day at most
(`UpdateSchedule`), what the Settings card offers at each stage
(`UpdateLook`), and putting the new files in place (`FolderSwap`). The app's
`Services/UpdateService.cs` does the downloading and checking.

Windows won't let a running program's files be overwritten, but will let
them be renamed. So the running version unzips the new one into a hidden
`.update` folder inside its own, renames each file it replaces aside, moves
the new one in, lets go of its one-copy lock and starts the new version.
Because the old version does all of it, the new version needs no part in
the swap; it only clears the `.update` folder when it starts. This is Neap's
own update and nothing to do with the headset's firmware, which stays with
Swarm II.

The updater runs as the person, never elevated, so it can only update a copy
in a folder they can write to; anywhere else it says a newer version exists
and sends them to the download (`UpdateOffer`).

Linux never updates itself. The program is one that an AppImage, a package or
the person's own hands put where it is, and replacing files under a running copy
crashed it, so Neap says a newer version is out and opens its release page.
It uses the same check and the same offer, which decides that a system that
cannot install is sent to the download.
