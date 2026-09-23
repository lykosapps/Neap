# Security

## Reporting a problem

Please report security problems privately, through GitHub's **Report a
vulnerability** button on this repository's Security tab, rather than in a
public issue.

## What counts

The app runs without admin rights and talks to the headset over its HID
control channel. The problems that matter most are ones that could:

- send the headset a write outside the confirmed settings registry, or a
  value outside a setting's range, which the client is meant to refuse;
- reach the firmware update path, which this project never uses;
- leave other applications' volumes turned down after the app exits, beyond
  what the volume journal restores;
- let another program or user on the machine drive the app.

A headset setting that simply does the wrong thing is a bug, not a security
problem: please open an ordinary issue.
