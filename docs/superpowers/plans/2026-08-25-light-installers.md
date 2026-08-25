# Light Installers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the three self-contained packages with three light installers that install .NET 10 only when the machine lacks it, built for every release by CI and locally by act (Linux) and a native script (Windows).

**Architecture:** Both programs publish framework-dependent into one shared payload folder (67.4 MB, 24.4 MB compressed — measured). One pinned runtime version with an immutable URL and SHA512 lives in `packaging/runtime.json`, read only by build scripts, which substitute the values into the installers they produce. Windows gets a WiX Burn bundle that chains Microsoft's runtime installer as a remote payload; Linux gets a self-extracting `.run`; macOS gets one universal `.pkg`.

**Tech Stack:** .NET 10 SDK, WiX 6.0.1 (`UI`, `Netfx`, `Bal` extensions), POSIX sh, PowerShell 7, GitHub Actions, act ≥ 0.2.86, `lipo`/`pkgbuild`/`productbuild` on macOS.

**Spec:** `docs/superpowers/specs/2026-08-25-light-installers-design.md` — read it first. It records which assumptions were checked and which three turned out to be wrong; the plan does not repeat that evidence.

**Branch:** `feat/light-installers` (off `main`).

## Global Constraints

- **Runtime pin:** `10.0.11`. URL pattern `https://builds.dotnet.microsoft.com/dotnet/Runtime/<version>/<file>` — immutable, so a pinned SHA512 stays valid. Never an `aka.ms` link.
- **Never download and execute a script** at install time. No `dotnet-install.sh`. Every fetch is a pinned URL whose SHA512 is verified before use.
- **Detection never reads the registry.** Windows asks hostfxr via `netfx:DotNetCoreSearch`; Unix uses `dotnet --list-runtimes` and the known install directories.
- **No trimming, no single-file, no self-contained.** `PublishTrimmed=false` stays (Avalonia loads XAML by reflection).
- **Size ceilings, asserted by the build:** Windows bundle ≤ 40 MB, Linux `.run` ≤ 40 MB, macOS `.pkg` ≤ 70 MB.
- **The two program names must differ by more than capitalisation** — `lego2stl` and `Lego2STL.Gui`. Windows and default macOS filesystems are case-insensitive, so `Lego2STL.exe` beside `lego2stl.exe` is one file. Every build asserts both are present.
- **Tests stay green:** on this branch, 336 in `Lego2STL.Tests` + 26 in `Lego2STL.UiTests` = **362**. `dotnet test --configuration Release --nologo`. (381 is the count on `fix/windows-recogniser-and-plates`, which carries mesh-repair tests this branch does not; measured at 362 here before any packaging change, so 362 is the baseline to hold.)
- **Comments and CHANGELOG entries: one sentence each.** Test comments are exempt.
- **Commit messages** follow `<type>: <description>`, describe only behaviour a user can observe or the public interface, and **never name internal classes, methods, members, or files**. End every commit with:
  ```
  Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01HK4TucGA7iSj3CE8BftDZA
  ```
- **PROGRESS.md protocol:** read it before each task; immediately after each task append one line `PHASE:INST-<n> WAVE:0 STATUS:<complete|failed> TS:<ISO-8601-UTC>`. Never re-run a task already marked complete.

---

## File Structure

**Created**

| File | Responsibility |
|---|---|
| `packaging/runtime.json` | The pin: runtime version, and per-platform file name, SHA512, byte size. Read only by build scripts. |
| `packaging/refresh-runtime.sh` | Regenerates `runtime.json` from Microsoft's release metadata. Run by hand to bump the runtime. |
| `packaging/lib/runtime-probe.sh` | POSIX-sh fragment: find an existing .NET 10, verify a SHA512. Concatenated into the `.run` header and the macOS `preinstall`; sourced directly by its tests. |
| `packaging/lib/payload.sh` | Publishes both programs for one RID framework-dependent and merges them into one payload folder, asserting both are present. |
| `packaging/tests/runtime-probe.test.sh` | Tests the probe against stubbed environments. |
| `packaging/tests/runtime-pin.test.sh` | Tests `runtime.json` is well-formed and internally consistent. |
| `packaging/tests/run-installer.test.sh` | Tests the built `.run` end to end with no runtime available. |
| `packaging/linux/installer-header.sh` | The `.run` script: argument parsing, probe, runtime acquisition, install, uninstall. |
| `packaging/windows/Bundle.wxs` | The Burn bundle: runtime `ExePackage` + the application MSI. |
| `packaging/macos/fuse-universal.sh` | Fuses the two published RIDs into one universal payload, asserting every Mach-O carries both architectures. |
| `packaging/macos/preinstall` | Installs the runtime into `/usr/local/share/dotnet` when missing. |
| `packaging/macos/distribution.xml` | `productbuild` distribution: title, licence, install location. |
| `packaging/local-windows.ps1` | Mirrors the workflow's `windows` job natively, then inspects the bundle. |

**Modified**

| File | Change |
|---|---|
| `Directory.Build.props` | The publish property group: framework-dependent, no single file. |
| `packaging/build-windows.ps1` | Framework-dependent payload, `Files`-harvested MSI, Burn bundle, size ceiling. |
| `packaging/build-unix.sh` | Framework-dependent payload, `.run` assembly, universal `.pkg`, size ceilings; `.deb` and `.dmg` removed. |
| `packaging/windows/Lego2STL.wxs` | Two hand-written `File` elements become one `Files` harvest; `PATH` entry moves to its own component. |
| `packaging/linux/install.sh` | Gains the same runtime probe as the `.run`. |
| `.github/workflows/package.yml` | Three package jobs updated; macOS matrix collapses to one job; release notes mention the runtime. |
| `packaging/act/local-package.yml` | Verifies the `.run` by installing it with no runtime present. |
| `README.md`, `packaging/README.md`, `README-act.md` | Documentation. |
| `PROGRESS.md` | One line per task. |

**Deleted:** nothing. The `.deb` and `.dmg` disappear because no script builds them any more.

---

## Task 1: The runtime pin

**Files:**
- Create: `packaging/runtime.json`
- Create: `packaging/refresh-runtime.sh`
- Test: `packaging/tests/runtime-pin.test.sh`

**Interfaces:**
- Consumes: nothing.
- Produces: `packaging/runtime.json` with exactly this shape. Every later task reads it:
  ```json
  {
    "version": "10.0.11",
    "urlBase": "https://builds.dotnet.microsoft.com/dotnet/Runtime",
    "platforms": {
      "win-x64":   { "file": "dotnet-runtime-10.0.11-win-x64.exe",     "sha512": "<128 hex>", "size": 30655152 },
      "linux-x64": { "file": "dotnet-runtime-10.0.11-linux-x64.tar.gz", "sha512": "<128 hex>", "size": 36651444 },
      "osx-x64":   { "file": "dotnet-runtime-10.0.11-osx-x64.tar.gz",   "sha512": "<128 hex>", "size": 35584453 },
      "osx-arm64": { "file": "dotnet-runtime-10.0.11-osx-arm64.tar.gz", "sha512": "<128 hex>", "size": 33247018 }
    }
  }
  ```
  A download URL is `<urlBase>/<version>/<file>`. Sizes above are from the 10.0.10 release and will differ for 10.0.11 — the script writes the real ones.

- [ ] **Step 1: Write the failing test**

Create `packaging/tests/runtime-pin.test.sh`:

```sh
#!/usr/bin/env bash
# Checks the runtime pin is well-formed. A hand-edited pin that has lost a hash, or whose
# file names no longer match its version, would otherwise fail much later - inside an
# installer on someone else's machine.
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
pin="$(dirname "$here")/runtime.json"
failures=0

check() {
  if [ "$2" = "$3" ]; then
    printf '  ok   %s\n' "$1"
  else
    printf '  FAIL %s\n       expected: %s\n       actual:   %s\n' "$1" "$3" "$2"
    failures=$((failures + 1))
  fi
}

python3 - "$pin" <<'PY' > /tmp/pin-facts.txt
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

fact() { grep -E "^$1 " /tmp/pin-facts.txt | tail -1 | awk '{print $NF}'; }

check "pin parses and names a major version of 10" "$(fact major)" "10"
check "url base is the immutable builds host" \
  "$(fact urlbase)" "https://builds.dotnet.microsoft.com/dotnet/Runtime"
check "all four platforms are pinned" \
  "$(grep -E '^platforms ' /tmp/pin-facts.txt | awk '{print $2}')" \
  "linux-x64,osx-arm64,osx-x64,win-x64"

for platform in linux-x64 osx-arm64 osx-x64 win-x64; do
  check "$platform hash is 128 characters" \
    "$(grep -E "^hashlen $platform " /tmp/pin-facts.txt | awk '{print $NF}')" "128"
  check "$platform hash is lowercase hex" \
    "$(grep -E "^hexonly $platform " /tmp/pin-facts.txt | awk '{print $NF}')" "True"
  check "$platform file name carries the pinned version" \
    "$(grep -E "^hasversion $platform " /tmp/pin-facts.txt | awk '{print $NF}')" "True"
  check "$platform size is plausible for a runtime" \
    "$(grep -E "^size $platform " /tmp/pin-facts.txt | awk '{print $NF}')" "True"
done

rm -f /tmp/pin-facts.txt
if [ "$failures" -ne 0 ]; then printf '\n%d check(s) failed\n' "$failures"; exit 1; fi
printf '\nall checks passed\n'
```

- [ ] **Step 2: Run it to make sure it fails**

```bash
chmod +x packaging/tests/runtime-pin.test.sh
./packaging/tests/runtime-pin.test.sh
```

Expected: fails — `packaging/runtime.json` does not exist, so `python3` raises `FileNotFoundError`.

- [ ] **Step 3: Write the generator**

Create `packaging/refresh-runtime.sh`:

```sh
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
    size = subprocess.run(
        ["curl", "-fsIL", "-o", "/dev/null", "-w", "%{header_json}", url],
        capture_output=True, text=True, check=True,
    ).stdout
    length = int(json.loads(size)["content-length"][-1])
    platforms[rid] = {
        "file": filename,
        "sha512": entry["hash"].lower(),
        "size": length,
    }
    print(f"    {rid:10} {length:>11,} bytes", file=sys.stderr)

json.dump(
    {"version": version, "urlBase": url_base, "platforms": platforms},
    sys.stdout, indent=2,
)
sys.stdout.write("\n")
PY

echo "==> Wrote $here/runtime.json"
```

