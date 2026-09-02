#!/usr/bin/env bash
#
# Works out the version the packages carry, by reading Lego2STL.Core's own <Version> element -
# the single place a version is set. Nothing passes a version in: not a tag, not a workflow
# input. A build only ever carries what the code itself says it is.
#
#   ./version.sh                        -> reads src/Lego2STL.Core/Lego2STL.Core.csproj
#   ./version.sh path/to/Other.csproj   -> reads a different csproj, for testing this script
#
# Both the real workflow and the local one call this rather than each holding their own copy,
# so that running the local one actually says something about the real one.
#
# Prints what it worked out, and appends it to $GITHUB_OUTPUT when running under a workflow.

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
csproj="${1:-$here/../src/Lego2STL.Core/Lego2STL.Core.csproj}"

if [ ! -f "$csproj" ]; then
  echo "::error::'$csproj' does not exist." >&2
  exit 2
fi

number="$(grep -oE '<Version>[^<]+</Version>' "$csproj" | head -1 | sed -E 's#</?Version>##g')"

if [ -z "$number" ]; then
  echo "::error::'$csproj' has no <Version> element. Add one, e.g. <Version>1.2.0</Version>." >&2
  exit 1
fi

if ! printf '%s' "$number" | grep -qE '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.]+)?$'; then
  # ::error:: is what makes a workflow show this against the run rather than burying it in a
  # log; outside one it is merely a prefix, so the same line serves both.
  echo "::error::'$number', read from '$csproj', is not a version these packages can carry. Use 1.2.0, or 1.2.0-rc1." >&2
  exit 1
fi

# A hyphen is what marks a pre-release in semantic versioning, so one is announced as a
# pre-release rather than as a finished version.
if printf '%s' "$number" | grep -q -- '-'; then
  prerelease=true
else
  prerelease=false
fi

echo "number=$number"
echo "prerelease=$prerelease"

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  {
    echo "number=$number"
    echo "prerelease=$prerelease"
  } >> "$GITHUB_OUTPUT"
fi
