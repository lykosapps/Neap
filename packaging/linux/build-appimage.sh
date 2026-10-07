#!/bin/bash
# Builds Neap's AppImage from the app published for Linux.
#
#   build-appimage.sh <published folder> <version> [output folder]
#
# The published folder is `dotnet publish src/Neap.Desktop -c Release -r linux-x64 -f net10.0 --self-contained`.
# Needs appimagetool, found on the PATH or named in APPIMAGETOOL.
set -euo pipefail

publish=${1:?the folder the app was published to}
version=${2:?the version, such as 0.3.0}
out=${3:-.}

here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../.." && pwd)
tool=${APPIMAGETOOL:-appimagetool}

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
appdir="$work/Neap.AppDir"

mkdir -p "$appdir/usr/bin" "$appdir/usr/share/doc/neap"
cp -r "$publish"/. "$appdir/usr/bin/"

# The program is called Neap, so that is what the desktop's process list shows.
mv "$appdir/usr/bin/Neap.Desktop" "$appdir/usr/bin/Neap"

# What a person needs to read, and the rule that lets them use the headset.
cp "$here/70-neap.rules" "$root/LICENSE" "$root/THIRD-PARTY-NOTICES.txt" "$appdir/usr/share/doc/neap/"

install -m 755 "$here/AppRun" "$appdir/AppRun"
cp "$here/neap.desktop" "$here/neap.png" "$appdir/"
cp "$here/neap.png" "$appdir/.DirIcon"

mkdir -p "$out"
image="$out/Neap-$version-x86_64.AppImage"
ARCH=x86_64 "$tool" --appimage-extract-and-run "$appdir" "$image"
(cd "$out" && sha256sum "$(basename "$image")" > "$(basename "$image").sha256")
echo "built $image"