- [ ] **Step 4: Generate the pin and run the test**

```bash
chmod +x packaging/refresh-runtime.sh
./packaging/refresh-runtime.sh
./packaging/tests/runtime-pin.test.sh
```

Expected: the generator prints four byte counts, and every check passes. If the newest 10.0.x is not 10.0.11, that is fine — record whatever it wrote in the commit message and update the Global Constraints line in this plan.

- [ ] **Step 5: Commit**

```bash
git add packaging/runtime.json packaging/refresh-runtime.sh packaging/tests/runtime-pin.test.sh
git commit -m "build: pin the .NET runtime the installers will fetch"
```

- [ ] **Step 6: Record progress**

Append to `PROGRESS.md`: `PHASE:INST-1 WAVE:0 STATUS:complete TS:<now>`

---

## Task 2: The runtime probe

**Files:**
- Create: `packaging/lib/runtime-probe.sh`
- Test: `packaging/tests/runtime-probe.test.sh`

**Interfaces:**
- Consumes: nothing.
- Produces: a POSIX-sh fragment defining exactly these, for the `.run` header, the macOS `preinstall`, `install.sh` and the tests:
  - `runtime_search_roots()` → prints the candidate roots, one per line. Honours `RUNTIME_SEARCH_ROOTS` (colon-separated) when set, so a test can point it at an empty directory.
  - `runtime_find()` → prints the root holding a .NET 10 runtime and returns 0; prints nothing and returns 1 when there is none.
  - `runtime_sha512 <file>` → prints the lowercase hex digest.
  - `runtime_verify <file> <expected>` → returns 0 on match; on mismatch prints the two digests, deletes the file, returns 1.

The fragment defines functions only and must never run anything when sourced.

- [ ] **Step 1: Write the failing test**

Create `packaging/tests/runtime-probe.test.sh`:

```sh
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
```

- [ ] **Step 2: Run it to make sure it fails**

```bash
chmod +x packaging/tests/runtime-probe.test.sh
./packaging/tests/runtime-probe.test.sh
```

Expected: fails at the `.` source line — `packaging/lib/runtime-probe.sh` does not exist.

- [ ] **Step 3: Write the probe**

Create `packaging/lib/runtime-probe.sh`:

```sh
# Finding an existing .NET 10, and checking a download.
#
# Sourced by the Linux .run installer, by install.sh inside the tarball, and by the macOS
# preinstall script. Defines functions and does nothing else, so sourcing it is free.
#
# The registry-style shortcuts do not work here and are not attempted: patches of the runtime
# install side by side, so the question is never "which version" but "is any 10.0.x present".

# Where a .NET install root might be. RUNTIME_SEARCH_ROOTS overrides the list, which is what
# lets the tests ask about a machine they can control.
runtime_search_roots() {
    if [ -n "${RUNTIME_SEARCH_ROOTS:-}" ]; then
        printf '%s\n' "$RUNTIME_SEARCH_ROOTS" | tr ':' '\n'
        return 0
    fi
    printf '%s\n' \
        "$HOME/.dotnet" \
        "/usr/share/dotnet" \
        "/usr/lib/dotnet" \
        "/usr/local/share/dotnet"
}

# Does this root hold a 10.x runtime? A directory named 10.something under the shared
# framework is the only thing that matters; its patch number never does, because the app asks
# for 10.0.0 and the host picks the newest 10.0.x it finds.
runtime_root_has_ten() {
    _root="$1"
    [ -n "$_root" ] || return 1
    [ -d "$_root/shared/Microsoft.NETCore.App" ] || return 1
    for _candidate in "$_root"/shared/Microsoft.NETCore.App/10.*; do
        [ -d "$_candidate" ] && return 0
    done
    return 1
}

# Prints the root holding a .NET 10 runtime, or nothing.
runtime_find() {
    if runtime_root_has_ten "${DOTNET_ROOT:-}"; then
        printf '%s\n' "$DOTNET_ROOT"
        return 0
    fi

    runtime_search_roots | while IFS= read -r _root; do
        if runtime_root_has_ten "$_root"; then
            printf '%s\n' "$_root"
            exit 0
        fi
    done | {
        IFS= read -r _found || true
        if [ -n "${_found:-}" ]; then
            printf '%s\n' "$_found"
            return 0
        fi
        return 1
    } && return 0

    # Nothing where one is normally kept. A dotnet on the path may still know better - a
    # distribution package, or an install somewhere of the user's choosing. Asking it is the
    # authoritative answer, so it is asked last rather than not at all.
    if command -v dotnet >/dev/null 2>&1; then
        if dotnet --list-runtimes 2>/dev/null | grep -q '^Microsoft\.NETCore\.App 10\.'; then
            _exe="$(command -v dotnet)"
            if command -v readlink >/dev/null 2>&1; then
                _resolved="$(readlink -f "$_exe" 2>/dev/null || printf '%s' "$_exe")"
            else
                _resolved="$_exe"
            fi
            printf '%s\n' "$(dirname "$_resolved")"
            return 0
        fi
    fi

    return 1
}

# The digest of a file, whichever tool this system carries. Linux has sha512sum; macOS has
# shasum and not the other.
runtime_sha512() {
    if command -v sha512sum >/dev/null 2>&1; then
        sha512sum "$1" | cut -d' ' -f1
    elif command -v shasum >/dev/null 2>&1; then
        shasum -a 512 "$1" | cut -d' ' -f1
    else
        echo "no sha512 tool found (looked for sha512sum and shasum)" >&2
        return 1
    fi
}

# Checks a download against the digest built into this installer. A mismatch means the file
# is not the one this installer was built to trust, so it is deleted rather than left to be
# unpacked by anything else.
runtime_verify() {
    _file="$1"
    _expected="$(printf '%s' "$2" | tr 'A-Z' 'a-z')"
    _actual="$(runtime_sha512 "$_file" | tr 'A-Z' 'a-z')" || return 1

    if [ "$_actual" = "$_expected" ]; then
        return 0
    fi

    echo "the download does not match what this installer expects:" >&2
    echo "  expected $_expected" >&2
    echo "  actual   $_actual" >&2
    rm -f "$_file"
    return 1
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
./packaging/tests/runtime-probe.test.sh
```

Expected: PASS, 13 checks. The `runtime_find` subshell plumbing is the part most likely to
misbehave — a `while` loop in a pipeline runs in a subshell, which is why the result comes
back through the pipe rather than a variable. If any "finds a root" check fails, that is
where to look.

- [ ] **Step 5: Commit**

```bash
git add packaging/lib/runtime-probe.sh packaging/tests/runtime-probe.test.sh
git commit -m "build: find an existing .NET 10 rather than assuming one"
```

- [ ] **Step 6: Record progress**

Append `PHASE:INST-2 WAVE:0 STATUS:complete TS:<now>` to `PROGRESS.md`.

---

## Task 3: Framework-dependent payload

**Files:**
- Modify: `Directory.Build.props:22-33` (the `RuntimeIdentifier != ''` property group)
- Create: `packaging/lib/payload.sh`

**Interfaces:**
- Consumes: nothing.
- Produces: `payload.sh <rid> <version> <out-dir>` — publishes both projects framework-dependent
  for `<rid>` and merges them into `<out-dir>`, which afterwards holds both programs and one
  shared set of assemblies. Exits non-zero if either program is missing. Used by
  `build-unix.sh`; `build-windows.ps1` does the same thing in PowerShell.

- [ ] **Step 1: Change the publish settings**

In `Directory.Build.props`, replace the whole `One self-contained file per platform` property group with:

```xml
  <!--
    Publish settings apply only once a runtime has been named, so that a plain build stays a
    plain build and 'dotnet publish -r <rid>' is what produces something installable.
    Naming a runtime here instead would fix every build to Windows.
  -->
  <PropertyGroup Condition="'$(RuntimeIdentifier)' != ''" Label="Framework-dependent: .NET is installed, not carried">
    <!--
      The runtime is a dependency the installers put in place, not a passenger in every
      program. Carrying it made the Windows installer 152 MB, and carried it twice because
      there are two programs.
    -->
    <SelfContained>false</SelfContained>
    <!--
      Not one file per program, deliberately. A framework-dependent single file cannot be
      compressed - the SDK refuses it, NETSDK1176 - and came out at 66.9 MB against 57 MB for
      the compressed self-contained one it replaced, while duplicating some 60 MB of shared
      assemblies across the two programs. One shared folder is 67.4 MB for both, 24.4 MB
      packed.
    -->
    <PublishSingleFile>false</PublishSingleFile>
    <!-- Avalonia loads XAML by reflection; trimming breaks it. Not trimmed anywhere. -->
    <PublishTrimmed>false</PublishTrimmed>
  </PropertyGroup>
```

- [ ] **Step 2: Check the change did what it says**

```bash
dotnet publish src/Lego2STL.Cli/Lego2STL.Cli.csproj -c Release -f net10.0 -r linux-x64 -o /tmp/probe --nologo
ls /tmp/probe/lego2stl /tmp/probe/lego2stl.dll /tmp/probe/lego2stl.runtimeconfig.json
grep -c 'includedFrameworks' /tmp/probe/lego2stl.runtimeconfig.json || echo "no includedFrameworks - framework-dependent, as intended"
```

Expected: all three files exist, and `includedFrameworks` is **absent** — its presence is what
marks a self-contained publish.

- [ ] **Step 3: Write the payload builder**

Create `packaging/lib/payload.sh`:

```sh
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
```

- [ ] **Step 4: Run it and check both programs are there**

```bash
chmod +x packaging/lib/payload.sh
./packaging/lib/payload.sh linux-x64 0.0.0-dev /tmp/payload-linux
ls -l /tmp/payload-linux/lego2stl /tmp/payload-linux/Lego2STL.Gui
find /tmp/payload-linux -name '*.pdb' | wc -l
```

Expected: both programs present and executable, zero `.pdb` files, and the printed size
noticeably under the 44 MB the Windows payload measured (Linux carries no
`Microsoft.Windows.SDK.NET.dll`). **Record the real number** — the spec's Linux figure is an
estimate and should be replaced with it.

- [ ] **Step 5: Confirm the test suite is unaffected**

```bash
dotnet test --configuration Release --nologo
```

