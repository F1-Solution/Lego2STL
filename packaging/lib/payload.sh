#!/usr/bin/env bash
#
#   payload.sh <rid> <version> <out-dir>
#
# Publishes both programs for one runtime and merges them into a single folder: two programs
# over one shared set of assemblies, which is the whole reason the packages got smaller.
#
# Each publishes on its own first. Publishing both into one folder does not work - the second
# run clears what the first put there - so they are gathered afterwards instead.
set -euo pipefail

rid="${1:?usage: payload.sh <rid> <version> <out-dir>}"
version="${2:?}"
out="${3:?}"

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$(dirname "$here")")"

# The plain target framework. The Windows one exists only for the text recogniser, and this
# script never builds for Windows - build-windows.ps1 does.
framework="net10.0"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

for project in Cli Gui; do
    dotnet publish "$root/src/Lego2STL.$project/Lego2STL.$project.csproj" \
        -c Release -f "$framework" -r "$rid" -p:Version="$version" \
        -o "$work/$project" --nologo
done

rm -rf "$out"
mkdir -p "$out"

# The console program first, then the window one over the top. Where both published the same
# assembly the two copies are identical, so which one lands is immaterial; what matters is
# that only one does.
cp -R "$work/Cli/." "$out/"
cp -R "$work/Gui/." "$out/"

# Debug databases are published alongside the assemblies and are of no use to anyone
# installing this. They are also enormous - the Skia one alone is 85 MB, more than the whole
# rest of the payload.
find "$out" -name '*.pdb' -delete

for program in lego2stl Lego2STL.Gui; do
    if [ ! -f "$out/$program" ]; then
        echo "missing from the payload: $program" >&2
        echo "Do the two programs share a name once case is ignored?" >&2
        exit 1
    fi
    chmod +x "$out/$program"
done

files="$(find "$out" -type f | wc -l | tr -d ' ')"
bytes="$(du -sk "$out" | cut -f1)"
printf '    %s: %s files, %s MB\n' "$rid" "$files" "$((bytes / 1024))"
