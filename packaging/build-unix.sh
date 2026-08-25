#!/usr/bin/env bash
#
# Builds the Linux or macOS package.
#
#   ./build-unix.sh linux  [x64|arm64] [version]
#   ./build-unix.sh macos  [x64|arm64] [version]
#
# Produces, into artifacts/dist:
#
#   linux   Lego2STL-<version>-linux-<arch>.run      one file that installs everything: the
#                                                    programs, a menu entry, and .NET 10 when
#                                                    the machine has not got it
#           Lego2STL-<version>-linux-<arch>.tar.gz   the same contents for anyone who would
#                                                    rather unpack it themselves
#
#   macos   Lego2STL-<version>-osx-<arch>.dmg        a disk image to drag into Applications,
#                                                    when built on macOS
#           Lego2STL-<version>-osx-<arch>.zip        the same application, always produced
#
# What is inside is the same either way: one windowed program and one console program over one
# shared set of assemblies. They need .NET 10, which the installer fetches from a fixed address
# and checks against a fingerprint - but only when the machine has none, so most people
# download nothing.
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

staging="$root/artifacts/staging/$rid"
dist="$root/artifacts/dist"

step() { printf '\033[36m==> %s\033[0m\n' "$1"; }

step "Publishing for $rid"
rm -rf "$staging"
mkdir -p "$staging" "$dist"

# Both programs into one folder, over one copy of the assemblies they share.
payload="$staging/payload"
"$here/lib/payload.sh" "$rid" "$version" "$payload"

cli="$payload/lego2stl"
gui="$payload/Lego2STL.Gui"

if [ "$platform" = "linux" ]; then
  # ---- Linux: a self-extracting installer, and a tarball anyone can unpack -------------

  step "Gathering the tarball"
  tree="$staging/tree"
  rm -rf "$tree"
  mkdir -p "$tree"
  cp -R "$payload/." "$tree/"
  cp "$here/linux/lego2stl.desktop" "$tree/"
  cp "$here/linux/install.sh" "$tree/install.sh"
  cp "$here/lib/runtime-probe.sh" "$tree/runtime-probe.sh"
  cp "$root/README.md" "$tree/" 2>/dev/null || true
  chmod +x "$tree/install.sh"

  tarball="$dist/Lego2STL-$version-linux-$arch.tar.gz"
  rm -f "$tarball"
  tar -czf "$tarball" -C "$tree" .
  printf '    %s  (%s MB)\n' "$tarball" "$(($(stat -c%s "$tarball") / 1048576))"

  step "Building the installer"

  # The pin is read here, on a machine with python, and written into the installer. The
  # installer itself must run on any Linux, so it reads no JSON and trusts no tool it cannot
  # count on: the address and the fingerprint are simply part of the file.
  pin="$here/runtime.json"
  python="python3"; command -v python3 >/dev/null 2>&1 || python="python"

  # Said here rather than as a stack trace out of python, because the answer is to pin that
  # platform in runtime.json and there is no way to guess it.
  if ! "$python" -c "import json,sys;sys.exit(0 if 'linux-$arch' in json.load(open('$pin'))['platforms'] else 1)"; then
    echo "runtime.json has no pin for linux-$arch. Add one with packaging/refresh-runtime.sh." >&2
    exit 1
  fi

  runtime_version="$("$python" -c "import json;print(json.load(open('$pin'))['version'])")"
  runtime_file="$("$python" -c "import json;print(json.load(open('$pin'))['platforms']['linux-$arch']['file'])")"
  runtime_sha="$("$python" -c "import json;print(json.load(open('$pin'))['platforms']['linux-$arch']['sha512'])")"
  runtime_base="$("$python" -c "import json;print(json.load(open('$pin'))['urlBase'])")"
  runtime_url="$runtime_base/$runtime_version/$runtime_file"

  header="$staging/header.sh"

  # The probe goes in bodily rather than being fetched or sourced: there is nowhere to source
  # it from on the machine this ends up on.
  awk -v probe="$here/lib/runtime-probe.sh" '
    /^@RUNTIME_PROBE@$/ { while ((getline line < probe) > 0) print line; next }
    { print }
  ' "$here/linux/installer-header.sh" > "$header.stage1"

  sed -e "s|@VERSION@|$version|g" \
      -e "s|@RUNTIME_VERSION@|$runtime_version|g" \
      -e "s|@RUNTIME_URL@|$runtime_url|g" \
      -e "s|@RUNTIME_SHA512@|$runtime_sha|g" \
      "$header.stage1" > "$header"
  rm -f "$header.stage1"

  # The payload begins on the line after the marker. Substituting a number for the
  # placeholder cannot change how many lines the header has, which is what makes counting
  # first and substituting second safe.
  marker="$(grep -n '^__PAYLOAD_BELOW__$' "$header" | cut -d: -f1)"
  [ -n "$marker" ] || { echo "the header has lost its payload marker" >&2; exit 1; }
  sed -i.bak "s|@PAYLOAD_LINE@|$((marker + 1))|" "$header"
  rm -f "$header.bak"

  installer="$dist/Lego2STL-$version-linux-$arch.run"
  rm -f "$installer"
  cat "$header" > "$installer"
  tar -czf - -C "$tree" . >> "$installer"
  chmod +x "$installer"

  installer_mb=$(($(stat -c%s "$installer") / 1048576))
  printf '    %s  (%s MB)\n' "$installer" "$installer_mb"
  printf '    .NET %s, fetched only when missing\n' "$runtime_version"

  # An installer that grew past this is carrying the runtime again, which is the one thing
  # this packaging exists to stop.
  if [ "$installer_mb" -gt 40 ]; then
    echo "the installer is ${installer_mb} MB, over the 40 MB ceiling. Is it carrying the runtime?" >&2
    exit 1
  fi

else
  # ---- macOS: an application bundle, zipped, and a disk image when on macOS ---------------

  # The programs themselves cross-build from anywhere - dotnet emits a real Mach-O binary on
  # Windows or Linux quite happily. What cannot be done elsewhere is turn them into something
  # a Mac will open: the signature, the archive that keeps the permission bits, and the disk
  # image all come from tools that ship only with macOS. Said here rather than discovered
  # three notices later, because the folder left behind otherwise looks like a package.
  if [ "$(uname -s)" != "Darwin" ]; then
    echo
    echo "    Note: this is not macOS, so what follows will not be a package anyone can open."
    echo "    The binaries are real and the bundle's layout is right, but codesign, ditto and"
    echo "    hdiutil are macOS-only, and the executable bit does not survive most other"
    echo "    filesystems. Build on a Mac, or let the workflow's macos job do it."
    echo
  fi

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

  # Nothing to hand anyone means the run did not do what it was asked, whatever the notices
  # above said individually. Saying so with an exit code stops a build script upstream from
  # treating a loose folder as a released package.
  if ! ls "$dist"/Lego2STL-"$version"-osx-"$arch".* >/dev/null 2>&1; then
    echo >&2
    echo "no macOS package was produced: the bundle is at $app, but nothing archived it." >&2
    echo "Run this on macOS, or use the workflow's macos job." >&2
    exit 1
  fi
fi

step "Done"