Expected: 354 + 27 = 381 passing. Publish settings do not touch a plain build, so a failure
here means the property group is missing its `RuntimeIdentifier` condition.

- [ ] **Step 6: Commit**

```bash
git add Directory.Build.props packaging/lib/payload.sh
git commit -m "build: programs share one copy of their dependencies"
```

- [ ] **Step 7: Record progress**

Append `PHASE:INST-3 WAVE:0 STATUS:complete TS:<now>` to `PROGRESS.md`.

---

## Task 4: Windows gate — prove the bundle behaves

**This task decides whether the Windows design survives.** It builds the smallest bundle that
can answer two questions, and nothing is built on top until both are answered. If the second
answer is no, **stop and report** — do not quietly accept a UAC prompt on every install.

**Files:**
- Create: `packaging/windows/Bundle.wxs`

**Interfaces:**
- Consumes: `packaging/runtime.json` (Task 1).
- Produces: `Bundle.wxs`, taking preprocessor variables `Version`, `RuntimeVersion`,
  `RuntimeUrl`, `RuntimeSha512`, `RuntimeSize`, `MsiPath`, `LicenseRtf`.

- [ ] **Step 1: Install the toolset and confirm the extensions exist**

```powershell
dotnet tool install --global wix --version 6.0.1
wix extension add --global WixToolset.UI.wixext/6.0.1
wix extension add --global WixToolset.Netfx.wixext/6.0.1
wix extension add --global WixToolset.Bal.wixext/6.0.1
wix extension list --global
```

Expected: three extensions listed. If `WixToolset.Netfx.wixext` cannot be added at 6.0.1,
**stop** — `netfx:DotNetCoreSearch` is the only reliable detector, and the alternatives were
measured to be wrong.

- [ ] **Step 2: Write the bundle**

Create `packaging/windows/Bundle.wxs`:

```xml
<?xml version="1.0" encoding="utf-8"?>

<!--
  The Windows installer.

  Two things happen, in this order: .NET 10 is installed if the machine has not got it, and
  then Lego2STL is. The runtime is fetched at that moment rather than carried, which is what
  keeps this file around 25 MB instead of 152 MB.

  Nothing here elevates when the runtime is already present, which is the common case for
  everybody after their first install: the application installs for one user, under their own
  profile, and needs no administrator. When the runtime is missing, Microsoft's own installer
  asks for administrator itself, at the moment it runs.
-->

<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:bal="http://wixtoolset.org/schemas/v4/wxs/bal"
     xmlns:netfx="http://wixtoolset.org/schemas/v4/wxs/netfx">

  <?ifndef Version?><?define Version="1.0.0"?><?endif?>

  <Bundle Name="Lego2STL"
          Version="$(Version)"
          Manufacturer="Lego2STL"
          UpgradeCode="A7E1D9C4-3B62-4F08-9A15-6E2D8C7B4013"
          IconSourceFile="$(MsiPath)"
          Compressed="yes">

    <BootstrapperApplication>
      <bal:WixStandardBootstrapperApplication
          Theme="hyperlinkLicense"
          LicenseFile="$(LicenseRtf)"
          SuppressOptionsUI="yes" />
    </BootstrapperApplication>

    <!--
      Asks the host resolver what runtimes are installed, which is the only thing on the
      machine that reliably knows. The registry keys an installer would normally search are
      not usable: one reads a version older than what is installed, and the other no longer
      exists.
    -->
    <netfx:DotNetCoreSearch Id="SearchRuntime10"
                            RuntimeType="core"
                            Platform="x64"
                            MajorVersion="10"
                            Variable="DotNetRuntime10" />

    <Chain>

      <!--
        PerMachine="no" is deliberate and is not a description of what the runtime installer
        does. It keeps this bundle's own bookkeeping under the current user, which is what
        stops Burn asking for administrator before it has established that anything needs it.
        Microsoft's installer carries its own request for administrator and raises it when it
        actually runs - so a machine that already has .NET 10 sees no prompt at all.

        Permanent, because other programs use the runtime: removing Lego2STL must not take it
        away from them.
      -->
      <ExePackage Id="DotNetRuntime"
                  DisplayName="Microsoft .NET $(RuntimeVersion) Runtime"
                  PerMachine="no"
                  Permanent="yes"
                  Vital="yes"
                  Compressed="no"
                  DetectCondition="DotNetRuntime10 &gt;= v10.0.0"
                  InstallArguments="/install /quiet /norestart">
        <ExePackagePayload
            Name="dotnet-runtime-$(RuntimeVersion)-win-x64.exe"
            DownloadUrl="$(RuntimeUrl)"
            ProductName="Microsoft .NET $(RuntimeVersion) Runtime"
            Description="Microsoft .NET $(RuntimeVersion) Runtime"
            Version="$(RuntimeVersion).0"
            Hash="$(RuntimeSha512)"
            Size="$(RuntimeSize)" />
        <!-- 3010 is "installed, restart to finish", which is a success and not a failure. -->
        <ExitCode Value="3010" Behavior="success" />
      </ExePackage>

      <MsiPackage Id="Lego2STL"
                  SourceFile="$(MsiPath)"
                  Vital="yes" />

    </Chain>

  </Bundle>

</Wix>
```

- [ ] **Step 3: Build it against the MSI that already exists**

The current `build-windows.ps1` still produces a self-contained MSI; that is fine, this step
only asks whether the bundle compiles and chains.

```powershell
./packaging/build-windows.ps1 -Version 0.0.0-gate
$pin = Get-Content packaging/runtime.json | ConvertFrom-Json
$p = $pin.platforms.'win-x64'
wix build packaging/windows/Bundle.wxs `
  -ext WixToolset.Bal.wixext -ext WixToolset.Netfx.wixext `
  -d "Version=0.0.0" `
  -d "RuntimeVersion=$($pin.version)" `
  -d "RuntimeUrl=$($pin.urlBase)/$($pin.version)/$($p.file)" `
  -d "RuntimeSha512=$($p.sha512)" `
  -d "RuntimeSize=$($p.size)" `
  -d "MsiPath=$(Resolve-Path artifacts/dist/Lego2STL-0.0.0-gate-win-x64.msi)" `
  -d "LicenseRtf=$(Resolve-Path packaging/windows/License.rtf)" `
  -o artifacts/dist/gate.exe
```

Expected: `gate.exe` is produced. Two failures are worth distinguishing:
- **`netfx:DotNetCoreSearch` unrecognised** → the extension is not really there. Stop.
- **The `Hash` is rejected** → Burn wants a different digest length. Fall back to
  `wix burn remotepayload <url>`, which downloads the file and emits the whole
  `ExePackagePayload` element; write it to `artifacts/staging/runtime-payload.wxs` and replace
  the inline element with `<?include ...?>`. Record which route worked.

- [ ] **Step 4: Answer question one — does it detect a runtime that is there?**

```powershell
dotnet --list-runtimes | Select-String 'Microsoft.NETCore.App 10\.'
./artifacts/dist/gate.exe /log $env:TEMP\gate-present.log
Select-String -Path $env:TEMP\gate-present.log -Pattern 'DotNetRuntime10|Detected|Skipping|DotNetRuntime'
```

Expected: the log shows `DotNetRuntime10` resolved to the installed version and the runtime
package **skipped**. If it plans to install the runtime on a machine that has 10.0.11, the
detect condition is wrong — fix it here, before anything depends on it.

- [ ] **Step 5: Answer question two — no prompt when the runtime is present**

Run `gate.exe` from a normal, non-elevated session and watch for a UAC prompt.

Expected: **no prompt**, and the log records a per-user registration. If a prompt appears:
- Confirm it is Burn's and not the MSI's, in the log.
- **Stop and report.** The fallback is a single UAC prompt on every install, which the user
  explicitly did not choose, so it is theirs to accept — not something to adopt quietly.

- [ ] **Step 6: Answer question three — it installs the runtime when it is missing**

On a machine or VM with no .NET 10 (a clean Windows Sandbox is enough — it has no .NET):

```powershell
.\gate.exe /log C:\gate-absent.log
```

Expected: exactly one UAC prompt, raised by Microsoft's runtime installer; the runtime is
installed; the MSI follows; afterwards `dotnet --list-runtimes` reports 10.x. If Windows
Sandbox is not available, record this as unverified and say so — do not claim it works.

- [ ] **Step 7: Commit**

```bash
git add packaging/windows/Bundle.wxs
git commit -m "feat: the Windows installer fetches .NET 10 only when the machine lacks it"
```

- [ ] **Step 8: Record progress**

Append `PHASE:INST-4 WAVE:0 STATUS:complete TS:<now>` to `PROGRESS.md`. If a question above
was answered "no", append `STATUS:failed` and stop the plan here.

---

## Task 5: The Windows build script

**Files:**
- Modify: `packaging/windows/Lego2STL.wxs` (the `Programs` component group, and the icon source)
- Modify: `packaging/build-windows.ps1` (whole script)

**Interfaces:**
- Consumes: `runtime.json` (Task 1), `Bundle.wxs` (Task 4).
- Produces: `artifacts/dist/Lego2STL-<version>-win-x64.exe` and `...-win-x64.zip`.

- [ ] **Step 1: Harvest the payload in the MSI**

The MSI listed two files by hand; the payload is now 65. In `packaging/windows/Lego2STL.wxs`,
replace the whole `<ComponentGroup Id="Programs" ...>` element with:

```xml
    <ComponentGroup Id="Programs" Directory="INSTALLFOLDER">
      <!-- 65 files rather than two, so they are gathered rather than listed. -->
      <Files Include="$(Publish)\**" />
    </ComponentGroup>

    <ComponentGroup Id="PathEntry" Directory="INSTALLFOLDER">
      <Component Id="ConsoleOnPath" Guid="D4A9F3B2-8C51-4E07-A6B3-1F852C9D7E40">
        <!--
          The console program goes on the path, so 'lego2stl' works from any terminal. The
          windowed one deliberately does not: starting it from a terminal is not how it is
          meant to be used, and two similarly named things on the path invites the wrong one.
        -->
        <Environment Id="PathEntry"
                     Name="PATH"
                     Value="[INSTALLFOLDER]"
                     Permanent="no"
                     Part="last"
                     Action="set"
                     System="no" />
        <RegistryValue Root="HKCU"
                       Key="Software\Lego2STL"
                       Name="onPath"
                       Type="integer"
                       Value="1"
                       KeyPath="yes" />
      </Component>
    </ComponentGroup>
