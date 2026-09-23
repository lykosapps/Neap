# Contributing

Thanks for looking. This app talks to real hardware, so a change is only
finished when it has been tried on a headset as well as by the tests.

## Building and testing

You need Windows 10 version 2004 or later and the .NET SDK version named in
`global.json`.

```
dotnet build dotnet/StealthPro.sln -c Release
dotnet test --project dotnet/StealthPro.Core.Tests -c Release
dotnet format dotnet/StealthPro.sln --verify-no-changes
```

Any warning fails the build. The same three commands run on every push and
pull request.

The tests cover what needs no hardware: the frames sent to the headset, the
parsing of its replies, which writes are allowed, the transmitter slots, the
settings registry, presets against a scripted headset, the connection states,
the routing warning, the mix and its volume journal, and the app's resource
file. When you learn something
new about the protocol, add a test that would have caught the old mistake.

## Working with the hardware

- **One process at a time.** Only one program can usefully hold the
  headset's control channel. Close Swarm II and this app before using the
  probe.
- **Prove which device you are talking to.** A transmitter answers questions
  about a headset it can no longer reach, with stale values. The probe's `who`
  command asks each one separately.
- **Never touch the firmware update path**, and never write a transmitter
  slot's base address (0x400, 0x420, 0x440, 0x460). Writing one once left a
  headset selected onto the wrong transmitter. See FINDINGS.md.

## Before a release

Run through this with the release build, not a debug one:

1. **Each way of connecting.** With the Charging Dock alone, the USB
   Transmitter alone, and both, check the header reads *Headset connected*.
   Switch the headset off: *Not connected*. With both plugged in, unplug the
   one carrying the sound while the other carries the settings: *No sound*.
   Unplug the one carrying the settings while the other stays in: *Settings
   unavailable*. With nothing plugged in: *Nothing plugged in*.
2. **The USB-C cable.** Plug it in with the headset on: sound and microphone
   move to the cable. Switch the headset off on the cable: *Headset off*.
3. **The mix.** Turn the chat wheel through its range and back; the mix
   follows it and beeps once at the centre.
4. **Recovery.** Move the mix fully to chat, end the app from Task Manager,
   and start it again. Every other app's volume comes back.
5. **Presets.** Save a preset named with an ampersand and 19 characters,
   overwrite it, then delete it. The probe's `presets` command agrees at
   each step.
6. **Audio format.** Change the headset's format on the Device page and
   check Windows Sound settings shows the same.

## Pull requests

Keep each one to a single change, with a commit message that says what
changed and why. Run the formatter before pushing.
