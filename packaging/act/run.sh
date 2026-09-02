#!/usr/bin/env bash
#
# Runs the buildable part of the packaging workflow locally, in Docker, using act.
#
#   ./packaging/act/run.sh                 both jobs
#   ./packaging/act/run.sh linux           only the linux job
#   ./packaging/act/run.sh "" --dryrun
#
# Builds the Linux installer and tarball, installs what it built both as a machine that has
# .NET and as one that has none, and runs the packaging tests.
#
# Only the version and linux jobs run: act cannot run Windows or macOS containers, so test,
# windows, macos and release are out of reach locally. For the Windows one use
# packaging/local-windows.ps1 instead. See README-act.md.
#
# The version is never passed in here either - it comes from Lego2STL.Core's <Version>
# element, same as the real workflow. This script only prints it before building, so a
# stale csproj is obvious before ten minutes are spent on it.
#
# Needs Docker running with the Linux engine, and act on the path. The first run pulls about
# 1.1 GB of image and then downloads the .NET SDK inside it, so allow ten to fifteen minutes.

set -euo pipefail

job="${1:-}"
shift $(( $# > 0 ? 1 : 0 )) || true
extra=("$@")

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"
workflow="$here/local-package.yml"

step() { printf '\033[36m==> %s\033[0m\n' "$1"; }
problem() { printf '\033[31m!!  %s\033[0m\n' "$1" >&2; }

# ---- Check the things whose absence gives an unhelpful error later ------------------------

step 'Checking what is needed'

command -v act >/dev/null 2>&1 || {
  problem 'act is not on the path. Install it with:  winget install nektos.act'
  exit 1
}

command -v docker >/dev/null 2>&1 || {
  problem 'docker is not on the path. Install Docker Desktop.'
  exit 1
}

# docker info fails, rather than reporting something useful, when the engine is not up.
if ! ostype="$(docker info --format '{{.OSType}}' 2>/dev/null)"; then
  problem 'Docker is installed but not running. Start Docker Desktop and wait for it to settle.'
  exit 1
fi

if [ "$ostype" != "linux" ]; then
  problem "Docker is in $ostype container mode. act needs Linux containers."
  exit 1
fi

printf '    act    %s\n' "$(act --version)"
printf '    docker %s\n' "$(docker --version | sed 's/^Docker version //')"

# act 0.2.84 and earlier carry CVE-2026-34041 and CVE-2026-34042, which act itself reports on
# every run. Worth stopping for: this hands a container a copy of the repository.
minimum='0.2.86'
current="$(act --version | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)"

if [ -n "$current" ] && [ "$(printf '%s\n%s\n' "$minimum" "$current" | sort -V | head -1)" != "$minimum" ]; then
  problem "act $current is affected by CVE-2026-34041 and CVE-2026-34042."
  echo '    Upgrade first:  winget upgrade nektos.act'
  echo '    To run anyway:  SKIP_ACT_VERSION_CHECK=1 ...'
  if [ "${SKIP_ACT_VERSION_CHECK:-}" != "1" ]; then
    exit 1
  fi
  printf '\033[33m    Continuing anyway, as asked.\033[0m\n'
fi

# ---- Show the version the code carries, before spending ten minutes on it -----------------

if ! version="$("$root/packaging/version.sh" | sed -n 's/^number=//p')"; then
  problem 'Lego2STL.Core has no usable <Version>. See packaging/version.sh.'
  exit 1
fi

# ---- Warn if the two workflows have drifted apart -----------------------------------------

# They share their scripts, so the one thing that can quietly differ is the SDK they ask for.
sdks_in() { grep -oE "dotnet-version:[[:space:]]*'[^']+'" "$1" | grep -oE "'[^']+'" | tr -d "'" | sort -u; }

if ! diff <(sdks_in "$root/.github/workflows/package.yml") <(sdks_in "$workflow") >/dev/null; then
  printf '\033[33m    note: the two workflows ask for different .NET versions\033[0m\n'
fi

# ---- Run ------------------------------------------------------------------------------------

arguments=(
  workflow_dispatch
  -W "$workflow"
)

[ -n "$job" ] && arguments+=(-j "$job")
[ ${#extra[@]} -gt 0 ] && arguments+=("${extra[@]}")

step "Building at version $version, read from Lego2STL.Core"
step "Running act${job:+ (job: $job)}"
printf '\033[90m    act %s\033[0m\n\n' "${arguments[*]}"

cd "$root"

if act "${arguments[@]}"; then
  echo
  step 'Done'
  if [ -d "$root/.act-artifacts" ]; then
    echo '    The packages are under .act-artifacts:'
    find "$root/.act-artifacts" -type f -printf '      %10s bytes  %P\n' 2>/dev/null \
      || find "$root/.act-artifacts" -type f
  else
    echo '    No artifacts were kept, which is expected for a dry run or for the version job alone.'
  fi
else
  code=$?
  echo
  problem "act finished with exit code $code."
  echo '    A first failure is usually the image pull or the SDK download; run it again before reading too much into it.'
  exit $code
fi