```

The `Environment` element needs a component of its own now that the console program is
harvested rather than named, and a component needs a key path, which is what the registry
value is for.

Add the new group to the feature, beside the two already there:

```xml
    <Feature Id="Main" Title="Lego2STL" Level="1">
      <ComponentGroupRef Id="Programs" />
      <ComponentGroupRef Id="PathEntry" />
      <ComponentGroupRef Id="Shortcuts" />
    </Feature>
```

- [ ] **Step 2: Rewrite the build script**

Replace `packaging/build-windows.ps1` with:

```powershell
<#
.SYNOPSIS
  Builds the Windows package: an installer that fetches .NET 10 when it is missing, and a zip.

.DESCRIPTION
  Produces artifacts/dist:
    Lego2STL-<version>-win-x64.exe   the installer, when the WiX toolset is available
    Lego2STL-<version>-win-x64.zip   the same programs in a folder, to unpack anywhere

  The zip is always produced. An installer is a convenience; a folder that can be copied to a
  machine and run is what makes the tool portable, and is what any script wants. Both need
  .NET 10 on the machine - the installer puts it there, the zip expects it.

  Two programs, deliberately: a console one that can be scripted and write to a pipe, and a
  windowed one that does not flash a console when it starts. One executable cannot be both.

.NOTES
  Needs the .NET SDK. For the installer also:
      dotnet tool install --global wix --version 6.0.1
      wix extension add --global WixToolset.UI.wixext/6.0.1
      wix extension add --global WixToolset.Netfx.wixext/6.0.1
      wix extension add --global WixToolset.Bal.wixext/6.0.1
  Run on Windows. The Windows build is the only one that can read a document, because the
  text recogniser it uses is part of Windows.
#>

[CmdletBinding()]
param(
    [string]$Version = '1.0.0',
    [string]$Configuration = 'Release',
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$staging = Join-Path $root 'artifacts\staging\win-x64'
$payload = Join-Path $root 'artifacts\publish\win-x64'
$dist = Join-Path $root 'artifacts\dist'

# The Windows target framework, which is what carries the text recogniser.
$framework = 'net10.0-windows10.0.19041.0'

# An installer that grew past this is carrying the runtime again, which is the one thing this
# packaging exists to stop. Measured payload is 24.4 MB packed; the ceiling leaves room to
# grow without leaving room to regress.
$ceilingMb = 40

function Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }

Step "Publishing for win-x64 ($framework)"

Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $payload -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $payload, $dist | Out-Null

# Each project publishes into a folder of its own. Publishing two into one folder does not
# work: the second run clears what the first put there. They are gathered afterwards instead,
# which is also what lets them share one copy of everything they both use.
foreach ($name in 'Cli', 'Gui') {
    dotnet publish (Join-Path $root "src\Lego2STL.$name\Lego2STL.$name.csproj") `
        -c $Configuration -f $framework -r win-x64 `
        -p:Version=$Version `
        -o (Join-Path $staging $name) --nologo
    if ($LASTEXITCODE -ne 0) { throw "publish failed for Lego2STL.$name" }
}

Copy-Item (Join-Path $staging 'Cli\*') $payload -Recurse -Force
Copy-Item (Join-Path $staging 'Gui\*') $payload -Recurse -Force

# Debug databases are published beside the assemblies and are of no use to anyone installing
# this. The Skia one alone is 85 MB, more than the rest of the payload together.
Get-ChildItem $payload -Recurse -Filter *.pdb | Remove-Item -Force

# Both programs have to be here. Windows compares file names without regard to case, so two
# programs whose names differ only in capitalisation quietly become one. Checked, not assumed.
foreach ($name in 'lego2stl.exe', 'Lego2STL.Gui.exe') {
    if (-not (Test-Path (Join-Path $payload $name))) {
        throw "$name is missing from the payload. Do the two programs share a name?"
    }
}

$payloadMb = [math]::Round(((Get-ChildItem $payload -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host ("    {0} files, {1} MB" -f (Get-ChildItem $payload -Recurse -File).Count, $payloadMb)

Step 'Packing the zip'
$zip = Join-Path $dist "Lego2STL-$Version-win-x64.zip"
Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host ("    {0}  ({1} MB)" -f $zip, [math]::Round(((Get-Item $zip).Length / 1MB), 1))

if ($SkipInstaller) {
    Step 'Installer skipped'
    exit 0
}

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    Write-Warning 'The WiX toolset was not found, so no installer was built.'
    Write-Warning 'Install it with:  dotnet tool install --global wix --version 6.0.1'
    exit 0
}

# ---- The application, as an installer that needs no administrator ----------------------

Step 'Building the application installer'
$msi = Join-Path $staging "Lego2STL-$Version-win-x64.msi"

# Pinned to the toolset's own version: the extension and the tool are released together, and
# an unpinned add resolves to a newer one the tool refuses.
foreach ($extension in 'WixToolset.UI.wixext/6.0.1', 'WixToolset.Netfx.wixext/6.0.1', 'WixToolset.Bal.wixext/6.0.1') {
    wix extension add --global $extension
    if ($LASTEXITCODE -ne 0) { throw "could not add $extension" }
}

# Worked out first and quoted: an expression written inline after -d is split into a separate
# argument, and the toolset then reads the licence as another source file.
$license = Join-Path $PSScriptRoot 'windows\License.rtf'

wix build (Join-Path $PSScriptRoot 'windows\Lego2STL.wxs') `
    -ext WixToolset.UI.wixext `
    -d "Version=$Version" `
    -d "Publish=$payload" `
    -d "LicenseRtf=$license" `
    -o $msi
if ($LASTEXITCODE -ne 0) { throw 'the application installer did not build' }

# ---- The bundle, which puts .NET in place first when it has to -------------------------

Step 'Building the installer'

$pin = Get-Content (Join-Path $PSScriptRoot 'runtime.json') -Raw | ConvertFrom-Json
$platform = $pin.platforms.'win-x64'
$runtimeUrl = "$($pin.urlBase)/$($pin.version)/$($platform.file)"
Write-Host "    .NET $($pin.version), fetched from $runtimeUrl only when missing"

$exe = Join-Path $dist "Lego2STL-$Version-win-x64.exe"
Remove-Item -LiteralPath $exe -Force -ErrorAction SilentlyContinue

wix build (Join-Path $PSScriptRoot 'windows\Bundle.wxs') `
    -ext WixToolset.Bal.wixext `
    -ext WixToolset.Netfx.wixext `
    -d "Version=$Version" `
    -d "RuntimeVersion=$($pin.version)" `
    -d "RuntimeUrl=$runtimeUrl" `
    -d "RuntimeSha512=$($platform.sha512)" `
    -d "RuntimeSize=$($platform.size)" `
    -d "MsiPath=$msi" `
    -d "LicenseRtf=$license" `
    -o $exe
if ($LASTEXITCODE -ne 0) { throw 'the installer did not build' }

# The toolset writes a debugging database beside its output; it is not part of the package and
# only confuses anyone looking at the folder.
Get-ChildItem $dist -Filter *.wixpdb | Remove-Item -Force

$exeMb = [math]::Round(((Get-Item $exe).Length / 1MB), 1)
Write-Host ("    {0}  ({1} MB)" -f $exe, $exeMb)

if ($exeMb -gt $ceilingMb) {
    throw "the installer is $exeMb MB, over the $ceilingMb MB ceiling. Is it carrying the runtime again?"
}

Step 'Done'
```

- [ ] **Step 3: Build and check what came out**

```powershell
./packaging/build-windows.ps1 -Version 0.0.0-dev
Get-ChildItem artifacts/dist | Select-Object Name, @{n='MB';e={[math]::Round($_.Length/1MB,1)}}
```

Expected: an `.exe` around 25 MB and a `.zip` around 24 MB, and **no `.msi` in `dist`** — the
MSI is now an intermediate and lives in `staging`. If the `.exe` exceeds 40 MB the script
throws, which is the point.

- [ ] **Step 4: Check the zip actually runs**

```powershell
Expand-Archive artifacts/dist/Lego2STL-0.0.0-dev-win-x64.zip -DestinationPath $env:TEMP\l2s-zip -Force
& $env:TEMP\l2s-zip\lego2stl.exe --help
```

Expected: the help text. This machine has .NET 10, so a framework-dependent program runs
straight out of the folder — which is exactly what the zip promises.

- [ ] **Step 5: Install it and check the result**

```powershell
./artifacts/dist/Lego2STL-0.0.0-dev-win-x64.exe /log $env:TEMP\install.log
```

Expected: no UAC prompt (this machine has .NET 10), a Start Menu entry, `lego2stl --help`
working from a **new** terminal, and one entry in Programs and Features. Then install the
previous self-contained MSI first and re-run this, to confirm it upgrades rather than sitting
beside it — the `UpgradeCode` is unchanged for that reason.

- [ ] **Step 6: Commit**

```bash
git add packaging/build-windows.ps1 packaging/windows/Lego2STL.wxs
git commit -m "feat: the Windows installer is a tenth of the size it was"
```

- [ ] **Step 7: Record progress**

Append `PHASE:INST-5 WAVE:0 STATUS:complete TS:<now>` to `PROGRESS.md`.

---

## Task 6: The Linux `.run` installer

**Files:**
- Create: `packaging/linux/installer-header.sh`
- Create: `packaging/tests/run-installer.test.sh`
- Modify: `packaging/build-unix.sh` (the Linux branch)
- Modify: `packaging/linux/install.sh` (add the probe)

**Interfaces:**
- Consumes: `runtime-probe.sh` (Task 2), `payload.sh` (Task 3), `runtime.json` (Task 1).
- Produces: `artifacts/dist/Lego2STL-<version>-linux-x64.run` and `...-linux-x64.tar.gz`.
- The header takes these placeholders, substituted by `build-unix.sh`: `@VERSION@`,
  `@RUNTIME_VERSION@`, `@RUNTIME_URL@`, `@RUNTIME_SHA512@`, `@PAYLOAD_LINE@`.

- [ ] **Step 1: Write the failing test**

Create `packaging/tests/run-installer.test.sh`:

```sh
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
```

- [ ] **Step 2: Run it to make sure it fails**

```bash
chmod +x packaging/tests/run-installer.test.sh
./packaging/tests/run-installer.test.sh
```

Expected: fails immediately — no `.run` exists yet.

- [ ] **Step 3: Write the installer header**

Create `packaging/linux/installer-header.sh`:

```sh
#!/usr/bin/env sh
#
# Lego2STL @VERSION@ for Linux.
#
#   ./Lego2STL-@VERSION@-linux-x64.run                for you only, under ~/.local
#   sudo ./Lego2STL-@VERSION@-linux-x64.run --system  for everyone, under /usr/local
#
#   --prefix <dir>   somewhere else entirely
#   --no-runtime     never fetch .NET; stop instead if it is missing
#   --uninstall      remove what was installed
#
# This file is a script with a tarball attached to the end of it. .NET 10 is fetched only if
# this machine has not got it, from a fixed address whose contents are checked against a
# fingerprint built into this file - so a substituted download is refused rather than unpacked.
set -eu

VERSION="@VERSION@"
RUNTIME_VERSION="@RUNTIME_VERSION@"
RUNTIME_URL="@RUNTIME_URL@"
RUNTIME_SHA512="@RUNTIME_SHA512@"
PAYLOAD_LINE=@PAYLOAD_LINE@

prefix="$HOME/.local"
runtime_root=""
fetch_runtime=yes
action=install

while [ $# -gt 0 ]; do
    case "$1" in
        --system)     prefix="/usr/local"; shift ;;
        --prefix)     prefix="${2:?--prefix needs a directory}"; shift 2 ;;
        --no-runtime) fetch_runtime=no; shift ;;
        --uninstall)  action=uninstall; shift ;;
        -h|--help)    sed -n '3,17p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *)            echo "unknown option: $1" >&2; exit 2 ;;
    esac
