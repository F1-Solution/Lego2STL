#!/usr/bin/env bash
#
# Works out the version the packages carry.
#
#   ./version.sh v1.2.0      -> 1.2.0,      not a pre-release
#   ./version.sh v1.2.0-rc1  -> 1.2.0-rc1,  a pre-release
#
# A tag is written v1.2.0; a version is not. The leading letter is not part of the number, and
# dotnet refuses the whole string if it is left on, so a build that passes the tag straight
# through fails at its first publish step for a reason that is nowhere near the cause.
#
# Both the real workflow and the local one call this rather than each holding their own copy,
# so that running the local one actually says something about the real one.
#
# Prints what it worked out, and appends it to $GITHUB_OUTPUT when running under a workflow.

set -euo pipefail

raw="${1:-}"

if [ -z "$raw" ]; then
  echo "usage: $0 <tag-or-version>" >&2
  exit 2
fi

number="${raw#v}"

if ! printf '%s' "$number" | grep -qE '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.]+)?$'; then
  # ::error:: is what makes a workflow show this against the run rather than burying it in a
  # log; outside one it is merely a prefix, so the same line serves both.
  echo "::error::'$raw' is not a version these packages can carry. Tag it v1.2.0, or v1.2.0-rc1." >&2
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
