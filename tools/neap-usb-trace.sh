#!/bin/bash
# Records, passively, what happens between a Linux PC and the Atlas Air's USB transmitter across
# boots, shutdowns, sleeps and unplugs, so one report can show why the transmitter stops answering.
#
#   sudo bash neap-usb-trace.sh install     once. Starts a background recorder that survives restarts.
#   (use the PC as normal for a few days, shut down and start it as you usually do)
#   sudo neap-usb-trace collect             after a boot where Neap says the headset is not connected.
#                                           Does an unplug/replug test and writes one file to attach.
#   sudo neap-usb-trace uninstall           removes everything it installed, including its logs.
#
# The recorder only listens. It records USB control requests (never what is typed, and the data of
# anything but Turtle Beach devices and descriptors is left out), the transmitter's power and driver
# state, USB lines of the kernel log, and when the PC sleeps, wakes or shuts down. It keeps about
# 40 MB at most and sends the transmitter nothing.

ROOT=${ROOT:-}
SYS=${SYS:-/sys}
LOGDIR=${LOGDIR:-/var/log/neap-usb-trace}
LIB=$ROOT/usr/local/lib/neap-usb-trace
BIN=$ROOT/usr/local/sbin/neap-usb-trace
UNIT=$ROOT/etc/systemd/system/neap-usb-trace.service
HOOK=$ROOT/usr/lib/systemd/system-sleep/neap-usb-trace
VENDOR=${VENDOR:-10f5}
PRODUCT=${PRODUCT:-225e}

need_root() { [ "$(id -u)" = 0 ] || { echo "Run it with sudo:  sudo bash $0 $1"; exit 1; }; }
ask() { [ -n "$ASSUME_YES" ] && return 0; local a; read -r -p "$1 [Y/n] " a; [[ ! $a =~ ^[Nn] ]]; }
say() { echo "$@"; }

# -- the recorder ---------------------------------------------------------------------------
write_daemon() {
    cat > "$LIB/daemon.py" <<'PYEOF'
#!/usr/bin/env python3
"""Passive recorder for Turtle Beach USB devices. Reads; sends nothing."""
import datetime, glob, os, re, signal, subprocess, sys, threading, time

SYS = os.environ.get("SYS", "/sys")
LOGDIR = os.environ.get("LOGDIR", "/var/log/neap-usb-trace")
MON = os.environ.get("MON", SYS + "/kernel/debug/usb/usbmon/0u")
VENDOR = os.environ.get("VENDOR", "10f5")
MAX_BYTES = int(os.environ.get("MAX_BYTES", 6 * 1024 * 1024))
KEEP = 5
GET_DESCRIPTOR = "80 06"


def now():
    return datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]


class Log:
    def __init__(self, name):
        os.makedirs(LOGDIR, exist_ok=True)
        self.path = os.path.join(LOGDIR, name)
        self.lock = threading.Lock()
        self.f = open(self.path, "a", buffering=1)

    def write(self, text):
        with self.lock:
            self.f.write(f"{now()}  {text}\n")
            if self.f.tell() > MAX_BYTES:
                self.f.close()
                for i in range(KEEP - 1, 0, -1):
                    if os.path.exists(f"{self.path}.{i}"):
                        os.replace(f"{self.path}.{i}", f"{self.path}.{i + 1}")
                os.replace(self.path, self.path + ".1")
                self.f = open(self.path, "a", buffering=1)


events = Log("events.log")
control = Log("control.log")
devmap = {}


def read(path):
    try:
        with open(path) as f:
            return f.read().strip()
    except OSError:
        return None


def scan():
    found = {}
    for d in glob.glob(SYS + "/bus/usb/devices/*"):
        v, p = read(d + "/idVendor"), read(d + "/idProduct")
        bus, dev = read(d + "/busnum"), read(d + "/devnum")
        if None in (v, p, bus, dev):
            continue
        found[(int(bus), int(dev))] = (os.path.basename(d), v, p, d)
    return found