done

say() { printf '%s\n' "$*"; }
step() { printf '==> %s\n' "$*"; }

# ---- the runtime probe, inserted here when this installer was built --------------------
@RUNTIME_PROBE@
# ---------------------------------------------------------------------------------------

if [ "$action" = uninstall ]; then
    step "Removing Lego2STL from $prefix"
    rm -rf "$prefix/lib/lego2stl"
    rm -f "$prefix/bin/lego2stl" "$prefix/bin/lego2stl-gui"
    rm -f "$prefix/share/applications/lego2stl.desktop"
    say "Done. The .NET runtime was left alone; other programs may be using it."
    exit 0
fi

step "Installing Lego2STL $VERSION into $prefix"

# ---- .NET 10, if this machine has not got it ------------------------------------------

if runtime_root="$(runtime_find)"; then
    say "    .NET 10 is already here: $runtime_root"
    private_runtime=no
else
    if [ "$fetch_runtime" = no ]; then
        say "No .NET 10 runtime was found, and --no-runtime was given." >&2
        say "Install .NET 10 - your distribution's package, or" >&2
        say "$RUNTIME_URL - and run this again." >&2
        exit 1
    fi

    if [ "$prefix" = "/usr/local" ]; then
        runtime_root="/usr/share/dotnet"
    else
        runtime_root="$HOME/.dotnet"
    fi

    step "Downloading .NET $RUNTIME_VERSION into $runtime_root"
    say "    from $RUNTIME_URL"

    tarball="$(mktemp)"
    if command -v curl >/dev/null 2>&1; then
        curl -fSL --progress-bar "$RUNTIME_URL" -o "$tarball"
    elif command -v wget >/dev/null 2>&1; then
        wget -q --show-progress -O "$tarball" "$RUNTIME_URL"
    else
        rm -f "$tarball"
        say "Neither curl nor wget is here, so .NET cannot be fetched." >&2
        say "Install one of them, or install .NET 10 yourself and run this again." >&2
        exit 1
    fi

    runtime_verify "$tarball" "$RUNTIME_SHA512" || exit 1
    say "    fingerprint checked"

    mkdir -p "$runtime_root"
    tar -xzf "$tarball" -C "$runtime_root"
    rm -f "$tarball"
    private_runtime=yes
fi

# ---- the programs ---------------------------------------------------------------------

step "Unpacking"
mkdir -p "$prefix/lib/lego2stl" "$prefix/bin" "$prefix/share/applications"
tail -n +"$PAYLOAD_LINE" "$0" | tar -xz -C "$prefix/lib/lego2stl"

install -m 0644 "$prefix/lib/lego2stl/lego2stl.desktop" \
                "$prefix/share/applications/lego2stl.desktop"
rm -f "$prefix/lib/lego2stl/lego2stl.desktop"

# The programs are started through a small script each. A runtime this installer put under a
# home directory is not anywhere the programs would look on their own, so the script says
# where it is; when the machine already had .NET, there is nothing to say and the script only
# stands in the way of the program by a few microseconds.
for pair in "lego2stl lego2stl" "lego2stl-gui Lego2STL.Gui"; do
    name="${pair% *}"
    program="${pair#* }"
    {
        printf '#!/usr/bin/env sh\n'
        if [ "$private_runtime" = yes ]; then
            printf 'DOTNET_ROOT="%s"\n' "$runtime_root"
            printf 'export DOTNET_ROOT\n'
            printf 'PATH="$DOTNET_ROOT:$PATH"\n'
            printf 'export PATH\n'
        fi
        printf 'exec "%s/lib/lego2stl/%s" "$@"\n' "$prefix" "$program"
    } > "$prefix/bin/$name"
    chmod 0755 "$prefix/bin/$name"
done

# So the new menu entry appears without logging out. Not every system has this, and it not
# being there is not a failure.
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$prefix/share/applications" 2>/dev/null || true
fi

say ""
say "Done."
case ":${PATH}:" in
    *":$prefix/bin:"*) say "Run 'lego2stl --help' to begin." ;;
    *) say "$prefix/bin is not on your PATH. Add this to your shell's startup file:"
       say "    export PATH=\"\$PATH:$prefix/bin\"" ;;
esac

exit 0
__PAYLOAD_BELOW__
```

- [ ] **Step 4: Assemble it in the build script**

In `packaging/build-unix.sh`, replace the whole Linux branch (`if [ "$platform" = "linux" ]`
… up to the `else`) with:

```bash
if [ "$platform" = "linux" ]; then
  # ---- Linux: a self-extracting installer, and a tarball anyone can unpack -------------

  step "Gathering the tarball"
  tree="$staging/tree"
  rm -rf "$tree"
  mkdir -p "$tree"
  cp -R "$payload/." "$tree/"
  cp "$here/linux/lego2stl.desktop" "$tree/"
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
```

Also, earlier in `build-unix.sh`, replace the two `dotnet publish` calls and the `cli`/`gui`
variables with one call to the payload builder:

```bash
step "Publishing for $rid"
payload="$staging/payload"
"$here/lib/payload.sh" "$rid" "$version" "$payload"
```

And update the file's header comment to describe a `.run` and a `.tar.gz` rather than a
`.deb`, and to say that .NET 10 is installed when missing rather than carried.

- [ ] **Step 5: Teach the tarball's install.sh the same probe**

In `packaging/linux/install.sh`, after the `prefix` is worked out, insert:

```sh
here="$(cd "$(dirname "$0")" && pwd)"

# The same probe the .run installer uses. This script only reports; unlike the installer it
# fetches nothing, because someone who unpacked a tarball by hand did not ask it to.
. "$here/runtime-probe.sh"
if ! runtime_find >/dev/null; then
    echo "Note: no .NET 10 runtime was found, and the programs need one." >&2
    echo "      Install .NET 10, or use the .run installer, which fetches it." >&2
fi
```

and change the two `install -m 0755` lines to copy the whole payload folder rather than two
binaries:

```sh
mkdir -p "$prefix/lib/lego2stl" "$prefix/bin" "$prefix/share/applications"
cp -R "$here/." "$prefix/lib/lego2stl/"
rm -f "$prefix/lib/lego2stl/install.sh" "$prefix/lib/lego2stl/lego2stl.desktop"
ln -sf "$prefix/lib/lego2stl/lego2stl"     "$prefix/bin/lego2stl"
ln -sf "$prefix/lib/lego2stl/Lego2STL.Gui" "$prefix/bin/lego2stl-gui"
install -m 0644 "$here/lego2stl.desktop" "$prefix/share/applications/lego2stl.desktop"
```

Update its opening comment: the programs are no longer self-contained, and .NET 10 has to be
present.

- [ ] **Step 6: Build and run the test**

On Linux, or in the act container (Task 9). On Windows this step is skipped — `build-unix.sh`
needs a Linux `dotnet publish` host and `tar` behaving as GNU tar.

```bash
./packaging/build-unix.sh linux x64 0.0.0-dev
./packaging/tests/run-installer.test.sh
```

Expected: 10 checks pass, including the runtime-less install that really downloads 36 MB.
**Record the real `.run` size** and replace the spec's Linux estimate with it.

- [ ] **Step 7: Commit**

```bash
git add packaging/linux/installer-header.sh packaging/linux/install.sh \
        packaging/build-unix.sh packaging/tests/run-installer.test.sh
git commit -m "feat: one Linux installer that fetches .NET 10 when the machine lacks it"
```

- [ ] **Step 8: Record progress**

Append `PHASE:INST-6 WAVE:0 STATUS:complete TS:<now>` to `PROGRESS.md`.

---

## Task 7: The macOS universal package

Nothing here can be verified on this machine. The build asserts what it can and fails loudly
otherwise; the first real proof comes from the runner in Task 8.

**Files:**
- Create: `packaging/macos/fuse-universal.sh`
- Create: `packaging/macos/preinstall`
- Create: `packaging/macos/distribution.xml`
- Modify: `packaging/build-unix.sh` (the macOS branch)

**Interfaces:**
- Consumes: `payload.sh` (Task 3), `runtime-probe.sh` (Task 2), `runtime.json` (Task 1).
- Produces: `artifacts/dist/Lego2STL-<version>-osx-universal.pkg` and `...-osx-universal.zip`.
- `fuse-universal.sh <x64-dir> <arm64-dir> <out-dir>` — one payload that runs on both.

- [ ] **Step 1: Write the fuser**

Create `packaging/macos/fuse-universal.sh`:

```sh
#!/usr/bin/env bash
#
#   fuse-universal.sh <x64-payload> <arm64-payload> <out>
#
# Turns two payloads into one that runs on any Mac. The assemblies are the bulk of it and are
# identical either way, so they are carried once; only the handful of genuinely native files -
# the two program launchers, Skia, PDFium, HarfBuzz - are doubled.
#
# Anything that differs and is not a Mach-O binary is an error rather than a guess: it means
# something in the build is not reproducible, and quietly picking one of the two would ship
# whichever the loop happened to reach first.
set -euo pipefail

