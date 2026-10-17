---
name: wsl-distro-checks
description: "Rocky Linux 10 and openSUSE Tumbleweed WSL systems kept (owner's choice) for re-checking the Linux app and testing the package install"
metadata:
  node_type: memory
  type: project
  originSessionId: fbc97cb2-a5bc-4426-a3dd-08a7a872d8fa
  modified: 2026-10-07T00:01:18.182Z
---

Installed 2026-10-07 with the owner's OK: WSL distros `RLCPlus-10` (Rocky 10) and `openSUSE-Tumbleweed`, alongside Ubuntu-24.04. About 4 GB together. Owner said keep them for now; delete both once Linux packaging is done.

**Why:** Deck was unreachable and the owner has no second Linux PC, so these are the only other-distro checks.

**How to apply:** Publish self-contained (`dotnet publish src/Neap.Desktop -c Release -r linux-x64 -f net10.0 --self-contained`), then run a page-capture script in each distro as root with `export HOME=/root` (WSL passes a bogus HOME otherwise). openSUSE: `xvfb-run` (needs xorg-x11-server-Xvfb + xvfb-run packages). Rocky 10 has no Xvfb and Xwayland-run fails under WSL, so use WSLg's `DISPLAY=:0` (windows may flash on the owner's desktop; pass `--behind`). Both draw all seven pages and a second launch wakes the first. See [[scoped-pretend-runs]] for pretend-run etiquette.
