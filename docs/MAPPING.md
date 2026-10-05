# Help support another headset

Neap is built and tested on the Stealth Pro II. Other Turtle Beach headsets
may speak a similar language, but each setting has to be matched to what
the headset sends before Neap can safely change it. Swarm II already knows
how, so the way to learn is to record Swarm II talking to your headset
while you change one thing at a time.

This takes about half an hour, a Windows PC with administrator rights, and
one restart.

## 1. Record what Neap sees

With your headset connected and switched on, open Neap, go to **Settings**,
and under **Diagnostics** choose **Start** next to **Record headset
activity**. Wait ten seconds, then choose **Stop and save**.

Neap saves a text file to your Downloads folder. It lists the Turtle Beach
devices plugged in and every value the headset reported, even ones Neap has
no name for. The headset's serial number and radio addresses are left out,
so this file is safe to post publicly.

## 2. Install USBPcap

[USBPcap](https://desowin.org/usbpcap/) records what passes over a USB
connection. Install it, then restart Windows: it doesn't work until you do.

## 3. Find the headset's connection

Open **Windows PowerShell** as administrator and run:

```
& "$env:ProgramFiles\USBPcap\USBPcapCMD.exe"
```

It lists each USB connection (`\\.\USBPcap1`, `\\.\USBPcap2` and so on) and
the devices on it. Find your transmitter or dock under its Turtle Beach name
and note two numbers: the connection's number, and the device's own number
beside its name. Close the window.

## 4. Record Swarm II

1. Quit Neap from its icon in the notification area, so it isn't talking to
   the headset at the same time.
2. Open Swarm II with your headset connected.
3. Download [capture.ps1](../tools/capture.ps1) and, in the same
   administrator PowerShell, run it with your two numbers, for example:

   ```
   .\capture.ps1 -Interface 2 -Device 1 -Seconds 600 -Name my-headset
   ```

   That records for ten minutes. If PowerShell refuses to run the script,
   run `Set-ExecutionPolicy -Scope Process Bypass` first.
4. While it records, change **one thing at a time** in Swarm II, and wait
   about five seconds between changes. Write down the time and what you
   changed, for example `14:02:10 mic volume 50 to 70`. Go through as many
   settings as you can, then press each button and turn each dial on the
   headset itself.

The recording is saved in your Temp folder as `tbcap\my-headset.pcap`. Type
`%TEMP%\tbcap` in File Explorer's address bar to find it.

## 5. Send it

[Open an issue](https://github.com/lykosapps/Neap/issues) named after your
headset, such as "Support for the Stealth 700 Gen 3", and attach the file
from step 1 and your notes.

**Don't attach the USB recording to the issue.** Unlike Neap's file, it
holds your headset's serial number and radio addresses. Say in the issue
that you have it, and we'll arrange a private way to send it.
