# Contributing

Thanks for looking. This app talks to real hardware, so a change is only
finished when it has been tried on a headset as well as by the tests.

## Building and testing

You need Windows 10 version 2004 or later and the .NET SDK version named in
`global.json`.

```
dotnet build Neap.sln -c Release
dotnet test --project tests/Neap.Core.Tests -c Release
dotnet format Neap.sln --verify-no-changes
```

XAML is formatted by XAML Styler, installed as a tool of this repository. In
PowerShell:

```
dotnet tool restore
dotnet xstyler -f ((git ls-files '*.xaml') -join ',')
```

Add `-p` to check without changing anything. Any warning fails the build.
The build, the tests and both formatting checks run on every push and pull
request.

The tests cover what needs no hardware: the frames sent to the headset, the
parsing of its replies, which writes are allowed, the transmitter slots, the
settings registry, presets against a scripted headset, the connection states,
the routing warning, the mix and its volume journal, and the app's resource
file. When you learn something
new about the protocol, add a test that would have caught the old mistake.

## Testing the screens without the headset

`--pretend` runs the app against a pretend headset: nothing reaches the
hardware, Windows' audio, the Startup folder or the real app's settings,
and it can run beside the real app. Publish, then run
`powershell -ExecutionPolicy Bypass -File tools\pretend.ps1`. It operates
every screen through UI Automation, checks each command sent against the
registry, and takes screenshots. It comes to the front as it changes page,
so run it while nobody is using the machine. Add `-Light` to see every
screen in the light theme without changing Windows' own.

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
   | Switch it off on the cable | *Not connected*, then *Headset off*, with nothing in between | |
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

## Pull requests

Keep each one to a single change, with a commit message that says what
changed and why. Run the formatter before pushing.

By opening a pull request you agree that your contribution is licensed under
the same terms as the project: the GNU General Public License, version 3 or
later.
