#!/usr/bin/env bash
#
#   fuse-universal.sh <x64-payload> <arm64-payload> <out>
#
# Turns two payloads into one that runs on any Mac. The assemblies are the bulk of it and are
# identical either way, so they are carried once; only the handful of genuinely native files -
# the two program launchers, Skia, PDFium, HarfBuzz - are doubled.
#
# A file that differs and is not a Mach-O binary was expected to be reproducible and was not:
# deps.json legitimately carries its RID in the runtime target name, and Gui.dll differs for a
# reason not yet root-caused (ContinuousIntegrationBuild=true, which fixes the usual embedded
# obj-path cause of this, did not fix it here). Rather than fail on each one as it is found,
# the Intel copy is kept and the difference is logged loudly.
#
# KNOWN LIMITATION: unverified on real Apple silicon hardware. If a shipped Mac build behaves
# oddly on Apple silicon, this fallback - not the fusing of the native libraries above, which
# is still checked - is the first place to look.
set -euo pipefail

x64="${1:?usage: fuse-universal.sh <x64> <arm64> <out>}"
arm="${2:?}"
out="${3:?}"

command -v lipo >/dev/null 2>&1 || { echo "lipo is needed and was not found" >&2; exit 1; }

rm -rf "$out"
mkdir -p "$out"

( cd "$x64" && find . -type f ) | while IFS= read -r relative; do
    left="$x64/$relative"
    right="$arm/$relative"
    target="$out/$relative"
    mkdir -p "$(dirname "$target")"

    if [ ! -f "$right" ]; then
        echo "    only in the Intel payload, carried as it is: $relative"
        cp "$left" "$target"
        continue
    fi

    if cmp -s "$left" "$right"; then
        cp "$left" "$target"
        continue
    fi

    if file "$left" | grep -q 'Mach-O'; then
        lipo -create "$left" "$right" -output "$target"
        architectures="$(lipo -archs "$target")"
        case "$architectures" in
            *x86_64*arm64*|*arm64*x86_64*)
                printf '    fused %-40s %s\n' "$relative" "$architectures" ;;
            *)
                echo "fusing $relative produced only '$architectures'" >&2
                exit 1 ;;
        esac
        continue
    fi

    echo "    KNOWN LIMITATION: $relative differs between the two builds and is not a program; keeping the Intel one (unverified on Apple silicon)" >&2
    cp "$left" "$target"
done

( cd "$arm" && find . -type f ) | while IFS= read -r relative; do
    if [ ! -f "$out/$relative" ]; then
        echo "    only in the Apple silicon payload, carried as it is: $relative"
        mkdir -p "$(dirname "$out/$relative")"
        cp "$arm/$relative" "$out/$relative"
    fi
done

for program in lego2stl Lego2STL.Gui; do
    [ -f "$out/$program" ] || { echo "missing from the fused payload: $program" >&2; exit 1; }
    chmod +x "$out/$program"
    architectures="$(lipo -archs "$out/$program")"
    case "$architectures" in
        *x86_64*arm64*|*arm64*x86_64*) ;;
        *) echo "$program is only $architectures; it would not run on every Mac" >&2; exit 1 ;;
    esac
done

printf '    fused payload: %s files, %s MB\n' \
    "$(find "$out" -type f | wc -l | tr -d ' ')" \
    "$(($(du -sk "$out" | cut -f1) / 1024))"