x64="${1:?usage: fuse-universal.sh <x64> <arm64> <out>}"
arm="${2:?}"
out="${3:?}"

command -v lipo >/dev/null 2>&1 || { echo "lipo is needed and was not found" >&2; exit 1; }

rm -rf "$out"
mkdir -p "$out"

fused=0
copied=0

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

    echo "$relative differs between the two builds and is not a program." >&2
    echo "Something in the build is not reproducible; refusing to pick one." >&2
    exit 1
done

( cd "$arm" && find . -type f ) | while IFS= read -r relative; do
    if [ ! -f "$out/$relative" ]; then
        echo "    only in the Apple silicon payload, carried as it is: $relative"
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
```

- [ ] **Step 2: Write the preinstall script**

Create `packaging/macos/preinstall`:

```sh
#!/bin/sh
#
# Runs before the application is put in place, as root, and installs .NET 10 if this Mac has
# not got it. Nothing happens when it has.
#
# /usr/local/share/dotnet specifically: that is where .NET expects to be on a Mac, so the
# programs find it without being told. An application opened from the Finder inherits no shell
# environment, so anywhere else would need something the programs cannot rely on.
set -eu

RUNTIME_VERSION="@RUNTIME_VERSION@"
RUNTIME_URL_X64="@RUNTIME_URL_X64@"
RUNTIME_SHA512_X64="@RUNTIME_SHA512_X64@"
RUNTIME_URL_ARM64="@RUNTIME_URL_ARM64@"
RUNTIME_SHA512_ARM64="@RUNTIME_SHA512_ARM64@"

# ---- the runtime probe, inserted here when this package was built ----------------------
@RUNTIME_PROBE@
# ---------------------------------------------------------------------------------------

if runtime_find >/dev/null; then
    echo "Lego2STL: .NET 10 is already installed; leaving it alone."
    exit 0
fi

case "$(uname -m)" in
    arm64) url="$RUNTIME_URL_ARM64"; sha="$RUNTIME_SHA512_ARM64" ;;
    x86_64) url="$RUNTIME_URL_X64"; sha="$RUNTIME_SHA512_X64" ;;
    *) echo "Lego2STL: unknown processor $(uname -m); cannot fetch .NET." >&2; exit 1 ;;
esac

echo "Lego2STL: installing .NET $RUNTIME_VERSION from $url"

tarball="$(mktemp)"
curl -fSL "$url" -o "$tarball"
runtime_verify "$tarball" "$sha" || exit 1

mkdir -p /usr/local/share/dotnet
tar -xzf "$tarball" -C /usr/local/share/dotnet
rm -f "$tarball"

echo "Lego2STL: .NET $RUNTIME_VERSION installed."
```

- [ ] **Step 3: Write the distribution description**

Create `packaging/macos/distribution.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- What the installer window shows, and where the application goes. -->
<installer-gui-script minSpecVersion="2">
  <title>Lego2STL</title>
  <options customize="never" require-scripts="true" hostArchitectures="x86_64,arm64" />
  <domains enable_localSystem="true" enable_anywhere="false" enable_currentUserHome="false" />
  <choices-outline>
    <line choice="default" />
  </choices-outline>
  <choice id="default" visible="false">
    <pkg-ref id="com.lego2stl.app" />
  </choice>
  <pkg-ref id="com.lego2stl.app">app.pkg</pkg-ref>
</installer-gui-script>
```

- [ ] **Step 4: Rewrite the macOS branch of the build script**

In `packaging/build-unix.sh`, replace the whole `else` branch (the macOS half) with:

```bash
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
           --identifier com.lego2stl.app \
           --version "$version" \
           --install-location / \
           "$component"

  pkg="$dist/Lego2STL-$version-osx-universal.pkg"
  rm -f "$pkg"
  productbuild --distribution "$here/macos/distribution.xml" \
               --package-path "$staging" \
               --resources "$here/macos" \
               "$pkg"

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
```

Note the `stat` difference: `stat -f%z` on macOS, `stat -c%s` on Linux. The Linux branch uses
the Linux form; do not share one helper between them.

- [ ] **Step 5: Check what can be checked here**

```bash
bash -n packaging/build-unix.sh
bash -n packaging/macos/fuse-universal.sh
sh -n packaging/macos/preinstall
./packaging/build-unix.sh macos arm64 0.0.0-dev; echo "exit: $?"
```

```powershell
# Well-formedness only, and with PowerShell's own reader rather than a Python XML parser -
# the stdlib ones resolve external entities by default, which is a poor habit to write into a
# build even when the file being read is one this repository wrote.
[xml](Get-Content packaging/macos/distribution.xml -Raw) | Out-Null; "distribution.xml parses"
```

Expected: the three scripts parse, the XML parses, and the build **exits 1** with the "this is
not macOS" message — refusing rather than leaving a folder that resembles a package.

- [ ] **Step 6: Commit**

```bash
git add packaging/macos/fuse-universal.sh packaging/macos/preinstall \
        packaging/macos/distribution.xml packaging/build-unix.sh
git commit -m "feat: one macOS installer for every Mac, fetching .NET 10 when needed"
```

- [ ] **Step 7: Record progress**

Append `PHASE:INST-7 WAVE:0 STATUS:complete TS:<now>` to `PROGRESS.md`, and note in the commit
body that macOS is unverified until the runner has built it.

---

## Task 8: The release workflow

**Files:**
- Modify: `.github/workflows/package.yml`

**Interfaces:**
- Consumes: every build script above.
- Produces: six artifacts per release plus `SHA256SUMS.txt`.

- [ ] **Step 1: Update the three package jobs**

In `.github/workflows/package.yml`:

Replace the `windows` job's toolset step with all three extensions:

```yaml
      - name: Install the installer toolset
        run: |
          dotnet tool install --global wix --version 6.0.1
          wix extension add --global WixToolset.UI.wixext/6.0.1
          wix extension add --global WixToolset.Netfx.wixext/6.0.1
          wix extension add --global WixToolset.Bal.wixext/6.0.1