def snapshot(d):
    state = {a: read(f"{d}/{a}") for a in ("power/control", "power/runtime_status", "authorized", "configuration")}
    for i in sorted(glob.glob(d + ":*")):
        drv = os.path.realpath(i + "/driver") if os.path.exists(i + "/driver") else "-"
        state[os.path.basename(i)] = os.path.basename(drv)
    real = os.path.realpath(d)
    state["hidraw"] = ",".join(sorted(os.path.basename(h) for h in glob.glob(SYS + "/class/hidraw/hidraw*")
                                        if os.path.realpath(h).startswith(real + "/")))
    return state


def watcher():
    last_state = {}
    known = {}
    while True:
        found = scan()
        for key, (name, v, p, d) in found.items():
            if key not in known:
                events.write(f"usb device appeared: {key[0]}:{key[1]:03d} {name} {v}:{p}")
        for key in list(known):
            if key not in found:
                events.write(f"usb device gone: {key[0]}:{key[1]:03d} {known[key][0]} {known[key][1]}:{known[key][2]}")
        known = found
        devmap.clear()
        devmap.update({k: v[1] for k, v in found.items()})
        for key, (name, v, p, d) in found.items():
            if v != VENDOR:
                continue
            st = snapshot(d)
            if last_state.get(key) != st:
                last_state[key] = st
                susp = read(d + "/power/runtime_suspended_time")
                events.write(f"{VENDOR}:{p} at {key[0]}:{key[1]:03d} state: "
                             + " ".join(f"{k}={v2}" for k, v2 in st.items()) + f" runtime_suspended_time={susp}")
        time.sleep(0.5)


class Runs:
    """Identical consecutive control transfers are written once with a count."""

    def __init__(self):
        self.lock = threading.Lock()
        self.sig = None
        self.n = 0
        self.first = self.last = None

    def add(self, sig, ts):
        with self.lock:
            if sig == self.sig:
                self.n += 1
                self.last = ts
                return
            self._flush()
            self.sig, self.n, self.first, self.last = sig, 1, ts, ts

    def flush(self):
        with self.lock:
            self._flush()

    def _flush(self):
        if self.sig is not None:
            addr, setup, status, length, data = self.sig
            control.write(f"{addr} s {setup} -> status {status} len {length}"
                          + (f" = {data}" if data else "") + (f"  x{self.n}" if self.n > 1 else "")
                          + f"  [usbmon {self.first}" + (f"..{self.last}" if self.n > 1 else "") + "]")
        self.sig = None
        self.n = 0


runs = Runs()


def usbmon():
    if not os.access(MON, os.R_OK):
        events.write(f"usbmon is not available ({MON}); only state and kernel messages are recorded")
        return
    pending = {}
    with open(MON) as f:
        for line in f:
            p = line.split()
            if len(p) < 5 or p[3][:3] not in ("Ci:", "Co:"):
                continue
            tag, ts, ev, addr = p[0], p[1], p[2], p[3]
            parts = addr.split(":")
            try:
                key = (int(parts[1]), int(parts[2]))
            except (IndexError, ValueError):
                continue
            if ev == "S":
                if p[4] == "s" and len(p) >= 10:
                    out = " ".join(p[p.index("=") + 1:]) if "=" in p else ""
                    pending[tag] = (" ".join(p[5:10]), out)
                if len(pending) > 4096:
                    pending.clear()
                continue
            if ev != "C":
                continue
            setup, out = pending.pop(tag, ("?", ""))
            status = p[4]
            length = p[5] if len(p) > 5 else "0"
            data = " ".join(p[p.index("=") + 1:]) if "=" in p else out
            ours = devmap.get(key) == VENDOR
            if not (ours or setup.startswith(GET_DESCRIPTOR)):
                data = ""
            runs.add((f"{parts[0]}:{key[0]}:{key[1]:03d}:{parts[3]}", setup, status, length, data[:96]), ts)


def flusher():
    while True:
        time.sleep(5)
        runs.flush()


def kernel_log():
    pattern = re.compile(r"usb|xhci|ehci|hid|snd|audio|10f5|turtle|suspend|resume|reset", re.I)
    try:
        proc = subprocess.Popen(["dmesg", "-w"], stdout=subprocess.PIPE, text=True, errors="replace")
    except OSError as e:
        events.write(f"dmesg could not be started: {e}")
        return
    for line in proc.stdout:
        if pattern.search(line):
            events.write("kernel: " + line.rstrip())


