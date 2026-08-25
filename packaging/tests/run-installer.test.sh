#!/usr/bin/env bash
# Installs the built .run into a temporary prefix and checks the programs then run. Twice:
# once as a machine that already has .NET, and once as a machine that has none, because those
# are two entirely different paths through the installer and the second is the one nobody
# would otherwise exercise until a stranger ran it.
#
# The runtime-less case is real, not mocked: PATH is emptied of dotnet and the search roots
# are pointed at an empty directory, so the installer genuinely finds nothing, genuinely
# downloads 36 MB, and genuinely checks the digest.
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$(dirname "$here")")"

installer="$(ls "$root"/artifacts/dist/*linux-x64.run 2>/dev/null | head -1)"
if [ -z "$installer" ]; then
  echo "no .run in artifacts/dist - build one first:" >&2
  echo "  ./packaging/build-unix.sh linux x64 0.0.0-dev" >&2
  exit 1
fi

failures=0
pass() { printf '  ok   %s\n' "$1"; }
fail() { printf '  FAIL %s\n       %s\n' "$1" "$2"; failures=$((failures + 1)); }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# ---- a machine that already has .NET 10 ----------------------------------------------
prefix="$work/have"
if "$installer" --prefix "$prefix" > "$work/have.log" 2>&1; then
  pass "installs where .NET 10 is already present"
else
  fail "installs where .NET 10 is already present" "$(tail -5 "$work/have.log")"
fi

if [ -x "$prefix/bin/lego2stl" ]; then
  pass "the console program is installed and executable"
else
  fail "the console program is installed and executable" "not at $prefix/bin/lego2stl"
fi

if [ -f "$prefix/share/applications/lego2stl.desktop" ]; then
  pass "the menu entry is installed"
else
  fail "the menu entry is installed" "no .desktop file"
fi

if grep -q 'Downloading' "$work/have.log"; then
  fail "downloads nothing when .NET is already there" "$(grep 'Downloading' "$work/have.log")"
else
  pass "downloads nothing when .NET is already there"
fi

if "$prefix/bin/lego2stl" --help > "$work/help.txt" 2>&1; then
  pass "the installed program runs"
else
  fail "the installed program runs" "$(tail -5 "$work/help.txt")"
fi

# ---- a machine with no .NET at all ----------------------------------------------------
prefix="$work/bare"
empty="$work/no-dotnet"; mkdir -p "$empty"

if env -i HOME="$work/fakehome" PATH=/usr/bin:/bin \
     RUNTIME_SEARCH_ROOTS="$empty" \
     "$installer" --prefix "$prefix" > "$work/bare.log" 2>&1; then
  pass "installs on a machine with no .NET"
else
  fail "installs on a machine with no .NET" "$(tail -15 "$work/bare.log")"
fi

if grep -q 'Downloading .NET' "$work/bare.log"; then
  pass "fetches the runtime when there is none"
else
  fail "fetches the runtime when there is none" "no download in the log"
fi

if [ -d "$work/fakehome/.dotnet/shared/Microsoft.NETCore.App" ]; then
  pass "the runtime lands in the home directory, needing no root"
else
  fail "the runtime lands in the home directory, needing no root" "nothing under .dotnet"
fi

if env -i HOME="$work/fakehome" PATH=/usr/bin:/bin \
     "$prefix/bin/lego2stl" --help > "$work/bare-help.txt" 2>&1; then
  pass "the program runs against the runtime the installer fetched"
else
  fail "the program runs against the runtime the installer fetched" "$(tail -10 "$work/bare-help.txt")"
fi

# ---- refusing, rather than guessing ---------------------------------------------------
if env -i HOME="$work/fakehome2" PATH=/usr/bin:/bin \
     RUNTIME_SEARCH_ROOTS="$empty" \
     "$installer" --prefix "$work/refused" --no-runtime > "$work/refused.log" 2>&1; then
  fail "--no-runtime refuses when there is no runtime" "it installed anyway"
else
  pass "--no-runtime refuses when there is no runtime"
fi

# ---- uninstalling ---------------------------------------------------------------------
if "$installer" --prefix "$work/have" --uninstall > "$work/uninstall.log" 2>&1; then
  if [ -e "$work/have/bin/lego2stl" ]; then
    fail "--uninstall removes the programs" "the program is still there"
  else
    pass "--uninstall removes the programs"
  fi
else
  fail "--uninstall removes the programs" "$(tail -5 "$work/uninstall.log")"
fi

if [ "$failures" -ne 0 ]; then printf '\n%d check(s) failed\n' "$failures"; exit 1; fi
printf '\nall checks passed\n'