```

Replace the whole `macos` job with a single one — no matrix:

```yaml
  macos:
    name: macos
    needs: [test, version]
    # One job, not two. The package is universal: the programs are published for both kinds of
    # Mac and fused, so one file serves every machine and only one runner is billed. macOS
    # minutes cost ten times Linux ones.
    runs-on: macos-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build the package
        env:
          VERSION: ${{ needs.version.outputs.number }}
        run: ./packaging/build-unix.sh macos universal "$VERSION"

      - uses: actions/upload-artifact@v4
        with:
          name: macos
          path: artifacts/dist/*
          if-no-files-found: error
```

`build-unix.sh` must accept `universal` as its architecture for macOS — extend its
architecture check to `x64|arm64|universal`, and have the macOS branch ignore the value, since
it always builds both. Reject `universal` for Linux, where it means nothing.

- [ ] **Step 2: Say what a release needs, in the release**

In the `release` job's publish step, replace the `flags=` line with:

```bash
          notes=$(mktemp)
          {
            echo "Needs **.NET 10**. Each installer puts it in place when the machine has not"
            echo "got it, and leaves it alone when it has. The zip and tarball expect it to be"
            echo "there already."
            echo
            echo "| System | Install with | Or unpack |"
            echo "|---|---|---|"
            echo "| Windows | \`.exe\` | \`.zip\` |"
            echo "| Linux | \`.run\` | \`.tar.gz\` |"
            echo "| macOS | \`.pkg\` (any Mac) | \`.zip\` |"
          } > "$notes"

          flags=(--generate-notes --notes-file "$notes" --title "Lego2STL $VERSION")
```

- [ ] **Step 3: Check the workflow parses and the graph is right**

```bash
python3 -c "import yaml,sys;d=yaml.safe_load(open('.github/workflows/package.yml'));print(sorted(d['jobs']))"
./packaging/act/run.ps1 -DryRun     # or run.sh --dry-run
```

Expected: jobs `linux, macos, release, test, version, windows`, and the dry run lists steps
without starting a container.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/package.yml packaging/build-unix.sh
git commit -m "ci: build the three installers for every release"
```

- [ ] **Step 5: Record progress**

Append `PHASE:INST-8 WAVE:0 STATUS:complete TS:<now>` to `PROGRESS.md`.

---

## Task 9: Building the packages locally

**Files:**
- Modify: `packaging/act/local-package.yml`
- Create: `packaging/local-windows.ps1`
- Modify: `packaging/act/run.ps1`, `packaging/act/run.sh` (the summary they print)

**Interfaces:**
- Consumes: every build script above.
- Produces: `.act-artifacts/linux/*` from act; `artifacts/dist/*` from the Windows script.

- [ ] **Step 1: Make the act job prove the installer works**

In `packaging/act/local-package.yml`, replace the `Look inside what was built` step with:

```yaml
      # What a local run is actually for. The real workflow cannot do this: its runners always
      # have .NET, so the path a stranger takes - no runtime, fetch one, check it, unpack it,
      # run - is never exercised there. Here dotnet is taken out of the environment and the
      # search roots are pointed somewhere empty, so the installer genuinely finds nothing.
      - name: Install what was built, as a machine with no .NET
        run: |
          set -euo pipefail
          cd artifacts/dist

          echo "=== produced ==="
          ls -lh

          installer=$(ls ./*linux-x64.run)
          echo
          echo "=== $installer, on a machine that has .NET ==="
          "$installer" --prefix /tmp/have
          /tmp/have/bin/lego2stl --help | head -3

          echo
          echo "=== $installer, on a machine that has none ==="
          mkdir -p /tmp/nowhere /tmp/barehome
          env -i HOME=/tmp/barehome PATH=/usr/bin:/bin \
              RUNTIME_SEARCH_ROOTS=/tmp/nowhere \
              "$installer" --prefix /tmp/bare
          test -d /tmp/barehome/.dotnet/shared/Microsoft.NETCore.App
          env -i HOME=/tmp/barehome PATH=/usr/bin:/bin /tmp/bare/bin/lego2stl --help | head -3

          echo
          echo "=== the tarball holds ==="
          tar -tzf ./*linux-x64.tar.gz | head -20

      - name: Run the packaging tests
        run: |
          set -euo pipefail
          ./packaging/tests/runtime-pin.test.sh
          ./packaging/tests/runtime-probe.test.sh
          ./packaging/tests/run-installer.test.sh
```

Update the file's header comment: the `.deb` is gone, and what this job now proves is the
runtime-acquisition path rather than the shape of a `.deb`.

- [ ] **Step 2: Write the Windows runner**

Create `packaging/local-windows.ps1`:

```powershell
<#
.SYNOPSIS
  Builds the Windows package on this machine, the way the workflow's windows job does.

.DESCRIPTION
  Not act, deliberately. act runs Linux containers, and a Windows runner is not a container at
  all, so there is nothing for it to run the windows job in. This does what that job does,
  step for step, against the same scripts - which is what makes a green run here mean
  something about the real one.

  Ends by looking inside the installer, because the thing most worth checking is invisible
  from the outside: that the runtime is fetched when needed rather than carried.

.PARAMETER Version
  The version to stamp on the packages. Must look like 1.2.0, or 1.2.0-rc1.

.PARAMETER SkipTests
  Skip the test suite. The workflow runs it in a job of its own before packaging.

.EXAMPLE
  ./packaging/local-windows.ps1
  ./packaging/local-windows.ps1 -Version 1.2.0
#>

[CmdletBinding()]
param(
    [string]$Version = '0.0.0-local',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot

function Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Problem($message) { Write-Host "!!  $message" -ForegroundColor Red }

# ---- The things whose absence gives an unhelpful error later ---------------------------

Step 'Checking what is needed'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Problem 'The .NET SDK is not on the path.'
    exit 1
}
Write-Host "    dotnet $(dotnet --version)"

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    Problem 'The WiX toolset is not on the path. Install it with:'
    Write-Host '    dotnet tool install --global wix --version 6.0.1'
    exit 1
}
Write-Host "    wix    $(wix --version)"

# Refuse a version the workflow would refuse, before spending minutes on it. The same script
# the workflow uses, so the two cannot disagree.
& bash (Join-Path $PSScriptRoot 'version.sh') $Version | Out-Null
if ($LASTEXITCODE -ne 0) {
    Problem "'$Version' is not a version the packages can carry. Use 1.2.0, or 1.2.0-rc1."
    exit 1
}

# ---- What the workflow's jobs do -------------------------------------------------------

if (-not $SkipTests) {
    Step 'Running the tests, as the test job does'
    dotnet test --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { Problem 'the tests failed'; exit 1 }
}

Step 'Building the package, as the windows job does'
& (Join-Path $PSScriptRoot 'build-windows.ps1') -Version $Version
if ($LASTEXITCODE -ne 0) { Problem 'the build failed'; exit 1 }

# ---- The part CI cannot show anyone ----------------------------------------------------

Step 'Looking inside the installer'

$exe = Get-ChildItem (Join-Path $root 'artifacts\dist') -Filter '*win-x64.exe' |
    Select-Object -First 1
if (-not $exe) { Problem 'no installer was produced'; exit 1 }

$unpacked = Join-Path $root 'artifacts\staging\bundle'
Remove-Item -LiteralPath $unpacked -Recurse -Force -ErrorAction SilentlyContinue
wix burn extract $exe.FullName -o $unpacked | Out-Null

$manifest = Join-Path $unpacked 'BundleManifest.xml'
if (-not (Test-Path $manifest)) { Problem 'the installer has no manifest to read'; exit 1 }

$xml = [xml](Get-Content $manifest -Raw)
$runtime = $xml.BurnManifest.Payload | Where-Object { $_.Name -like '*dotnet-runtime*' }

if (-not $runtime) {
    Problem 'the installer does not mention the .NET runtime at all.'
    exit 1
}

if ($runtime.Packaging -ne 'external') {
    Problem "the runtime is '$($runtime.Packaging)' rather than external - it is being carried, not fetched."
    exit 1
}

Write-Host "    the runtime is fetched, not carried: $($runtime.DownloadUrl)"
Write-Host ("    installer: {0}  ({1} MB)" -f $exe.Name, [math]::Round($exe.Length / 1MB, 1))

Step 'Done'
Get-ChildItem (Join-Path $root 'artifacts\dist') |
    ForEach-Object { Write-Host ("      {0,8:N1} MB  {1}" -f ($_.Length / 1MB), $_.Name) }
```

- [ ] **Step 3: Run both, and check the Windows one catches a regression**

```powershell
./packaging/local-windows.ps1 -Version 1.2.3
```

Expected: tests pass, the package builds, and the inspection reports the runtime as external
with its download URL.

Then prove the check has teeth — temporarily set `Compressed="yes"` on the `ExePackage` in
`Bundle.wxs`, rebuild, and confirm the script fails with "it is being carried, not fetched".
Revert the change.

```powershell
./packaging/act/run.ps1 -Version 1.2.3
```

Expected: the container builds the `.run` and the tarball, installs both ways, and the three
packaging tests pass. Allow ten to fifteen minutes on a first run.

- [ ] **Step 4: Commit**

```bash
git add packaging/local-windows.ps1 packaging/act/local-package.yml \
        packaging/act/run.ps1 packaging/act/run.sh
git commit -m "build: the packages can be built and checked without GitHub"
```

- [ ] **Step 5: Record progress**

Append `PHASE:INST-9 WAVE:0 STATUS:complete TS:<now>` to `PROGRESS.md`.

---

## Task 10: Documentation

**Files:**
- Modify: `README.md` (the install section)
- Modify: `packaging/README.md` (most of it)
- Modify: `README-act.md` (what runs locally)

- [ ] **Step 1: Update `packaging/README.md`**

Replace the *What comes out* table with:

```markdown
| System | File | What it is |
|---|---|---|
| Windows | `Lego2STL-<version>-win-x64.exe` | The installer. Installs .NET 10 first if the machine has not got it, then Lego2STL for the current user - no administrator unless the runtime is missing. Start Menu entry, and `lego2stl` on the path. |
| Windows | `Lego2STL-<version>-win-x64.zip` | The same folder, to unpack anywhere. Expects .NET 10 to be there. |
| Linux | `Lego2STL-<version>-linux-x64.run` | The installer. `./…run` for you alone under `~/.local`; `sudo ./…run --system` for everyone. Fetches .NET 10 into `~/.dotnet` when there is none. |
| Linux | `Lego2STL-<version>-linux-x64.tar.gz` | The same folder plus `install.sh`. Expects .NET 10 to be there. |
| macOS | `Lego2STL-<version>-osx-universal.pkg` | The installer, for any Mac. Installs .NET 10 into `/usr/local/share/dotnet` when there is none. |
| macOS | `Lego2STL-<version>-osx-universal.zip` | The same application bundle. |
```

Then add these sections, and delete the ones about the `.deb` and the disk image:

```markdown
## .NET is a dependency, not a passenger

Every package used to carry its own copy of .NET, twice over - once per program. That made the
Windows installer 152 MB. Now the two programs share one folder of 67 MB, 24 MB packed, and
the runtime is something the installer puts on the machine if it is not already there.

The runtime is pinned in `runtime.json`: a version, and per platform a file name, a SHA512 and
a byte count. Downloads come from `https://builds.dotnet.microsoft.com/dotnet/Runtime/<version>/<file>`,
which never changes under a given version, so a pinned fingerprint stays true. Every installer
checks that fingerprint before unpacking anything, and refuses otherwise.

Bumping it is deliberate:

```
./packaging/refresh-runtime.sh          # the newest 10.0.x
./packaging/refresh-runtime.sh 10.0.11  # a particular one
```

Nothing downloads a script and runs it. Microsoft publish `dotnet-install.sh` for this and it
is deliberately not used: it is a moving target fetched at install time, and all it does here
is unpack a tarball, which the installers do against a fingerprint they were built with.

## How each system is asked whether it has .NET

Not through the registry, on any of them. Two things were measured on a machine with .NET
10.0.10 installed: the `sharedhost` version read `8.0.29`, and the `sharedfx` key that most
installers search did not exist. Runtime patches also install side by side, so there is no one
upgrade family to look for.

- **Windows** asks the host resolver, through WiX's `netfx:DotNetCoreSearch`.
- **Linux and macOS** look in the places a runtime is kept - `$DOTNET_ROOT`, `~/.dotnet`,
  `/usr/share/dotnet`, `/usr/lib/dotnet`, `/usr/local/share/dotnet` - and then ask
  `dotnet --list-runtimes` if there is a `dotnet` on the path.

Any `10.0.x` counts. The programs ask for 10.0.0 and the host picks the newest patch it has.

## Nothing is a single file any more

It was, and the change is deliberate. A framework-dependent single file cannot be compressed -
the SDK refuses, `NETSDK1176` - and came out at 66.9 MB against the 57 MB compressed
self-contained one it would have replaced, while carrying some 60 MB of shared assemblies
twice, once per program. One shared folder is 67.4 MB for both programs and 24.4 MB packed.

## The macOS package is one file for both kinds of Mac

The programs are published twice, for Intel and for Apple silicon, and fused: files that are
identical - the assemblies, which are most of it - are carried once, and the few genuinely
native ones are combined with `lipo`. Every fused binary is checked for both architectures,
and a build that cannot produce one fails rather than shipping a package that runs on half the
Macs.
```

- [ ] **Step 2: Update `README.md`**

In the install section, replace the download table with the six artifacts above and add, in
one sentence each: .NET 10 is required; each installer fetches it when missing; the archives
expect it. Keep the existing note that reading a document needs Windows.

- [ ] **Step 3: Update `README-act.md`**

Change the *What runs, and what cannot* table:

```markdown
| Job | Locally | Why |
|---|---|---|
| `version` | **yes** | plain shell |
| `linux` | **yes** | the container is Ubuntu; it builds the `.run` and the tarball, installs both, and runs the packaging tests |
| `windows` | **yes, but not through act** | `./packaging/local-windows.ps1` - see below |
| `test` | no | runs on Windows, because reading a document needs the recogniser that is part of Windows |
| `macos` | no | needs macOS for `lipo`, `codesign`, `pkgbuild` and `productbuild` |
| `release` | no | publishes to GitHub, which is not a thing to do from a laptop by accident |
```

And add:

```markdown
## Windows, without act

act runs Linux containers. A Windows runner is not a container, so there is nothing for act to
run the `windows` job in - this is a limit of the approach, not something left undone.

```powershell
./packaging/local-windows.ps1 -Version 1.2.3
```

That does what the `windows` job does, step for step, against the same scripts: the tests, the
build, and then a look inside the installer to confirm the .NET runtime is fetched when needed
rather than carried. Needs the WiX toolset:

```powershell
dotnet tool install --global wix --version 6.0.1
```

## What the Linux run now proves

More than it used to. The container has .NET installed, so simply installing the `.run` there
says nothing about the case that matters. The job therefore installs it twice: once normally,
and once with `dotnet` taken out of the environment and the runtime search pointed at an empty
directory, so the installer genuinely finds nothing, downloads the pinned runtime, checks its
fingerprint, unpacks it, and runs the program against it. That path does not exist on any
GitHub runner.
```

Also correct the opening: what a local run builds is the `.run` and the tarball, not a `.deb`.

- [ ] **Step 4: Check the documentation matches what was built**

```bash
grep -rn 'deb\|dmg\|self-contained\|single file\|152 MB' README.md packaging/README.md README-act.md
ls artifacts/dist
```

Expected: every remaining mention is deliberately historical ("used to carry", "was, and the
change is deliberate"), and the file names in the documentation match what is actually in
`artifacts/dist`.

- [ ] **Step 5: Commit**

```bash
git add README.md packaging/README.md README-act.md
git commit -m "docs: what the installers need, and what they fetch"
```

- [ ] **Step 6: Record progress**

Append `PHASE:INST-10 WAVE:0 STATUS:complete TS:<now>` to `PROGRESS.md`.

---

## Task 11 (optional): Build the macOS package on Linux

**Skip this unless the user asks for it.** It exists because the user asked whether macOS can
be verified from this hardware. It cannot be *run* here — Apple licenses macOS virtualisation
only on Apple hardware — but it can be *built* here, which would let act cover all three
systems and remove the "unverified until the runner" gap from Task 7.

Everything below is additive: nothing in Tasks 1–10 changes, and the macOS job on the real
runner stays exactly as it is.

**Files:**
- Create: `packaging/macos/build-on-linux.sh`
- Modify: `packaging/act/local-package.yml` (a second job)

- [ ] **Step 1: Confirm the three tools can do it**

```bash
sudo apt-get install -y bomutils xar
winget install LLVM.LLVM     # on Windows, for llvm-lipo
llvm-lipo --version
which mkbom xar
```

`llvm-lipo` creates and reads universal Mach-O binaries anywhere; `mkbom` and `xar` are what a
`.pkg` is made of. If any of the three is unavailable, stop and report — this task is a
convenience and is not worth working around.

- [ ] **Step 2: Write the Linux builder**

`packaging/macos/build-on-linux.sh`, which:
1. calls `payload.sh osx-x64` and `payload.sh osx-arm64`;
2. calls `fuse-universal.sh` with `LIPO=llvm-lipo` — add that indirection to
   `fuse-universal.sh`, defaulting to `lipo`, so one script serves both hosts;
3. lays out `Lego2STL.app` and the `pkgroot` exactly as the macOS branch does;
4. builds the component with `mkbom` + `xar` rather than `pkgbuild`, and the product archive
   with `xar` rather than `productbuild`;
5. **stamps the package as unsigned in its own name** — `…-osx-universal-unsigned.pkg` —
   because nothing here can run `codesign`, and a package that looks like the real one but is
   not is worse than no package.

- [ ] **Step 3: Prove it is really universal**

```bash
llvm-lipo -archs artifacts/staging/universal/lego2stl
llvm-lipo -archs artifacts/staging/universal/Lego2STL.Gui
xar -tf artifacts/dist/*osx-universal-unsigned.pkg
```

Expected: both programs report `x86_64 arm64`, and the archive lists a `Bom`, a `PackageInfo`
and a `Payload`.

- [ ] **Step 4: Commit**

```bash
git add packaging/macos/build-on-linux.sh packaging/macos/fuse-universal.sh \
        packaging/act/local-package.yml
git commit -m "build: the macOS package can be assembled without a Mac, unsigned"
```

- [ ] **Step 5: Record progress**

Append `PHASE:INST-11 WAVE:0 STATUS:complete TS:<now>` to `PROGRESS.md`.

---

## Task 12: What the program could look like, and how it could feel

**This task is the user's own request and must not be dropped, including after a context
clear.** It is last because it is independent of everything above: it changes nothing about
packaging and produces proposals for the user to choose from, not an implementation.

The user asked, in their words, for the **UI designer** and **UX architect** agents from the
Agency Agents plugin, **3–5 different UI layouts and colour schemes** to choose from, and **at
least 3 UX proposals**.

**Files:**
- Read: `src/Lego2STL.Gui/Views/MainWindow.axaml`, `InputView.axaml`, `OptionsView.axaml`,
  `CatalogueView.axaml`, `RunView.axaml`
- Read: `src/Lego2STL.Gui/ViewModels/MainViewModel.cs`, `RunOptionsViewModel.cs`,
  `CataloguePartViewModel.cs`
- Read: `src/Lego2STL.Core/Text/Strings.English.cs`, `Strings.Italian.cs` — every label the
  window shows comes from here, in two languages, and any proposal that invents wording has to
  say which keys it would add
- Create: `docs/superpowers/specs/2026-08-25-gui-proposals.md`

**Interfaces:**
- Consumes: nothing from Tasks 1–11.
- Produces: a document of proposals and a published Artifact for comparing the layouts
  visually. Implementation is a separate plan, written only after the user chooses.

- [ ] **Step 1: Read the window as it is now**

Before dispatching anyone, write down in the document what the program's window does today:
its four views, what each is for, how a run is started, and what the user sees while one is
running. Proposals that do not name the thing they are changing are not usable.

- [ ] **Step 2: Dispatch both agents, in parallel, in one message**

Two `Agent` calls in a single message so they run concurrently. Give each the file list above
and these constraints, which are not negotiable and which a general-purpose designer will
otherwise break:

- **Avalonia 12.1, not the web.** No CSS, no HTML, no React. Proposals are in terms of
  Avalonia controls, `Fluent` theme, and what is achievable in `.axaml`.
- **Two languages, from resource keys.** English and Italian, both from
  `Strings.English.cs`/`Strings.Italian.cs`. German-length labels are not the constraint;
  Italian ones are, and they are frequently longer than the English.
- **Desktop, three systems.** Windows, Linux and macOS, from one code base.
- **Reading a document is Windows-only.** On Linux and macOS that path is unavailable and the
  window has to say so gracefully rather than hide it. Any proposal that ignores this is
  describing a program that does not exist.
- **No new dependencies** without saying so explicitly and why.

Ask the **UI designer** for **4 proposals**, each with: a name, an ASCII layout sketch of the
main window, a complete colour scheme as concrete hex values for light *and* dark, a type
scale, and one sentence on who it suits. Four rather than three so the user has a real choice,
and rather than five so each is worth reading.

Ask the **UX architect** for **3 proposals**, each with: a name, the flow it proposes as a
numbered sequence of what the user does, what it fixes about today's flow, what it costs, and
how it handles a run that fails halfway. Ask explicitly for one conservative proposal that
keeps the current structure and improves it, so the user is not forced to choose between three
rewrites.

- [ ] **Step 3: Write up what came back, without laundering it**

Assemble `docs/superpowers/specs/2026-08-25-gui-proposals.md`: the current-state description
from Step 1, then the four UI proposals, then the three UX proposals. Where the agents
disagree, or where a proposal breaks one of the constraints above, **say so in the document**
rather than quietly fixing it — a proposal the user chooses on a false premise costs far more
than one they reject.

- [ ] **Step 4: Make the layouts comparable**

Publish an Artifact showing the four layouts side by side, with each colour scheme rendered as
real swatches rather than named. Load the `artifact-design` skill first, as required. Colour
schemes cannot be judged from hex values in a table.

- [ ] **Step 5: Ask the user to choose**

Use `AskUserQuestion`: one question for the UI layout (4 options, with the ASCII sketch as each
option's `preview`), one for the colour scheme, one for the UX proposal. Then **stop**.
Implementing the choice is a new plan, and it starts with brainstorming, not with editing
`.axaml`.

- [ ] **Step 6: Commit the proposals**

```bash
git add docs/superpowers/specs/2026-08-25-gui-proposals.md
git commit -m "docs: proposals for how the window could look and behave"
```

- [ ] **Step 7: Record progress**

Append `PHASE:INST-12 WAVE:0 STATUS:complete TS:<now>` to `PROGRESS.md`.

---

## Self-review

**Spec coverage.** Every section of the spec maps to a task: build model → 3; runtime pin → 1;
Windows bundle → 4, 5; Linux `.run` → 6; macOS universal `.pkg` → 7; CI → 8; local runs → 9;
size guard → 5, 6, 7; error handling → the tests in 2 and 6 plus the ceilings; documentation →
10. Spec verification items 1, 2 and 5 are Task 4 and Task 5 Step 5; items 3 and 4 are Task 9
Step 1; item 6 is unverifiable here and is marked so in Task 7.

**Two things the spec said that this plan deliberately changes**, both from evidence gathered
while writing it:

1. The spec's `runtime.json` had no `size`. Burn needs a byte count as well as a hash to fetch
   a remote payload, so Task 1 adds it.
2. The spec left `refresh-runtime` as `.ps1` in one place and `.sh` in another. It is `.sh`
   everywhere here, because `version.sh` already set that precedent and the build scripts that
   read the pin run on all three systems.

**Names used consistently across tasks:** `runtime_find`, `runtime_sha512`, `runtime_verify`,
`runtime_search_roots`, `runtime_root_has_ten` (Task 2, used in 6 and 7);
`payload.sh <rid> <version> <out>` (Task 3, used in 6 and 7); `RUNTIME_SEARCH_ROOTS` (Task 2,
used in 6 and 9); `@RUNTIME_PROBE@` (Tasks 6 and 7); the `runtime.json` keys `version`,
`urlBase`, `platforms.<rid>.{file,sha512,size}` (Task 1, read in 5, 6, 7).

**Known gap, stated rather than hidden:** Task 6's build and test steps need a Linux host. On
this Windows machine they run inside act (Task 9), which means Task 6 cannot be fully closed
before Task 9's container works. Run Task 9 Step 1 early if Task 6 needs to be proven sooner.
