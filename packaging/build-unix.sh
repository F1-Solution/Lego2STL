#!/usr/bin/env bash
#
# Builds the Linux or macOS package.
#
#   ./build-unix.sh linux  [x64|arm64] [version]
#   ./build-unix.sh macos  [x64|arm64] [version]
#
# Produces, into artifacts/dist:
#
#   linux   Lego2STL-<version>-linux-<arch>.tar.gz   the programs, a menu entry and an
#                                                    install script that puts them in place
#           lego2stl_<version>_<arch>.deb            for anything Debian-derived, when the
#                                                    packaging tool is available
#
#   macos   Lego2STL-<version>-osx-<arch>.dmg        a disk image to drag into Applications,
#                                                    when built on macOS
#           Lego2STL-<version>-osx-<arch>.zip        the same application, always produced
#
# What is inside is the same either way: one windowed program and one console program, each
# self-contained, so nothing has to be installed alongside them.
#
# Reading a document is Windows-only, because the text recogniser it uses is part of Windows.
# Everything after the parts list - shapes, plates, clearance, calibration - works here, and
# so does starting from a set number. The programs say so when asked to do the one thing they
# cannot.

set -euo pipefail

platform="${1:-}"
arch="${2:-x64}"
version="${3:-1.0.0}"

case "$platform" in
  linux|macos) ;;
  *) echo "usage: $0 {linux|macos} [x64|arm64] [version]" >&2; exit 2 ;;
esac

case "$arch" in
  x64|arm64) ;;
  *) echo "architecture must be x64 or arm64" >&2; exit 2 ;;
esac

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$here")"

rid="linux-$arch"
[ "$platform" = "macos" ] && rid="osx-$arch"

# The plain target framework: the Windows one exists only for the text recogniser.
framework="net10.0"

staging="$root/artifacts/staging/$rid"
dist="$root/artifacts/dist"

step() { printf '\033[36m==> %s\033[0m\n' "$1"; }

step "Publishing for $rid"
rm -rf "$staging"
mkdir -p "$staging/cli" "$staging/gui" "$dist"

# One folder per program. Publishing two into the same folder does not work: the second run
# clears what the first put there.
dotnet publish "$root/src/Lego2STL.Cli/Lego2STL.Cli.csproj" \
  -c Release -f "$framework" -r "$rid" -p:Version="$version" -o "$staging/cli" --nologo
dotnet publish "$root/src/Lego2STL.Gui/Lego2STL.Gui.csproj" \
  -c Release -f "$framework" -r "$rid" -p:Version="$version" -o "$staging/gui" --nologo

cli="$staging/cli/lego2stl"
gui="$staging/gui/Lego2STL.Gui"

for program in "$cli" "$gui"; do
  [ -f "$program" ] || { echo "missing: $program" >&2; exit 1; }
  chmod +x "$program"
done

if [ "$platform" = "linux" ]; then
  # ---- Linux: a tarball anyone can unpack, and a .deb when the tool is here --------------

  step "Building the tarball"
  tree="$staging/tree"
  rm -rf "$tree"
  mkdir -p "$tree/bin" "$tree/share/applications" "$tree/share/doc/lego2stl"

  cp "$cli" "$tree/bin/lego2stl"
  cp "$gui" "$tree/bin/lego2stl-gui"
  cp "$here/linux/lego2stl.desktop" "$tree/share/applications/"
  cp "$here/linux/install.sh" "$tree/install.sh"
  cp "$root/README.md" "$tree/share/doc/lego2stl/" 2>/dev/null || true
  chmod +x "$tree/install.sh"

  tarball="$dist/Lego2STL-$version-linux-$arch.tar.gz"
  rm -f "$tarball"
  tar -czf "$tarball" -C "$tree" .
  echo "    $tarball"

  if command -v dpkg-deb >/dev/null 2>&1; then
    step "Building the .deb"

    debarch="amd64"
    [ "$arch" = "arm64" ] && debarch="arm64"

    debroot="$staging/deb"
    rm -rf "$debroot"
    mkdir -p "$debroot/DEBIAN" "$debroot/usr/bin" "$debroot/usr/share/applications"

    cp "$cli" "$debroot/usr/bin/lego2stl"
    cp "$gui" "$debroot/usr/bin/lego2stl-gui"
    cp "$here/linux/lego2stl.desktop" "$debroot/usr/share/applications/"

    installed_kb=$(du -sk "$debroot" | cut -f1)

    cat > "$debroot/DEBIAN/control" <<EOF