def stopping(signum, frame):
    runs.flush()
    try:
        jobs = subprocess.run(["systemctl", "list-jobs", "--no-legend"], capture_output=True, text=True, timeout=3).stdout
    except (OSError, subprocess.SubprocessError):
        jobs = ""
    shutting = re.search(r"(shutdown|reboot|poweroff|halt|kexec)", jobs)
    events.write("recorder stopping because of " + ("shutdown or reboot" if shutting else "a service stop")
                 + (f" (jobs: {' | '.join(j.strip() for j in jobs.splitlines()[:6])})" if shutting else ""))
    os._exit(0)


def main():
    boot = read("/proc/sys/kernel/random/boot_id")
    up = read("/proc/uptime")
    events.write(f"== recorder started; boot_id={boot} uptime={up.split()[0] if up else '?'}s kernel={os.uname().release}")
    events.write("cmdline: " + (read("/proc/cmdline") or "?"))
    events.write("usbcore autosuspend: " + str(read(SYS + "/module/usbcore/parameters/autosuspend")))
    signal.signal(signal.SIGTERM, stopping)
    signal.signal(signal.SIGINT, stopping)
    for target in (watcher, flusher, kernel_log):
        threading.Thread(target=target, daemon=True).start()
    usbmon()
    while True:
        time.sleep(3600)


main()
PYEOF
    chmod 755 "$LIB/daemon.py"
}

write_unit() {
    cat > "$UNIT" <<'EOF'
[Unit]
Description=Neap USB trace (passive: listens, sends nothing)
DefaultDependencies=no
After=local-fs.target systemd-modules-load.service
Before=sysinit.target shutdown.target
Conflicts=shutdown.target

[Service]
ExecStartPre=-/sbin/modprobe usbmon
ExecStartPre=-/bin/sh -c 'mountpoint -q /sys/kernel/debug || mount -t debugfs none /sys/kernel/debug'
ExecStart=/usr/bin/python3 /usr/local/lib/neap-usb-trace/daemon.py
KillSignal=SIGTERM
TimeoutStopSec=10
Nice=10

[Install]
WantedBy=sysinit.target
EOF
}

write_hook() {
    cat > "$HOOK" <<EOF
#!/bin/sh
# systemd calls this before sleeping and after waking.
echo "\$(date '+%Y-%m-%d %H:%M:%S.000')  sleep: \$1 (\$2)" >> $LOGDIR/events.log
EOF
    chmod 755 "$HOOK"
}

install_it() {
    need_root install
    command -v python3 > /dev/null || { say "python3 is needed and was not found."; exit 1; }
    mkdir -p "$LIB" "$(dirname "$UNIT")" "$(dirname "$HOOK")" "$(dirname "$BIN")" "$LOGDIR"
    write_daemon; write_unit; write_hook
    cp "$0" "$BIN" && chmod 755 "$BIN"
    if [ -z "$NO_SYSTEMCTL" ]; then
        systemctl daemon-reload
        systemctl enable --now neap-usb-trace.service || { say "The recorder could not be started."; exit 1; }
    fi
    say
    say "Installed and recording. Nothing else to do now."
    say "Use the PC as you normally do, including shutting it down and starting it. The next time"
    say "Neap says the headset is not connected after a start, run:   sudo neap-usb-trace collect"
}

