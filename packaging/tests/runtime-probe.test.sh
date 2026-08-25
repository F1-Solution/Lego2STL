#!/usr/bin/env bash
# The probe decides whether an installer downloads 36 MB or not, and gets it wrong in two
# expensive directions: a false negative downloads a runtime the machine already has, and a
# false positive installs programs that cannot start. Both directions are tested here against
# stubbed environments, because the real answer depends on the machine.
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../lib/runtime-probe.sh
. "$(dirname "$here")/lib/runtime-probe.sh"

failures=0
pass() { printf '  ok   %s\n' "$1"; }
fail() { printf '  FAIL %s\n       %s\n' "$1" "$2"; failures=$((failures + 1)); }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# A directory that looks like a .NET install root holding the given runtime versions.
make_root() {
  root="$work/$1"; shift
  for version in "$@"; do mkdir -p "$root/shared/Microsoft.NETCore.App/$version"; done
  printf '%s\n' "$root"
}

# A directory holding a fake `dotnet` that reports the given --list-runtimes output.
make_dotnet() {
  bin="$work/$1-bin"; mkdir -p "$bin"
  shift
  {
    printf '#!/bin/sh\n'
    printf 'if [ "$1" = "--list-runtimes" ]; then\n'
    for line in "$@"; do printf '  echo "%s"\n' "$line"; done
    printf 'fi\n'
  } > "$bin/dotnet"
  chmod +x "$bin/dotnet"
  printf '%s\n' "$bin"
}

# ---- a root holding .NET 10 is found -------------------------------------------------
ten="$(make_root ten 10.0.11)"
if out="$(RUNTIME_SEARCH_ROOTS="$ten" DOTNET_ROOT= PATH=/usr/bin:/bin runtime_find)"; then
  [ "$out" = "$ten" ] && pass "finds a root holding 10.0.11" \
    || fail "finds a root holding 10.0.11" "returned '$out'"
else
  fail "finds a root holding 10.0.11" "returned failure"
fi

# ---- a root holding only .NET 8 is not ------------------------------------------------
eight="$(make_root eight 8.0.29)"
if RUNTIME_SEARCH_ROOTS="$eight" DOTNET_ROOT= PATH=/usr/bin:/bin runtime_find >/dev/null; then
  fail "rejects a root holding only 8.0.29" "reported a runtime"
else
  pass "rejects a root holding only 8.0.29"
fi

# ---- side-by-side: 8 and 10 together count as found -----------------------------------
both="$(make_root both 8.0.29 10.0.8 10.0.11)"
if RUNTIME_SEARCH_ROOTS="$both" DOTNET_ROOT= PATH=/usr/bin:/bin runtime_find >/dev/null; then
  pass "accepts a root holding 8 and 10 side by side"
else
  fail "accepts a root holding 8 and 10 side by side" "reported nothing"
fi

# ---- an empty root is not a runtime ---------------------------------------------------
empty="$work/empty"; mkdir -p "$empty"
if RUNTIME_SEARCH_ROOTS="$empty" DOTNET_ROOT= PATH=/usr/bin:/bin runtime_find >/dev/null; then
  fail "rejects an empty root" "reported a runtime"
else
  pass "rejects an empty root"
fi

# ---- DOTNET_ROOT is honoured even when it is not in the search roots -------------------
if DOTNET_ROOT="$ten" RUNTIME_SEARCH_ROOTS="$empty" PATH=/usr/bin:/bin runtime_find >/dev/null; then
  pass "honours DOTNET_ROOT"
else
  fail "honours DOTNET_ROOT" "ignored it"
fi

# ---- dotnet on PATH reporting 10.x counts, with no matching directory at all -----------
tenbin="$(make_dotnet ten 'Microsoft.NETCore.App 10.0.11 [/opt/dotnet/shared/Microsoft.NETCore.App]')"
if DOTNET_ROOT= RUNTIME_SEARCH_ROOTS="$empty" PATH="$tenbin:/usr/bin:/bin" runtime_find >/dev/null; then
  pass "accepts dotnet on PATH reporting 10.0.11"
else
  fail "accepts dotnet on PATH reporting 10.0.11" "reported nothing"
fi

# ---- dotnet on PATH reporting only 8.x does not ----------------------------------------
eightbin="$(make_dotnet eight 'Microsoft.NETCore.App 8.0.29 [/opt/dotnet/shared/Microsoft.NETCore.App]')"
if DOTNET_ROOT= RUNTIME_SEARCH_ROOTS="$empty" PATH="$eightbin:/usr/bin:/bin" runtime_find >/dev/null; then
  fail "rejects dotnet on PATH reporting only 8.0.29" "reported a runtime"
else
  pass "rejects dotnet on PATH reporting only 8.0.29"
fi

# ---- ASP.NET 10 alone is not the runtime the programs need -----------------------------
aspbin="$(make_dotnet asp 'Microsoft.AspNetCore.App 10.0.11 [/opt/dotnet/shared/Microsoft.AspNetCore.App]')"
if DOTNET_ROOT= RUNTIME_SEARCH_ROOTS="$empty" PATH="$aspbin:/usr/bin:/bin" runtime_find >/dev/null; then
  fail "rejects an ASP.NET-only 10 install" "reported a runtime"
else
  pass "rejects an ASP.NET-only 10 install"
fi

# ---- nothing anywhere -----------------------------------------------------------------
if DOTNET_ROOT= RUNTIME_SEARCH_ROOTS="$empty" PATH=/usr/bin:/bin runtime_find >/dev/null; then
  fail "reports nothing when there is nothing" "reported a runtime"
else
  pass "reports nothing when there is nothing"
fi

# ---- hash verification ----------------------------------------------------------------
printf 'lego2stl' > "$work/payload"
digest="$(runtime_sha512 "$work/payload")"
if runtime_verify "$work/payload" "$digest" >/dev/null 2>&1; then
  pass "accepts a file whose digest matches"
else
  fail "accepts a file whose digest matches" "rejected it"
fi

printf 'lego2stl' > "$work/tampered"
if runtime_verify "$work/tampered" "$(printf 'f%.0s' $(seq 128))" >/dev/null 2>&1; then
  fail "rejects a file whose digest does not match" "accepted it"
elif [ -e "$work/tampered" ]; then
  fail "deletes a file whose digest does not match" "the file is still there"
else
  pass "rejects and deletes a file whose digest does not match"
fi

if [ "$failures" -ne 0 ]; then printf '\n%d check(s) failed\n' "$failures"; exit 1; fi
printf '\nall checks passed\n'
