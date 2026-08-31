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
  # The macOS package serves every Mac from one file, so the architecture is not a choice
  # there and the workflow says so. It is a choice on Linux, where there is no such thing.
  universal)
    if [ "$platform" != "macos" ]; then
      echo "universal is a macOS package; on Linux say x64 or arm64" >&2
      exit 2
    fi ;;
  *) echo "architecture must be x64, arm64, or universal on macOS" >&2; exit 2 ;;
esac

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$here")"

rid="linux-$arch"
[ "$platform" = "macos" ] && rid="osx-$arch"

staging="$root/artifacts/staging/$rid"
dist="$root/artifacts/dist"

step() { printf '\033[36m==> %s\033[0m\n' "$1"; }

rm -rf "$staging"
mkdir -p "$staging" "$dist"

# Publishing belongs inside each branch rather than above them: macOS needs two payloads and
# fuses them, and it refuses outright when this is not a Mac - which is worth saying before
# spending minutes on a build that cannot finish.

if [ "$platform" = "linux" ]; then
  # ---- Linux: a self-extracting installer, and a tarball anyone can unpack -------------

  step "Publishing for $rid"
  # Both programs into one folder, over one copy of the assemblies they share.
  payload="$staging/payload"
  "$here/lib/payload.sh" "$rid" "$version" "$payload"

  step "Gathering the tarball"
  tree="$staging/tree"
  rm -rf "$tree"
  mkdir -p "$tree"
  cp -R "$payload/." "$tree/"
  cp "$here/linux/lego2stl.desktop" "$tree/"
  cp "$root/src/Lego2STL.Gui/Assets/icon-256.png" "$tree/lego2stl.png"
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
  # ---- macOS: one package, for either kind of Mac --------------------------------------

  if [ "$(uname -s)" != "Darwin" ]; then
    echo
    echo "    This is not macOS. lipo, codesign, pkgbuild and productbuild all ship only with"
    echo "    macOS, so no package can be built here. The programs themselves cross-build"
    echo "    fine; it is turning them into something a Mac will open that cannot be done."
    echo "    Build on a Mac, or let the workflow's macos job do it."
    echo
    exit 1
  fi

  step "Publishing for both kinds of Mac"
  "$here/lib/payload.sh" osx-x64   "$version" "$staging/x64"
  "$here/lib/payload.sh" osx-arm64 "$version" "$staging/arm64"

  step "Fusing them into one"
  fused="$staging/universal"
  "$here/macos/fuse-universal.sh" "$staging/x64" "$staging/arm64" "$fused"

  step "Building the application bundle"
  app="$staging/Lego2STL.app"
  rm -rf "$app"
  mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
  sed -e "s/@VERSION@/$version/g" "$here/macos/Info.plist" > "$app/Contents/Info.plist"
  cp -R "$fused/." "$app/Contents/MacOS/"
  cp "$here/macos/icon.icns" "$app/Contents/Resources/"

  # The two keep the names they were built with. A bundle whose window program were called
  # "Lego2STL" could not also hold "lego2stl": a Mac disk is case-insensitive by default, so
  # the two would be one file and the bundle would quietly ship the same program twice.
  for program in Lego2STL.Gui lego2stl; do
    [ -f "$app/Contents/MacOS/$program" ] || { echo "missing from the bundle: $program" >&2; exit 1; }
  done

  # macOS refuses to open a bundle whose signature it cannot make sense of. An ad-hoc
  # signature is not a developer identity and does not avoid the warning on first open, but
  # it does stop the copied bundle being rejected outright on Apple silicon.
  codesign --force --deep --sign - "$app" || echo "    could not sign; the bundle is unsigned"

  step "Archiving the bundle"
  zipfile="$dist/Lego2STL-$version-osx-universal.zip"
  rm -f "$zipfile"
  # ditto rather than zip, because it is the one that keeps the permission bits and the
  # signature intact. A bundle zipped any other way can arrive unrunnable.
  ditto -c -k --keepParent "$app" "$zipfile"
  echo "    $zipfile"

  step "Building the installer"

  pin="$here/runtime.json"
  python="python3"; command -v python3 >/dev/null 2>&1 || python="python"
  read_pin() { "$python" -c "import json;p=json.load(open('$pin'));print($1)"; }
  runtime_version="$(read_pin "p['version']")"
  runtime_base="$(read_pin "p['urlBase']")"
  x64_file="$(read_pin "p['platforms']['osx-x64']['file']")"
  x64_sha="$(read_pin "p['platforms']['osx-x64']['sha512']")"
  arm_file="$(read_pin "p['platforms']['osx-arm64']['file']")"
  arm_sha="$(read_pin "p['platforms']['osx-arm64']['sha512']")"

  scripts="$staging/scripts"
  rm -rf "$scripts"
  mkdir -p "$scripts"
  awk -v probe="$here/lib/runtime-probe.sh" '
    /^@RUNTIME_PROBE@$/ { while ((getline line < probe) > 0) print line; next }
    { print }
  ' "$here/macos/preinstall" > "$scripts/preinstall.stage1"
  sed -e "s|@RUNTIME_VERSION@|$runtime_version|g" \
      -e "s|@RUNTIME_URL_X64@|$runtime_base/$runtime_version/$x64_file|g" \
      -e "s|@RUNTIME_SHA512_X64@|$x64_sha|g" \
      -e "s|@RUNTIME_URL_ARM64@|$runtime_base/$runtime_version/$arm_file|g" \
      -e "s|@RUNTIME_SHA512_ARM64@|$arm_sha|g" \
      "$scripts/preinstall.stage1" > "$scripts/preinstall"
  rm -f "$scripts/preinstall.stage1"
  chmod +x "$scripts/preinstall"

  packageroot="$staging/pkgroot"
  rm -rf "$packageroot"
  mkdir -p "$packageroot/Applications" "$packageroot/usr/local/bin"
  cp -R "$app" "$packageroot/Applications/"
  # So 'lego2stl' works in a terminal without anyone adding a folder to their path.
  ln -s "/Applications/Lego2STL.app/Contents/MacOS/lego2stl" "$packageroot/usr/local/bin/lego2stl"

  component="$staging/app.pkg"
  rm -f "$component"
  pkgbuild --root "$packageroot" \
           --scripts "$scripts" \
           --identifier org.lego2stl.app \
           --version "$version" \
           --install-location / \
           "$component"

  pkg="$dist/Lego2STL-$version-osx-universal.pkg"
  rm -f "$pkg"
  productbuild --distribution "$here/macos/distribution.xml" \
               --package-path "$staging" \
               --resources "$here/macos" \
               "$pkg"

  # stat -f%z is the macOS form; the Linux branch above uses -c%s. Nothing shared between
  # them, because there is no spelling that works on both.
  pkg_mb=$(($(stat -f%z "$pkg") / 1048576))
  printf '    %s  (%s MB)\n' "$pkg" "$pkg_mb"
  printf '    .NET %s, fetched only when missing\n' "$runtime_version"

  # A package that grew past this is carrying the runtime again. Higher than the other two
  # because a universal build doubles every native binary.
  if [ "$pkg_mb" -gt 70 ]; then
    echo "the package is ${pkg_mb} MB, over the 70 MB ceiling. Is it carrying the runtime?" >&2
    exit 1
  fi
fi

step "Done"