# -- the report -----------------------------------------------------------------------------
find_device() {
    for d in "$SYS"/bus/usb/devices/*; do
        [ "$(cat "$d/idVendor" 2>/dev/null)" = "$VENDOR" ] && [ "$(cat "$d/idProduct" 2>/dev/null)" = "$PRODUCT" ] && { echo "$d"; return; }
    done
}

show_state() {
    echo "--- $1 ($(date +%T))"
    dev=$(find_device)
    if [ -z "$dev" ]; then echo "transmitter not found"; return 1; fi
    echo "device: $(basename "$dev")"
    for a in power/control power/runtime_status power/runtime_suspended_time power/autosuspend_delay_ms \
             authorized configuration speed; do
        printf '%-34s %s\n' "$a" "$(cat "$dev/$a" 2>/dev/null)"
    done
    for i in "$dev":*; do
        [ -e "$i" ] && printf '%-34s %s\n' "$(basename "$i") driver" "$(basename "$(readlink "$i/driver" 2>/dev/null)" 2>/dev/null)"
    done
}

probe() {
    echo "--- does it answer? $1 ($(date +%T))"
    echo "MARK probe: $1" >> "$LOGDIR/events.log"
    SYS="$SYS" VENDOR="$VENDOR" PRODUCT="$PRODUCT" python3 - <<'PY'
import os, glob, fcntl, time
sysroot = os.environ["SYS"]; vendor = os.environ["VENDOR"].upper().zfill(8); product = os.environ["PRODUCT"].upper().zfill(8)
def ioc(n, ln): return (3 << 30) | (ln << 16) | (ord('H') << 8) | n
frame = bytes.fromhex("06 0a 00 05 5a 06 00 31 99 01 00 00 00").ljust(62, b"\x00")
found = False
for node in sorted(glob.glob(sysroot + "/class/hidraw/hidraw*")):
    try:
        uevent = open(node + "/device/uevent").read().upper()
        desc = open(node + "/device/report_descriptor", "rb").read()
    except OSError:
        continue
    if "HID_ID=0003:" + vendor + ":" + product not in uevent or bytes([0x06, 0x13, 0xff]) not in desc:
        continue
    found = True
    dev = "/dev/" + os.path.basename(node)
    try:
        fd = os.open(dev, os.O_RDWR)
    except OSError as e:
        print(dev, "could not be opened:", e); continue
    def read(seconds, want=None):
        end = time.time() + seconds; n = empty = 0; frames = []
        while time.time() < end:
            b = bytearray(62); b[0] = 7
            try: fcntl.ioctl(fd, ioc(0x0A, 62), b)
            except OSError: time.sleep(0.05); continue
            n += 1
            if b[1] == 0: empty += 1
            else:
                h = bytes(b[:24]).hex(" "); frames.append(h)
                if want and h.startswith(want): return n, empty, frames, h
            time.sleep(0.004)
        return n, empty, frames, None
    n, empty, frames, _ = read(2)
    print(f"{dev}: listened 2 s with nothing sent: {n} reads, {empty} empty, {len(frames)} with something in them")
    for f in frames[:4]: print("   ", f)
    try: fcntl.ioctl(fd, ioc(0x0B, 62), bytearray(frame))
    except OSError as e: print("   the question could not be sent:", e); os.close(fd); continue
    n, empty, frames, reply = read(8, "07 07")
    print(f"{dev}: after the question: {n} reads, {empty} empty, {len(frames)} with something in them")
    print("   ANSWERS:", reply if reply else "NO ANSWER")
    os.close(fd)
if not found: print("no control node for the transmitter was found")
PY
}

wait_for() {   # wait_for present|absent seconds
    local end=$((SECONDS + $2))
    while [ $SECONDS -lt $end ]; do
        if [ "$1" = present ] && [ -n "$(find_device)" ]; then return 0; fi
        if [ "$1" = absent ] && [ -z "$(find_device)" ]; then return 0; fi
        sleep 0.2
    done
    return 1
}

replug_test() {
    echo; echo "== unplug and replug test"
    echo "MARK replug test begins" >> "$LOGDIR/events.log"
    echo ">>> Unplug the transmitter's USB cable now. Wait 3 seconds, then plug it back into the SAME port."
    if wait_for absent 120; then
        echo "unplugged at $(date +%T)"
        if wait_for present 120; then
            echo "plugged in at $(date +%T); recording for 45 seconds, please leave it alone ..."
            echo "MARK replug test: plugged back in" >> "$LOGDIR/events.log"
            sleep "${WAIT:-45}"
        else
            echo "It was not plugged back in within two minutes."
        fi
    else
        echo "It was not unplugged within two minutes."
    fi
    show_state "AFTER the replug"
    probe "after the replug"
    sleep "${WAIT2:-20}"
    probe "20 seconds later"
    echo "MARK replug test ends" >> "$LOGDIR/events.log"
}

collect() {
    need_root collect
    owner=${SUDO_USER:-root}
    home=$(getent passwd "$owner" | cut -d: -f6)
    stamp=$(date +%Y%m%d-%H%M%S)
    work=$(mktemp -d)
    pack="$work/neap-usb-trace-$stamp"
    mkdir -p "$pack"
    rep="$pack/report.txt"
    {
        echo "== system"
        uname -r
        grep -E '^PRETTY_NAME=' /etc/os-release 2>/dev/null
        echo "now: $(date)   up since: $(uptime -s 2>/dev/null)"
        echo "cmdline: $(cat /proc/cmdline)"
        echo "usbcore autosuspend: $(cat "$SYS/module/usbcore/parameters/autosuspend" 2>/dev/null)"
        echo "power tools running: $(ps -eo comm | grep -E -i 'tlp|powertop|power-profiles|tuned|auto-cpufreq|thermald' | sort -u | tr '\n' ' ')"
        echo "access rule: $(tail -n 1 "$ROOT/etc/udev/rules.d/70-neap.rules" 2>/dev/null)"
        echo "recorder: $(systemctl is-active neap-usb-trace.service 2>/dev/null)"
        echo
        echo "== the transmitter as it is now (before touching anything)"
        if [ -z "$(find_device)" ]; then
            echo "The transmitter is not plugged in."
        else
            show_state "NOW, as the PC left it"
            if pgrep -x -i 'neap.desktop|neap' > /dev/null; then
                echo
                echo ">>> Neap is running. Quit it (right-click its icon by the clock, then Quit); this carries on by itself."
                end=$((SECONDS + 180))
                while pgrep -x -i 'neap.desktop|neap' > /dev/null && [ $SECONDS -lt $end ]; do sleep 1; done
            fi
            if pgrep -x -i 'neap.desktop|neap' > /dev/null; then
                echo "Neap is still running, so the questions and the replug test are skipped; the recorder holds what Neap saw."
            else
                probe "now, before any replug"
                echo
                if ask "Run the unplug and replug test now? It takes about two minutes"; then
                    replug_test
                else
                    echo "(replug test skipped)"
                fi
            fi
        fi
    } 2>&1 | tee "$rep"
    cp -r "$LOGDIR" "$pack/recorder" 2>/dev/null
    { journalctl -k -b 0 --no-pager 2>/dev/null; echo "--- previous boot"; journalctl -k -b -1 --no-pager 2>/dev/null; } \
        | grep -i -E 'usb|xhci|hid|snd|power|suspend|resume|reset|turtle|---' | tail -n 4000 > "$pack/kernel-journal.txt"
    lsusb -v -d "$VENDOR:$PRODUCT" 2>/dev/null | grep -v iSerial > "$pack/lsusb.txt"
    [ -r "$home/.local/share/neap/app.log" ] && cp "$home/.local/share/neap/app.log" "$pack/neap-app.log"
    tar czf "$home/neap-usb-trace-$stamp.tar.gz" -C "$work" "neap-usb-trace-$stamp"
    chown "$owner" "$home/neap-usb-trace-$stamp.tar.gz" 2>/dev/null
    rm -rf "$work"
    echo
    echo "Done. Attach this file to the issue:  $home/neap-usb-trace-$stamp.tar.gz"
    echo "It holds the recorder's logs, this report, the kernel's USB lines and Neap's own log."
    echo "You can leave the recorder running for now; remove it with:  sudo neap-usb-trace uninstall"
}

uninstall_it() {
    need_root uninstall
    if [ -z "$NO_SYSTEMCTL" ]; then systemctl disable --now neap-usb-trace.service 2>/dev/null; fi
    rm -rf "$LIB" "$UNIT" "$HOOK" "$BIN" "$LOGDIR"
    [ -z "$NO_SYSTEMCTL" ] && systemctl daemon-reload
    say "Removed, including the logs."
}

case "$1" in
    install) install_it ;;
    collect) collect ;;
    uninstall) uninstall_it ;;
    status) systemctl status neap-usb-trace.service --no-pager 2>&1 | head -12; ls -la "$LOGDIR" ;;
    *) sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'; exit 1 ;;
esac
