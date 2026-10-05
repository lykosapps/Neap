# Help support another headset

Neap is built for the Stealth Pro II. If you have a different Turtle Beach
headset, you can help it work with Neap by recording what your headset does
while you press its buttons. It takes about ten minutes, and you don't need
to install anything except Neap.

Recording only listens. It doesn't change anything on your headset.

## Before you start

- Get Neap from the
  [latest release](https://github.com/lykosapps/Neap/releases/latest) if
  you don't have it yet.
- Close Swarm II completely. Only one app can talk to the headset at a time.
  If its icon is by the clock (click the small arrow there to check),
  right-click it and quit.
- Switch your headset on and connect it the way you normally do.

## Record

1. Open Neap and go to **Settings**.
2. Under **Diagnostics**, next to **Record headset activity**, choose
   **Start**.
3. Do everything in the list below, in order. After each one, **count
   slowly to five** before doing the next. You don't need to write anything
   down: the order and the pauses show which is which.
4. Choose **Stop and save**.

The same steps are in Neap: choose the **?** button next to **Start**.

If Neap says it can't find your headset, record anyway. That tells us
something too.

## The list

Skip anything your headset doesn't have.

1. Turn the volume down a few steps, then back up.
2. Turn the game and chat balance dial one way, then back.
3. Mute the microphone, then unmute it. On many headsets you do this by
   lifting the microphone arm up, then putting it back down.
4. Press the noise-cancelling or mode button. Count to five, and keep
   pressing until it's back where it started.
5. Press any other button once, then again to put it back. Press it
   briefly. Holding a button down can start Bluetooth pairing.
6. Switch the headset off, wait ten seconds, then switch it back on.

## Send it

1. In Neap, choose **Report on GitHub**. Neap first shows what it found on
   your headset. Then your browser opens a form with some details already
   filled in, and the folder with your recording opens in front of it.
2. Sign in to GitHub. If you don't have an account, creating one is free
   and takes a minute or two.
3. Type your headset's name as it appears on the box, for example
   "Stealth 700 Gen 3".
4. Drag the recording from the folder into the box marked **Recording**,
   say which steps you skipped, and submit it.

The file doesn't contain your headset's serial number or anything else
that identifies you. Open it in Notepad if you'd like to check before
sending.

## Going further

Some settings can only be changed in Swarm II, such as the equaliser and
how loudly you hear your own voice. Supporting those means recording what
Swarm II sends to the headset. This part is optional and more technical:
it needs administrator rights and a restart.

1. Install [USBPcap](https://desowin.org/usbpcap/), then restart Windows.
2. Open **Windows PowerShell** as administrator and run:

   ```
   & "$env:ProgramFiles\USBPcap\USBPcapCMD.exe"
   ```

   It lists each USB connection (`\\.\USBPcap1`, `\\.\USBPcap2` and so on)
   and the devices on it. Find your transmitter or dock under its Turtle
   Beach name, note the connection's number and the device's own number
   beside its name, then close that window.
3. Quit Neap from its icon by the clock, and open Swarm II.
4. Download [capture.ps1](../tools/capture.ps1) and, in the same PowerShell
   window, run it with your two numbers, for example:

   ```
   .\capture.ps1 -Interface 2 -Device 1 -Seconds 600 -Name my-headset
   ```

   That records for ten minutes. If PowerShell refuses to run the script,
   run `Set-ExecutionPolicy -Scope Process Bypass` first.
5. While it records, change one setting at a time in Swarm II, wait about
   five seconds between changes, and write down the time and what you
   changed, for example `14:02:10 mic volume 50 to 70`.

The recording is saved as `my-headset.pcap`. Type `%TEMP%\tbcap` in File
Explorer's address bar to find it.

**Don't attach this one to the issue.** Unlike Neap's file, it contains
your headset's serial number. Say in the issue that you have it, and we'll
arrange a private way to send it.
