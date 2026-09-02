#!/usr/bin/env bash
# Checks the runtime pin is well-formed. A hand-edited pin that has lost a hash, or whose
# file names no longer match its version, would otherwise fail much later - inside an
# installer on someone else's machine.
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
pin="$(dirname "$here")/runtime.json"
failures=0

facts="$(mktemp)"
trap 'rm -f "$facts"' EXIT

check() {
  if [ "$2" = "$3" ]; then
    printf '  ok   %s\n' "$1"
  else
    printf '  FAIL %s\n       expected: %s\n       actual:   %s\n' "$1" "$3" "$2"
    failures=$((failures + 1))
  fi
}

python="python3"
command -v python3 >/dev/null 2>&1 || python="python"

"$python" - "$pin" > "$facts" <<'PY'
import json, sys
pin = json.load(open(sys.argv[1]))
version = pin["version"]
print("version", version)
print("major", version.split(".")[0])
print("urlbase", pin["urlBase"])
print("platforms", ",".join(sorted(pin["platforms"])))
for name, p in sorted(pin["platforms"].items()):
    print("hashlen", name, len(p["sha512"]))
    print("hexonly", name, all(c in "0123456789abcdef" for c in p["sha512"]))
    print("hasversion", name, version in p["file"])
    print("size", name, p["size"] > 20_000_000)
PY

fact() { grep -E "^$1 " "$facts" | tail -1 | awk '{print $NF}'; }

check "pin parses and names a major version of 10" "$(fact major)" "10"
check "url base is the immutable builds host" \
  "$(fact urlbase)" "https://builds.dotnet.microsoft.com/dotnet/Runtime"
check "all four platforms are pinned" \
  "$(grep -E '^platforms ' "$facts" | awk '{print $2}')" \
  "linux-x64,osx-arm64,osx-x64,win-x64"

for platform in linux-x64 osx-arm64 osx-x64 win-x64; do
  check "$platform hash is 128 characters" \
    "$(grep -E "^hashlen $platform " "$facts" | awk '{print $NF}')" "128"
  check "$platform hash is lowercase hex" \
    "$(grep -E "^hexonly $platform " "$facts" | awk '{print $NF}')" "True"
  check "$platform file name carries the pinned version" \
    "$(grep -E "^hasversion $platform " "$facts" | awk '{print $NF}')" "True"
  check "$platform size is plausible for a runtime" \
    "$(grep -E "^size $platform " "$facts" | awk '{print $NF}')" "True"
done

if [ "$failures" -ne 0 ]; then printf '\n%d check(s) failed\n' "$failures"; exit 1; fi
printf '\nall checks passed\n'
