#!/usr/bin/env bash
#
# Regenerates packaging/runtime.json from Microsoft's own release metadata.
#
#   ./packaging/refresh-runtime.sh            the newest 10.0.x
#   ./packaging/refresh-runtime.sh 10.0.11    a particular one
#
# The metadata carries both the download URL and its SHA512, so bumping the runtime costs
# about a megabyte rather than the 140 MB the four installers weigh. Byte sizes come from a
# HEAD request, because Burn needs the size as well as the hash to fetch a remote payload.
#
# Bumping the runtime is a deliberate commit, never something a build does on its own: the
# whole point of a pinned hash is that nobody can change what an installer downloads.
set -euo pipefail

want="${1:-}"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
metadata="https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json"

python="python3"
command -v python3 >/dev/null 2>&1 || python="python"

echo "==> Fetching the release metadata"
tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT
curl -fsSL "$metadata" -o "$tmp"

echo "==> Working out the pin"
"$python" - "$tmp" "$want" > "$here/runtime.json" <<'PY'
import json, subprocess, sys

metadata, want = json.load(open(sys.argv[1])), sys.argv[2]

releases = [r for r in metadata["releases"] if "runtime" in r]
if want:
    releases = [r for r in releases if r["runtime"]["version"] == want]
    if not releases:
        sys.exit(f"no release in the 10.0 channel carries runtime {want}")
runtime = releases[0]["runtime"]
version = runtime["version"]

wanted = {
    "win-x64":   f"dotnet-runtime-{version}-win-x64.exe",
    "linux-x64": f"dotnet-runtime-{version}-linux-x64.tar.gz",
    "osx-x64":   f"dotnet-runtime-{version}-osx-x64.tar.gz",
    "osx-arm64": f"dotnet-runtime-{version}-osx-arm64.tar.gz",
}
by_name = {f["url"].rsplit("/", 1)[-1]: f for f in runtime["files"]}

url_base = "https://builds.dotnet.microsoft.com/dotnet/Runtime"
platforms = {}
for rid, filename in wanted.items():
    entry = by_name.get(filename)
    if entry is None:
        sys.exit(f"the metadata for {version} has no {filename}")
    url = f"{url_base}/{version}/{filename}"
    # Content-Length rather than downloading: Burn needs the exact byte count, and 37 MB per
    # platform to learn a number the server already states is a poor trade.
    stated = subprocess.run(
        ["curl", "-fsIL", "-o", "/dev/null", "-w", "%header{content-length}", url],
        capture_output=True, text=True, check=True,
    ).stdout.strip()
    if not stated.isdigit():
        sys.exit(f"{url} did not state its size (got {stated!r})")
    platforms[rid] = {
        "file": filename,
        "sha512": entry["hash"].lower(),
        "size": int(stated),
    }
    print(f"    {rid:10} {int(stated):>11,} bytes", file=sys.stderr)

json.dump(
    {"version": version, "urlBase": url_base, "platforms": platforms},
    sys.stdout, indent=2,
)
sys.stdout.write("\n")
PY

echo "==> Wrote $here/runtime.json"