Package: lego2stl
Version: $version
Section: graphics
Priority: optional
Architecture: $debarch
Installed-Size: $installed_kb
Maintainer: Lego2STL
Description: Turn a LEGO parts catalogue into printable shapes
 Reads a parts list, or a set number, and produces one shape file per part plus
 printing plates grouped by colour. Reading a parts catalogue out of a document
 needs Windows; everything after the parts list works here.
EOF

    deb="$dist/lego2stl_${version}_${debarch}.deb"
    rm -f "$deb"
    dpkg-deb --build --root-owner-group "$debroot" "$deb"
    echo "    $deb"
  else
    echo "    dpkg-deb was not found, so no .deb was built. The tarball has everything."
  fi

else
  # ---- macOS: an application bundle, zipped, and a disk image when on macOS ---------------

  step "Building the application bundle"
  app="$staging/Lego2STL.app"
  rm -rf "$app"
  mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"

  sed -e "s/@VERSION@/$version/g" "$here/macos/Info.plist" > "$app/Contents/Info.plist"

  # The two keep the names they were built with. A bundle whose window program were called
  # "Lego2STL" could not also hold "lego2stl": a Mac disk is case-insensitive by default, so
  # the two would be one file and the bundle would quietly ship the same program twice.
  cp "$gui" "$app/Contents/MacOS/Lego2STL.Gui"
  cp "$cli" "$app/Contents/MacOS/lego2stl"
  chmod +x "$app/Contents/MacOS/Lego2STL.Gui" "$app/Contents/MacOS/lego2stl"

  for program in Lego2STL.Gui lego2stl; do
    [ -f "$app/Contents/MacOS/$program" ] || { echo "missing from the bundle: $program" >&2; exit 1; }
  done

  # macOS refuses to open a bundle whose signature it cannot make sense of. An ad-hoc
  # signature is not a developer identity and does not avoid the warning on first open, but
  # it does stop the copied bundle being rejected outright on Apple silicon.
  if command -v codesign >/dev/null 2>&1; then
    codesign --force --deep --sign - "$app" || echo "    could not sign; the bundle is unsigned"
  fi

  zipfile="$dist/Lego2STL-$version-osx-$arch.zip"
  rm -f "$zipfile"
  if command -v ditto >/dev/null 2>&1; then
    # ditto rather than zip, because it is the one that keeps the permission bits and the
    # signature intact. A bundle zipped any other way can arrive unrunnable.
    ditto -c -k --keepParent "$app" "$zipfile"
    echo "    $zipfile"
  elif command -v zip >/dev/null 2>&1; then
    (cd "$staging" && zip -qry "$zipfile" "Lego2STL.app")
    echo "    $zipfile"
  else
    echo "    neither ditto nor zip was found, so the bundle was not archived."
    echo "    It is at $app"
  fi

  if command -v hdiutil >/dev/null 2>&1; then
    step "Building the disk image"

    image="$staging/dmg"
    rm -rf "$image"
    mkdir -p "$image"
    cp -R "$app" "$image/"
    ln -s /Applications "$image/Applications"

    dmg="$dist/Lego2STL-$version-osx-$arch.dmg"
    rm -f "$dmg"
    hdiutil create -volname "Lego2STL" -srcfolder "$image" -ov -format UDZO "$dmg" >/dev/null
    echo "    $dmg"
  else
    echo "    hdiutil was not found, so no disk image was built. Build on macOS for one."
  fi
fi

step "Done"
